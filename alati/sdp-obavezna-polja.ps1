# Izvlači iz ServiceDesk Plus-a podatke potrebne za zatvaranje tiketa iz aplikacije (PLAN 2b):
# polja zahtjeva (i dodatna UDF polja), šablone zahtjeva, pravila zatvaranja i primjere zatvorenih tiketa.
# Ništa ne mijenja u SDP-u – samo čita (GET).
#
# Pokretanje (PowerShell kao administrator, na testnom serveru gdje radi SDP):
#   powershell -ExecutionPolicy Bypass -File .\sdp-obavezna-polja.ps1
# Na kraju nastane C:\Instalacija\sdp-polja.zip – taj fajl pošaljite.

$adresa = Read-Host "Adresa SDP-a (Enter = https://localhost)"
if ([string]::IsNullOrWhiteSpace($adresa)) { $adresa = "https://localhost" }
$adresa = $adresa.TrimEnd('/')
$kljuc = Read-Host "API ključ (isti kao u Šifarnici -> Veza sa ServiceDesk Plus)"
$primjeri = Read-Host "Brojevi nekoliko ZATVORENIH tiketa (npr. Printing, Sistem, Hardver), odvojeni zarezom"

# Poziva se preko curl.exe: Windows PowerShell 5.1 ne uspostavlja TLS 1.3 vezu sa SDP-om 15200
# („underlying connection was closed”), a curl.exe radi; -k jer testni SDP ima samopotpisan certifikat.
$izlaz = "C:\Instalacija\sdp-polja"
New-Item $izlaz -ItemType Directory -Force | Out-Null
Remove-Item "$izlaz\*" -Force -ErrorAction SilentlyContinue

function Uzmi($putanja, $fajl, $inputData) {
    $argumenti = @('-k', '-s', '-o', "$izlaz\$fajl", '-w', '%{http_code}',
                   '-H', "authtoken: $kljuc", '-H', 'Accept: application/vnd.manageengine.sdp.v3+json')
    # input_data se kodira ovdje (PowerShell 5.1 kvari navodnike u argumentima za curl.exe)
    $url = "$adresa$putanja"
    if ($inputData) { $url += "?input_data=" + [uri]::EscapeDataString($inputData) }
    $kod = & curl.exe @argumenti $url
    if ($kod -eq '200') {
        Write-Host "OK     $putanja"
        try { return (Get-Content "$izlaz\$fajl" -Raw -Encoding UTF8 | ConvertFrom-Json) } catch { return $null }
    }
    $tekst = if (Test-Path "$izlaz\$fajl") { Get-Content "$izlaz\$fajl" -Raw -Encoding UTF8 } else { "" }
    Add-Content "$izlaz\greske.txt" "$putanja : HTTP $kod $tekst"
    Write-Host "NEMA   $putanja (HTTP $kod)"
    return $null
}

$lista = '{"list_info":{"row_count":100}}'

# Opis svih polja zahtjeva (i dodatnih UDF polja s nazivima)
Uzmi "/api/v3/requests/_metainfo" "metainfo.json" $null | Out-Null

# Šabloni zahtjeva i detalji svakog (polja, obavezna polja)
$sabloni = Uzmi "/api/v3/request_templates" "sabloni.json" $lista
if ($sabloni -and $sabloni.request_templates) {
    foreach ($s in $sabloni.request_templates) { Uzmi "/api/v3/request_templates/$($s.id)" "sablon_$($s.id).json" $null | Out-Null }
}

# Pravila zatvaranja (naziv se razlikuje po verzijama – probaju se oba)
Uzmi "/api/v3/request_closure_rules" "pravila_zatvaranja.json" $null | Out-Null
Uzmi "/api/v3/closure_rules" "pravila_zatvaranja_2.json" $null | Out-Null
Uzmi "/api/v3/request_closure_codes" "sifre_zatvaranja.json" $lista | Out-Null
Uzmi "/api/v3/statuses" "statusi.json" $lista | Out-Null

# Primjeri zatvorenih tiketa (vidi se kako su obavezna polja popunjena)
foreach ($t in ($primjeri -split '[,; ]+' | Where-Object { $_ -match '^\d+$' })) {
    Uzmi "/api/v3/requests/$t" "tiket_$t.json" $null | Out-Null
}

Compress-Archive -Path "$izlaz\*" -DestinationPath "C:\Instalacija\sdp-polja.zip" -Force
Write-Host ""
Write-Host "Gotovo: C:\Instalacija\sdp-polja.zip"

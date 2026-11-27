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

# Testni SDP ima samopotpisan certifikat; SDP 15200 traži TLS 1.3 (isto kao u koraku 13)
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
[Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$izlaz = "C:\Instalacija\sdp-polja"
New-Item $izlaz -ItemType Directory -Force | Out-Null
Remove-Item "$izlaz\*" -Force -ErrorAction SilentlyContinue
$zaglavlja = @{ authtoken = $kljuc; Accept = "application/vnd.manageengine.sdp.v3+json" }

function Uzmi($putanja, $fajl, $inputData) {
    $url = "$adresa$putanja"
    if ($inputData) { $url += "?input_data=" + [uri]::EscapeDataString($inputData) }
    try {
        $odg = Invoke-WebRequest -Uri $url -Headers $zaglavlja -UseBasicParsing
        [IO.File]::WriteAllText("$izlaz\$fajl", $odg.Content, [Text.Encoding]::UTF8)
        Write-Host "OK     $putanja"
        return ($odg.Content | ConvertFrom-Json)
    } catch {
        $poruka = $_.Exception.Message
        if ($_.ErrorDetails) { $poruka += " " + $_.ErrorDetails.Message }
        Add-Content "$izlaz\greske.txt" "$putanja : $poruka"
        Write-Host "NEMA   $putanja"
        return $null
    }
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

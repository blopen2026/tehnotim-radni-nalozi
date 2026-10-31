# Korak 13 – API ključ i prvi poziv prema ServiceDesk Plus-u (test kopija)

Cilj: aplikacija Radni nalozi će čitati tikete iz SDP-a i upisivati napomene preko **SDP REST API v3**.
U ovom koraku samo pravimo pristup i ručno probamo poziv – kod aplikacije još se ne mijenja.

Sve se radi na **testnoj kopiji** (`https://TEST-NALOZI`).

## 1. Poseban tehničar za aplikaciju

U SDP-u: **Admin → Users → Technicians → + New Technician** (nazivi menija mogu se malo razlikovati).

| Polje | Vrijednost |
|---|---|
| Ime | `Radni nalozi (API)` |
| E-mail | može ostati prazno ili interna adresa |
| Login | uključiti (*Enable login for this technician*), korisničko ime `radninalozi.api`, jaka lozinka |
| Uloga / role | za početak **SDAdmin** ili uloga koja smije: pregled svih zahtjeva, dodavanje napomena (notes), dodavanje priloga |

> Zašto poseban tehničar: u istoriji tiketa se vidi da je napomenu upisala aplikacija, a ključ se može ugasiti
> bez diranja ličnih naloga. Kasnije (u produkciji) suzićemo ulogu na samo ono što treba.

## 2. API ključ

Otvorite tog tehničara → **Generate API Key** (ili *API key* → *Generate*).
Rok trajanja: *Never expires* (ili najduži ponuđeni).

**Ključ je lozinka:** ne šaljite ga u chat, ne upisujte ga u Git. Sačuvajte ga privremeno u Notepad na serveru.

## 3. Prvi poziv (PowerShell na serveru TEST-NALOZI)

SDP koristi vlastiti (samopotpisani) certifikat, pa za ovu probu u PowerShell-u privremeno preskačemo provjeru certifikata
(važi samo za taj prozor PowerShell-a):

```powershell
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
[Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$kljuc = Read-Host "API kljuc"
$zaglavlje = @{ authtoken = $kljuc; Accept = "application/vnd.manageengine.sdp.v3+json" }
$upit = '{"list_info":{"row_count":5,"sort_field":"id","sort_order":"desc"}}'
$url = "https://localhost/api/v3/requests?input_data=" + [uri]::EscapeDataString($upit)

$odgovor = Invoke-RestMethod -Uri $url -Headers $zaglavlje
$odgovor.requests | Select-Object id, subject, @{n='status';e={$_.status.name}}, @{n='site';e={$_.site.name}}
```

Očekivano: tabela sa 5 zadnjih tiketa (broj, naslov, status, site).

## 4. Jedan tiket u cijelosti (za mapiranje polja)

Upišite broj jednog tipičnog tiketa (npr. zahtjev za servis štampača kod klijenta):

```powershell
$id = Read-Host "Broj tiketa"
$tiket = Invoke-RestMethod -Uri "https://localhost/api/v3/requests/$id" -Headers $zaglavlje
$tiket | ConvertTo-Json -Depth 8 | Out-File C:\Instalacija\tiket-primjer.json -Encoding utf8
```

Fajl `C:\Instalacija\tiket-primjer.json` pošaljite – iz njega se vidi gdje su site, department, requester,
eksterni broj klijenta (ako je u nekom dodatnom polju) itd. To je osnova za korak 14 (tabela veza) i 15 (nacrti naloga).

## Ako ne radi

| Poruka | Uzrok |
|---|---|
| `401` / `authentication failed` | pogrešan ključ ili tehničar nema login |
| `403` / `permission denied` | ulozi tehničara fali pravo na zahtjeve |
| `Could not establish trust relationship` | nije pokrenut red `ServerCertificateValidationCallback` u istom prozoru |
| `Unable to connect` | SDP servis ne radi ili nije na 443 |

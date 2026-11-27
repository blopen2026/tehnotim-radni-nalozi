# Elektronska potvrda naloga (ovlaštena lica klijenta)

> **Za sada isključeno (27.11.2026.)** – sektor mreža ne može objaviti `/potvrda/` prema van (nema WAF-a).
> Nalog se zatvara dugmetom „Zatvori nalog” (vidi DOGOVORI.md). Kod ostaje u aplikaciji; da se uključi:
> skinuti `hidden` s kartice „Ovlaštena lica” u `klijent.html`, otkomentarisati prikaz kartice „Slanje e-maila” u `sifarnici.html`
> i u `nalog.html` vratiti odjeljak „Potvrda korisnika” (funkcije `popuniLica`, `prikaziPotvrdu`, `posaljiPotvrdu` su još tu).

Završen nalog se e-mailom šalje **ovlaštenom licu klijenta**. Ono otvori link, pregleda nalog i klikne
**„Potvrđujem nalog”** → nalog postaje **Završen**, a osoba dobije e-mail sa završnim PDF-om (sa upisanom potvrdom),
koji može odštampati, potpisati i predati svojoj službi finansija. Ako nešto nije tačno, umjesto potvrde pošalje
**primjedbu** → nalog se vraća u **U radu**, a nosilac naloga i onaj ko je poslao nalog dobiju e-mail.

Dogovor: `DOGOVORI.md`, 2026-11-27 – Potvrda naloga.

## Šta je novo u aplikaciji

| Gdje | Šta |
|---|---|
| Klijenti → klijent → **Ovlaštena lica** | Ime i prezime, e-mail, funkcija; važi za sve jedinice ili samo za izabrane; „aktivno”. Mijenja kancelarija i administrator. |
| Nalog → **Potvrda korisnika** | Izbor ovlaštenog lica (prvo lica za tu jedinicu, pa za sve jedinice, pa ostala) i dugme **Pošalji na potvrdu**. Stanje: čeka potvrdu / potvrđeno / primjedba. Potpis na ekranu ostaje kao druga mogućnost (kad je korisnik tu). |
| Šifarnici → **Slanje e-maila i potvrda naloga** (administrator) | SMTP server, port, zaštita, korisnik, lozinka, pošiljalac, **javna adresa aplikacije** i dugme za probni e-mail. |
| PDF naloga | Red „Elektronska potvrda korisnika” (stanje, ko, kad, kod potvrde); dno: lijevo „Nalog izvršio” (ime nosioca, bez potpisa), desno „Ovjera korisnika usluge (potpis korisnika)” – prazno polje za ručni potpis. |
| `/potvrda/…` | Stranica za korisnika – jedina stranica koja se otvara **bez Windows prijave**. Pristup daje dugi slučajni ključ iz linka; u bazi je samo njegov otisak (SHA-256). |

Pravila:
- Prije slanja nalog mora imati status i opis izvršene intervencije.
- „Pošalji ponovo” pravi novi link; stari prestaje da važi.
- Izmjena naloga dok čeka potvrdu je dozvoljena (korisnik na stranici uvijek vidi trenutni nalog).
- Nalog koji je korisnik potvrdio može mijenjati samo kancelarija/administrator; izmjena se upisuje u istoriju.
- Ako je nalog iz tiketa, pri potvrdi se u tiket upisuje napomena o završetku + PDF (kao do sada pri potpisu).
- Faza `ceka_potpis` se sada prikazuje kao **„Čeka potvrdu”**.

## Postavljanje nove verzije na testni server

Laptop: `git pull` → `objavi.cmd` → `Copy-VMFile` (kao do sada). Nova verzija koristi paket **MailKit** – `objavi.cmd` ga sam preuzme (treba internet na laptopu).

Server (PowerShell kao administrator):

```powershell
Stop-WebAppPool RadniNalozi
Expand-Archive C:\Instalacija\RadniNalozi.zip -DestinationPath C:\inetpub\RadniNalozi -Force
Start-WebAppPool RadniNalozi
```

Pri prvom pokretanju izvrši se skripta `010_potvrda_naloga.sql`.

### Jednom: stranica potvrde bez Windows prijave

Na sajtu se uključi **i anonimni pristup** (uz Windows prijavu, koja ostaje uključena). Prijavu tada traži sama aplikacija:
sve osim `/potvrda/…` i dalje traži Windows prijavu (aplikacija odgovori 401, a IIS pokrene Windows prijavu).

Napomena: anonimni pristup se **ne** uključuje posebno za putanju `RadniNalozi/potvrda` – IIS tada tu putanju ne šalje aplikaciji
nego traži fajl na disku (greška 404.0, handler StaticFile).

```powershell
Import-Module WebAdministration
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location 'RadniNalozi' -Filter 'system.webServer/security/authentication/anonymousAuthentication' -Name enabled -Value $true
```

Provjera:

```powershell
curl.exe -s -o NUL -w "%{http_code}`n" http://localhost/potvrda/proba    # 200 – stranica „Link više ne važi”
curl.exe -s -o NUL -w "%{http_code}`n" http://localhost/                 # 401 – ostatak traži prijavu
```

## Podešavanje u aplikaciji

1. **Šifarnici → Slanje e-maila i potvrda naloga**: upisati podatke mail servera firme (server, port, zaštita, korisnik, lozinka,
   adresa pošiljaoca, npr. `nalozi@tehnotim.ba`) i **javnu adresu** (za test: `http://TEST-NALOZI` ili IP adresa servera).
   Sačuvati, upisati svoju adresu i kliknuti **Pošalji probni e-mail**.
2. **Klijenti → klijent → Ovlaštena lica**: dodati bar jednu osobu (za probu: svoja e-mail adresa).

## Proba

1. Otvoriti nalog tog klijenta, izabrati status, upisati opis intervencije, sačuvati.
2. U „Potvrda korisnika” izabrati lice → **Pošalji na potvrdu**. Nalog prelazi u „Čeka potvrdu”.
3. U e-mailu (sa PDF-om u prilogu) kliknuti **Pregledaj i potvrdi nalog** → otvara se stranica naloga.
4. **Potvrđujem nalog** → stranica pokaže „Nalog je potvrđen”; stiže drugi e-mail sa završnim PDF-om; nalog u aplikaciji je „Završen”.
5. Probati i primjedbu: poslati nalog ponovo (drugi nalog), na stranici „Nešto nije tačno – pošalji primjedbu”.
   Nalog se vraća u „U radu”, primjedba se vidi na nalogu, nosilac dobija e-mail (ako ima upisan e-mail u Korisnicima).

## Za produkciju (12d) – pitanje za sektor mreža

Link mora raditi **iz mreže klijenta**. Treba odlučiti kako se iz interneta (ili mreže klijenata) dolazi do aplikacije:
objaviti **samo putanju `/potvrda/`** preko HTTPS-a (npr. `https://nalozi.tehnotim.ba/potvrda/…`, reverse proxy ili firewall pravilo),
a ostatak aplikacije ostaviti samo u internoj mreži. Javna adresa u Šifarnicima se onda postavi na tu adresu.

# Dogovori i odluke

Ovaj fajl se čita na početku svakog rada na projektu. Novi dogovori se dopisuju na vrh, s datumom.

## 2026-10-29 – Klijenti i uvoz

- Excel/CSV čita server (ClosedXML), ne preglednik – uvoz ne zavisi od interneta; podržani .xlsx i .csv (stari .xls sačuvati kao .xlsx).
- Prepoznavanje kolona, ćirilica → latinica i pregled rade na stranici; spajanje s postojećim jedinicama radi server, u jednoj transakciji.
- Pravila dopune: isti naziv ili ista adresa = ista jedinica; naziv i kontakt osoba ostaju; telefon se dodaje uz postojeći.
- Klijente i jedinice uređuju kancelarija i administrator; čitanje je dozvoljeno svima (treba formi naloga).

## 2026-10-29 – Nosilac naloga

- Nosilac naloga je korisnik aplikacije; posebna tabela Serviser je ukinuta (skripta 004).
- Svaki aktivni korisnik je na listi nosilaca, osim ako mu se isključi „Može biti nosilac naloga” (stranica Korisnici).
- Šifarnici: tipovi i materijal mijenja kancelarija; podatke firme, tarifu i numeraciju mijenja administrator.
- Pristup bazi u kodu: Dapper (SQL ostaje čitljiv u kodu).

## 2026-10-29 – Prijava i uloge

- Firma ima domen; prijava ide domenskim nalogom (Windows/Negotiate), bez posebnih lozinki.
- Uloge: serviser (nalozi), kancelarija (+ klijenti, uređaji, uvoz), administrator (+ korisnici, postavke).
- Uloga se vodi u tabeli `Korisnik`; prvi korisnik koji otvori aplikaciju dok je tabela prazna postaje administrator.
- Mora ostati bar jedan aktivan administrator.

## 2026-10-29 – Kako se mijenja baza

- Baza se opisuje SQL skriptama u `src/Tehnotim.RadniNalozi/Baza/Skripte` (001, 002, …); aplikacija ih sama izvršava pri pokretanju.
- Izvršena skripta se nikad ne mijenja; svaka izmjena = nova skripta.
- Nalog čuva i kopiju podataka kakvi su bili u trenutku naloga (naziv klijenta, lokacija, adresa, kontakt, model…),
  da stari nalozi i PDF ostanu isti i kad se podaci u bazi kasnije promijene.
- Broj naloga = redni broj u godini + godina (0001/2026); brojač po godini u tabeli `BrojacNaloga`.

## 2026-10-29 – Novi razvojni laptop

- Razvoj se seli na novi laptop sa čistim Windows 11 (stari računar ima SQL 2014, BarTender 2019 i neuspjelu instalaciju 2025).
- Na laptopu: SQL Server 2022 Express, instanca `SQL2022`, Windows autentifikacija; aplikacija lokalno koristi `.\SQL2022`.
- Prije instalacije SQL-a provjeriti veličinu sektora diska (`fsutil fsinfo sectorinfo C:`); ako je veća od 4096, prvo podesiti registar.

## 2026-10-29 – SQL Server na razvojnom računaru

- Razvojni računar ima tri instance: `BARTENDER` (program BarTender – ne dirati), `SQLEXPRESS` (SQL Server 2014 RTM – ne koristiti, ne dirati),
  `SQLEXPRESS01` (SQL Server 2025 Express – koristi ova aplikacija).
- Lokalna veza je u `appsettings.Development.json` (važi kad se pokreće sa `dotnet run`).
  Veza za server ide u `appsettings.json` / postavke servera pri instalaciji.
- Za server: SQL Server 2019 ili noviji, TLS 1.2.

## 2026-10-29 – Upozorenje TLS 1.0

- Na razvojnom računaru SqlClient javlja: „The negotiated TLS 1.0 is an insecure protocol”.
- Lokalno nije rizik (veza ne izlazi iz računara), ali **prije instalacije na server obavezno**:
  veza aplikacija ↔ SQL Server mora ići preko TLS 1.2 (ažuran SQL Server + TLS 1.2 uključen u Windowsu).
- Stranica `/api/status` prikazuje vrstu veze i šifrovanje radi provjere.

## 2026-10-29 – Početak

- Server: Windows; aplikacija ASP.NET Core u IIS-u, baza SQL Server Express.
- ServiceDesk Plus: izdanje Standard, build 15200, instaliran na našem serveru.
- Klijenti upisuju svoj broj tiketa; tačno polje još treba utvrditi (posebno polje ili tekst).
- 6 servisera; nalozi se uglavnom popunjavaju u kancelariji.
- Klijent potpisuje na licu mjesta ili kasnije (faza „Čeka potpis”); potpis servisera nije obavezan.
- Nalog završen = postoji potpis korisnika.
- Izgled i ponašanje: probna verzija u `prototip/` je referenca.

### Potvrđeno u probnoj verziji

- Klijent ima više poslovnih jedinica (mjesto, adresa, poštanski broj, telefoni, radno vrijeme, kontakt, km od BL).
- Uvoz jedinica iz Excela: prepoznavanje kolona po zaglavlju, ćirilica → latinica, dopuna postojećih po nazivu ili adresi.
- Poziv na zahtjev: interni i eksterni broj (tiket klijenta).
- Uređaj: serijski, model, inventurni broj, tip; brojač stranica po uređaju samo za tipove s brojačem; oznaka „samo crno-bijeli”.
- Utrošeno vrijeme: računa se iz početka i završetka ili se upisuje ručno.
- Ime PDF fajla: skraćenica klijenta + interni + eksterni broj (bez brojeva: + broj naloga).
- PDF: logo gore lijevo; gornji dio tabele do Garancije sa desnom stranom podijeljenom napola; donji dio sa Kolor/C/B/Ukupno iste širine.

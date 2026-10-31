# Dogovori i odluke

Ovaj fajl se čita na početku svakog rada na projektu. Novi dogovori se dopisuju na vrh, s datumom.

## 2026-10-31 – Početna i izvještaji

- Početna prema ulozi: serviser vidi samo svoje naloge (u radu, čeka potpis, posljednji) i broj tiketa svog sektora; kancelarija/administrator brojke po fazama i sve naloge; administrator i magacin i stanje sistema (sklopljeno). `/api/status` samo administrator.
- Na stranici Nalozi serviser i dalje vidi **sve** naloge (kolega može preuzeti nalog).
- Izvještaji: kancelarija i administrator sve, serviser samo naloge kojima je nosilac. Služe i za fakturisanje i za druge svrhe; izvoz u Excel i štampa/PDF.

## 2026-10-31 – Veza sa ServiceDesk Plus-om (faza 2)

- Nalog iz tiketa se pravi **ručno**: u aplikaciji se upiše/izabere broj tiketa (uz listu otvorenih tiketa bez naloga), aplikacija popuni nacrt. Automatika (svaki novi tiket / pri dodjeli tehničara) eventualno kasnije.
- Iz tiketa: interni broj = `id`; klijent = SDP `site` (tabela veza site → klijent); vrijeme zahtjeva = `created_time`.
- RGU šalje tikete iz svog Redmine-a: eksterni broj = broj iza `#` u naslovu (`[TEHNOTIM - Хардвер #39422]`), poslovna jedinica iz teksta („Аутор: Kalinovik PJ”), opis = tekst bez HTML-a i Redmine zaglavlja; ćirilica → latinica.
- Ostali klijenti uglavnom šalju obične mailove – pravila za njih nakon pregleda primjera tiketa.
- Ako se jedinica ne prepozna, bira se ručno u nacrtu.
- **Sektori** (Tehnička, Printing, Sistem, Mreže; ostali sektori zbog licence idu na Tehničku): sektor = SDP zajednički nalog (tehničar) i/ili grupa. Korisnik može biti u više sektora.
- Nosilac naloga iz tiketa bira se **ručno**; nude se serviseri sektora kojem je tiket dodijeljen u SDP-u (prvo tehničar, pa grupa), uz „prikaži sve”. Sektor se upisuje u nalog.
- Poslovna jedinica: aplikacija pamti vezu requester → jedinica (prvi put predloži po imenu, korisnik potvrdi).
- **Povratak u tiket:** napomena kad se nalog iz tiketa prvi put sačuva i napomena + PDF u prilogu kad nalog postane završen. Napomene vide samo tehničari; status tiketa aplikacija ne mijenja (ručno u SDP-u). Neuspio upis ne sprečava čuvanje – ponavlja se dugmetom „Pošalji u tiket”.
- Adresa SDP-a i API ključ su u Postavke (baza), mijenja ih administrator u Šifarnicima; ključ se nikad ne šalje pregledniku.

## 2026-10-30 – Testno okruženje

- Prije produkcije pravi se testni server koji simulira produkcioni: virtuelna mašina (Hyper-V) na razvojnom laptopu (16 GB RAM).
- VM `TEST-NALOZI`: Windows Server 2022 (evaluation), 8 GB RAM fiksno, 4 vCPU, 120 GB, External prekidač `LAN`.
- Na VM: SQL Server Express (naša baza), IIS + aplikacija, kopija ServiceDesk Plus 15200 (mail isključen).
- Prije svakog većeg koraka checkpoint.
- SDP kopija na testu (31.10.2026.): build 15200 SP-7, vraćen database backup 15200 iz produkcije (pri vraćanju odgovoreno „No” = testna instalacija → postavke dolaznog i odlaznog maila prazne), custom triggers isključeni, firewall blokira mail portove. Adresa: **https://TEST-NALOZI** (port 443, kao produkcija). Licenca: evaluation, ističe oko 29.11.2026. – kad istekne, tražiti od ManageEngine-a test licencu. Prilozi (fileAttachments) nisu vraćeni.
- SDP 15200 traži TLS 1.3 → aplikacija pri pozivu SDP API-ja mora dozvoliti TLS 1.3 (i vjerovati SDP certifikatu – na testu samopotpisan). Licenca je popunjena (5 tehničara): na testu je jednom tehničaru uklonjena prijava i napravljen `Radni nalozi (API)`; za produkciju odluka kasnije (dokupiti mjesto / postojeći nalog / provjeriti kod ME da li tehničar bez prijave troši licencu).
- Testni server se za sada NE stavlja u domenu (rad od kuće): radi u radnoj grupi, prijava u aplikaciju lokalnim nalozima servera (TEST-NALOZI\ime). U domenu se može dodati kasnije u firmi.
- IP adresa testnog servera ostaje na DHCP-u (laptop se seli između kuće i firme); pristupa se po imenu ili trenutnoj IP adresi.
- Produkcioni ServiceDesk Plus koristi ugrađeni PostgreSQL → i test kopija SDP-a koristi PostgreSQL (naša aplikacija ostaje na SQL Server Express).
- Produkcioni SDP šalje/prima mail preko običnog mail servera (SMTP/POP/IMAP) → na testnom serveru se firewallom blokiraju portovi 25, 465, 587, 110, 995, 143, 993 prije prvog pokretanja kopije.
- Aplikacija na serveru: IIS, sajt i pool `RadniNalozi` (No Managed Code), folder `C:\inetpub\RadniNalozi`, port 80, Windows prijava, bez anonimnog pristupa.
  Na SQL se spaja kao `IIS APPPOOL\RadniNalozi` (dbcreator – sama pravi bazu). Paket se pravi sa `objavi.cmd`.

## 2026-10-30 – Magacin rezervnih dijelova

- Magacin se vodi samo količinski (bez finansija), jedan magacin, upravlja samo administrator.
- Artikal ima naziv, PN i stanje; vodi se po količini ili po serijskom broju (SN – svaki komad zaseban).
- Stanje se skida pri svakom čuvanju naloga; knjiži se samo razlika između starog i novog stanja naloga. Brisanje naloga vraća materijal.
- Stanje može ići u minus, ali se korisniku prijavljuje upozorenje.
- Materijal kojeg nema u magacinu može se upisati u nalog; evidentira se posebno („Utrošeno van magacina”) dok ga administrator ne poveže s artikalom ili označi riješenim.
- Svako kretanje se upisuje u karticu artikla (MagacinPromjena).
- Tehnički opis (tabele, fajlovi, logika knjiženja): `docs/korak-10b-magacin.md`, dio „Tehnički opis”.

## 2026-10-30 – PDF

- PDF pravi server (PDFsharp 6.1, MIT licenca), font Arial iz Windows foldera s fontovima (server mora imati Arial – standardno ima).
- Raspored PDF-a = prototip (dvije mreže kolona: gornji dio do Garancije, donji dio od „Datum i vrijeme zahtjeva”).
- Štampa: PDF se učita u skriveni okvir i otvara se dijalog za štampu preglednika.
- Podaci firme u zaglavlju i podnožju PDF-a dolaze iz Šifarnika (Podaci firme), logo iz `wwwroot/img/logo.png`.

## 2026-10-30 – Potpisi

- Potpisi (serviser, korisnik) se čuvaju u tabeli Nalog kao PNG (varbinary), do 500 KB.
- Faza se određuje pri svakom čuvanju: potpis korisnika → Završen; inače „potpisuje kasnije” → Čeka potpis; inače U radu.
- Potpis servisera nije obavezan; ime izvođača = nosilac naloga.

## 2026-10-30 – Lista naloga i brisanje

- Nalog brišu samo kancelarija i administrator; broj obrisanog naloga se ne koristi ponovo (rupa u numeraciji je namjerna).
- Pri brisanju se brojač uređaja vraća na zadnji preostali nalog s brojačem.
- Istorija uređaja za servisere: link iz forme naloga na listu naloga filtriranu po serijskom broju.

## 2026-10-30 – Test kopija ServiceDesk Plus

- Za fazu 2 pravi se test kopija SDP na razvojnom laptopu; mora biti **build 15200** kao radni sistem (test instalacija je bila 15100 → nadograditi).
- Razlog: u 15200 su promijenjeni **formati Request API-ja**, Request Life Cycle je zamijenjen Request Workflow-om,
  a okidači za uvezene tiketove koriste događaj „Imported” umjesto „Created”.
- Webhooks i custom triggers postoje u ovim verzijama → trenutno otvaranje naloga na novi tiket je vjerovatno moguće (provjeriti na 15200).
- Poslije vraćanja backupa na test kopiju odmah isključiti odlazni mail, AD sinhronizaciju i zakazane zadatke.

## 2026-10-30 – Interni broj zahtjeva

- U listama naloga ispod broja naloga prikazuje se **interni** broj zahtjeva (ID tiketa u ServiceDesk Plus) – to je veza s tiketing sistemom.
  Eksterni broj (tiket klijenta) vidi se u nalogu i na PDF-u.

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

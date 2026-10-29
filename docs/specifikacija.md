# Radni nalozi – specifikacija aplikacije

Stanje: 2026-10-29. Izvorni dokument: Claude Docs „Radni nalozi – specifikacija aplikacije”.

## Svrha i obim

Tehnotim d.o.o. treba internu web aplikaciju za radne naloge, instaliranu na vlastitom Windows serveru
i povezanu sa ServiceDesk Plus (Standard, build 15200, instaliran kod nas). Koristi je 6 servisera i
kancelarija; nalozi se uglavnom popunjavaju u kancelariji, a klijent potpisuje na licu mjesta ili kasnije.

Aplikacija treba da:

- vodi bazu klijenata, njihovih poslovnih jedinica, uređaja, tipova uređaja, materijala i servisera;
- otvara nacrt naloga automatski iz novog tiketa u ServiceDesk Plus;
- popunjava nalog povlačenjem podataka iz baze, uz potpis na ekranu;
- pravi PDF u izgledu postojećeg papirnog obrasca i štampa ga direktno;
- vraća broj naloga i PDF u tiket kad je posao završen.

Probna verzija (`prototip/radni-nalozi-prototip.html`) je referenca za izgled i ponašanje.

## Tehnička arhitektura

| Dio | Izbor | Napomena |
|---|---|---|
| Server | Windows Server, IIS | Mora vidjeti SDP preko mreže |
| Baza | SQL Server Express | Dnevna rezervna kopija, čuvanje najmanje 30 dana |
| Backend | ASP.NET Core (C#), jedan servis | Drži API ključ za SDP, radi sinhronizaciju i pravi PDF |
| Frontend | HTML + JavaScript iz prototipa, PWA | Radi na telefonu |
| Prijava | Windows / AD nalozi | Uloge: serviser, kancelarija, administrator |
| Pristup | HTTPS, interna mreža ili VPN | Za teren: VPN |
| PDF i štampa | PDF na serveru iz HTML šablona naloga | Štampa direktno iz preglednika |
| Fajlovi | Potpisi, PDF-ovi, skenovi | Ulaze u rezervnu kopiju |

API ključ za ServiceDesk Plus nikad ne ide u preglednik; sve pozive prema SDP radi backend.

## Model podataka

| Tabela | Polja | Pravila |
|---|---|---|
| Klijent | naziv, skraćenica (za ime fajla), ugovor o održavanju DA/NE | Naziv jedinstven |
| Poslovna jedinica | klijent, naziv, mjesto, adresa, poštanski broj, telefon (više brojeva), radno vrijeme, kontakt osoba, e-mail, km od Banje Luke (jedan smjer), SDP site/department | Naziv jedinstven unutar klijenta |
| Uređaj | serijski broj, inventurni broj, model, tip, samo crno-bijeli, klijent, poslovna jedinica, garancija do, zadnje stanje brojača kolor i C/B s datumom | Serijski jedinstven; traži se i po inventurnom |
| Tip uređaja | naziv, prati brojač stranica DA/NE | Početni: Štampač, Multifunkcijski, Fotokopir (s brojačem); Računar, Laptop, Skener, Monitor |
| Materijal | naziv, šifra | Novi materijal iz naloga dodaje se sam |
| Korisnik | domenski nalog, ime i prezime, e-mail (veza s tehničarom u SDP), uloga, aktivan, može biti nosilac naloga | Nosilac naloga = korisnik aplikacije (tabela Serviser ukinuta) |
| Nalog | sva polja ispod + ID SDP zahtjeva, faza, istorija izmjena | Broj 0001/2026, nastavlja se kroz godinu |
| Stavka uređaja na nalogu | nalog, uređaj, tip, kolor, C/B, ukupno | Brojač samo za tipove koji ga prate |
| Stavka materijala | nalog, materijal, količina, broj otpremnice | |
| Postavke | tarifa KM/km, sljedeći broj naloga, podaci firme, logo | |
| Veza SDP lokacija | SDP site/department → klijent + poslovna jedinica | Nepoznata lokacija ide na ručnu provjeru |

## Radni nalog

| Polje | Kako se popunjava |
|---|---|
| ID naloga | Automatski pri prvom čuvanju, 0001/2026 |
| Poziv na zahtjev: interni | ID SDP zahtjeva ili ručno |
| Poziv na zahtjev: eksterni | Broj tiketa klijenta ili ručno; nije obavezan |
| Datum izdavanja | Današnji datum |
| Datum i vrijeme zahtjeva | Vrijeme kreiranja SDP tiketa, ili sada |
| Početak i završetak rada | Ručno |
| Utrošeno vrijeme rada | Iz početka i završetka; može se upisati ručno i tada se ne preračunava |
| Korisnik, poslovna jedinica | Izbor iz baze; nova jedinica se dodaje klijentu pri čuvanju |
| Adresa, kontakt podaci | Iz poslovne jedinice |
| Naplata | DA/NE ručno |
| Status ugovora | Iz klijenta, izmjenjivo |
| Garancija | Iz uređaja (garancija do ≥ datum naloga), izmjenjivo |
| Uređaj predao | Tekst: datum i ime |
| Nosilac naloga | Izbor iz aktivnih korisnika označenih kao nosilac; iz SDP tehničara (po e-mailu) kad nalog dolazi iz tiketa |
| Uređaji (više) | Serijski ili inventurni broj → model, tip, klijent i jedinica iz baze |
| Brojač stranica | Po uređaju, samo za tipove s brojačem: kolor, C/B, ukupno; prethodno stanje i razlika |
| Opis zahtjeva | Iz tiketa ili ručno |
| Opis izvršene intervencije | Ručno |
| Utrošeni materijal | Izbor iz liste, količina, broj otpremnice |
| Putni troškovi | Km iz poslovne jedinice (tamo i nazad) × tarifa |
| Status | Obavljena / Djelimično obavljena / Nije obavljena / Nije potrebna |
| Intervencija | Na terenu / Servis-kancelarija |
| Potpis servisera | Ime iz nosioca naloga; potpis nije obavezan |
| Potpis korisnika | Na ekranu, ili oznaka „potpisuje kasnije” |

Faze: Novo iz tiketa → U radu → Čeka potpis → Završen. Završen = postoji potpis korisnika.
Zadnje stanje brojača uređaja ažurira se samo iz naloga s novijim datumom.

## Integracija sa ServiceDesk Plus

Backend svakih 1–2 minute čita nove zahtjeve preko REST API v3 i od svakog pravi nacrt naloga; kad je
nalog završen, u zahtjev upisuje napomenu s brojem naloga i prilaže PDF. Tiket čija lokacija nije
povezana s klijentom čeka ručnu provjeru u kancelariji.

| ServiceDesk Plus zahtjev | Radni nalog |
|---|---|
| ID zahtjeva | Interni broj zahtjeva |
| Posebno polje „Broj tiketa klijenta” (ili obrazac u naslovu) | Eksterni broj |
| Site ili department podnosioca, preko tabele veza | Klijent i poslovna jedinica |
| Podnosilac: ime, telefon, e-mail | Kontakt podaci |
| Naslov i opis (HTML u tekst) | Opis zahtjeva |
| Vrijeme kreiranja | Datum i vrijeme zahtjeva |
| Tehničar (po e-mailu) | Nosilac naloga |
| Asset na zahtjevu, ako se koristi | Uređaj |

- Adresa: `http://<server>:<port>/api/v3/requests`.
- Ključ: poseban tehničar „API – Radni nalozi” s ograničenom ulogom; authtoken u zaglavlju svakog poziva; čuva se samo na serveru.
- Povrat: napomena „Radni nalog 0001/2026 – Završen” + PDF kao prilog; po dogovoru promjena statusa.
- Opcija: okidač (custom trigger) s webhookom na novi zahtjev, ako ga izdanje Standard podržava.
- Svaki uvezeni ID zahtjeva se pamti; isti tiket se ne uvozi dvaput.
- Prije razvoja: provjeriti tačne nazive polja i filtera u API dokumentaciji servera (build 15200).

## PDF i štampa

- Zaglavlje: logo gore lijevo, podaci firme desno.
- Gornji dio tabele (do Garancije): desna strana podijeljena napola; Interni i Eksterni iste širine; ista okomita linija dijeli nazive od vrijednosti; DA/NE dijele desnu polovinu.
- Donji dio (od „Datum i vrijeme zahtjeva”): uređaji s kolonama Model, Serijski broj, Inv. broj, Kolor, C/B, Ukupno (zadnje tri iste širine); red sa zbirom kad su dva ili više uređaja s brojačem.
- Podnožje: JIB 4405312380000, MB 11667694, žiro-račun 572-106-00020639-67 MF Banka a.d. Banja Luka.
- Ime fajla: skraćenica + interni + eksterni broj (`RUGIPP_1298_39422.pdf`); bez brojeva + broj naloga.
- Štampa direktno iz aplikacije; PDF kao pravi tekst, ne slika.

## Uvoz podataka

- Iz probne verzije: klijenti s jedinicama, uređaji, tipovi, materijal, serviseri, postavke, nalozi.
- Uvoz iz Excela/CSV za nove klijente (prepoznavanje kolona, ćirilica → latinica, dopuna bez dupliranja).
- Iz SDP: lokacije za lakše podešavanje tabele veza.
- Numeracija nastavlja od zadnjeg broja iz probne verzije.

## Otvorena pitanja

- [ ] Kako su klijenti vođeni u SDP: Site, Department ili po domenu e-maila?
- [ ] U koje polje klijenti upisuju svoj broj tiketa; može li se dodati posebno polje?
- [ ] Da li Standard build 15200 ima okidače s webhookom?
- [ ] Nalog za svaki tiket ili samo za određene kategorije/statuse?
- [ ] Treba li aplikacija sama zatvarati tiket?
- [ ] Ko održava server i rezervne kopije?

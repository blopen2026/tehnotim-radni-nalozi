# Korak 10b – Magacin rezervnih dijelova

Magacin se vodi **samo količinski** (bez cijena). Jedan magacin. Upravlja samo administrator.

## Šta je novo

- **Magacin** u meniju (samo administrator): artikli s nazivom, PN i stanjem.
  - Artikal se vodi **po količini** (npr. toner, kabl) ili **po serijskom broju (SN)** – tada je svaki komad zaseban.
  - **Prijem robe**: količina ili lista serijskih brojeva (svaki u svom redu) + otpremnica/dobavljač.
  - **Korekcija stanja** (inventura) – samo za artikle po količini, razlog je obavezan.
  - **Otpis komada** (SN) uz razlog.
  - **Kartica artikla**: svako kretanje (prijem, utrošak, povrat iz naloga, korekcija, otpis) s nalogom i korisnikom.
  - Sažetak: artikli u minusu, ispod minimuma, stavke „utrošeno van magacina”.
- **Nalog → Utrošeno**: kolone Materijal, PN, SN, Količina, Broj otpremnice.
  - Ispod svakog reda piše stanje u magacinu; za artikle po SN nude se slobodni serijski brojevi.
  - Stanje se mijenja **pri svakom čuvanju naloga**, knjiži se samo razlika (ponovno čuvanje ne skida dvaput; obrisan red vraća robu na stanje).
  - Stanje **može ići u minus** – nalog se sačuva, ali se ispod dugmeta prikaže upozorenje.
  - Materijal kojeg **nema u magacinu** (ili SN koji nije primljen) se sačuva i pojavi u listi **„Utrošeno van magacina”** na stranici Magacin. Administrator ga poveže s artiklom (tada se skida sa stanja) ili označi riješenim uz napomenu.
  - Brisanje naloga vraća materijal na stanje.
- **PDF** naloga ima kolone PN i SN u tabeli utrošenog materijala.
- Šifarnici: lista materijala je premještena u Magacin. Postojeći materijal postaje artikal sa stanjem 0.

## Pokretanje

1. `git pull`
2. Zaustavite aplikaciju (Ctrl+C) ako radi, pa `pokreni.cmd`. Pri pokretanju se izvrši skripta `005_magacin.sql`.

## Provjera

1. Kao administrator otvorite **Magacin** → „+ Novi artikal”: npr. *Toner HP 59A*, PN *CF259A*, po količini, minimum 2. Primite 3 kom.
2. Napravite artikal *Fuser HP M404* po SN i primite dva serijska broja.
3. Novi nalog → Utrošeno: izaberite toner (piše „Na stanju: 3”), količina 2; dodajte red s fuserom i izaberite SN. Sačuvajte.
4. Magacin: toner ima 1 (žuto – ispod minimuma), fuser ima 1, komad je „utrošen” s linkom na nalog. Kartica pokazuje utrošak.
5. Ponovo sačuvajte isti nalog bez izmjena → stanje se ne mijenja.
6. U nalogu promijenite količinu tonera na 4 i sačuvajte → upozorenje „u minusu”, stanje −2 (crveno).
7. U nalog upišite materijal kojeg nema u magacinu (npr. „Kabl USB 3m”) → poruka da je evidentirano posebno; na stranici Magacin se pojavi u „Utrošeno van magacina”. Probajte „Poveži” i „Riješeno”.
8. Obrišite red s fuserom iz naloga i sačuvajte → komad se vraća „na stanju”.
9. Štampa / PDF: tabela utrošenog materijala ima PN i SN.
10. Kao serviser: meni nema Magacin, ali u nalogu se vidi stanje i nude se SN.

Javite šta ne radi (poruku greške iz prozora gdje radi `pokreni.cmd`).

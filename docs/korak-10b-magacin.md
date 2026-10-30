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

## Tehnički opis (za buduće izmjene)

### Baza – skripta `Baza/Skripte/005_magacin.sql`

| Tabela | Šta čuva |
|---|---|
| `Materijal` (proširena) | artikal: `Naziv`, `PN`, `VodiSeSN` (0 = po količini, 1 = po SN), `Stanje`, `MinKolicina`, `Aktivan` |
| `MagacinKomad` (nova) | jedan komad artikla po SN: `SerijskiBroj`, `Status` (`na_stanju` / `utrosen` / `otpisan`), `NalogId` u kojem je utrošen; SN je jedinstven unutar artikla |
| `MagacinPromjena` (nova) | kartica artikla: svako kretanje, `Vrsta` (`prijem` / `utrosak` / `povrat` / `korekcija` / `otpis`), `Kolicina` (+ ulaz, − izlaz), `StanjePoslije`, nalog (Id i broj – broj ostaje i kad se nalog obriše), SN, dokument, napomena, korisnik |
| `NalogMaterijal` (proširena) | stavka naloga: `MaterijalId` (NULL = van magacina), `KomadId`, `PN`, `SerijskiBroj`, `VanMagacinaRijeseno`, `VanMagacinaNapomena` |

Stanje u `Materijal.Stanje` se mijenja samo kroz `Knjizenje.PromijeniStanjeAsync`, koja uvijek upiše i red u `MagacinPromjena` – kartica i stanje se uvijek slažu.

### Kod

- `Magacin/Knjizenje.cs` – sva logika knjiženja iz naloga:
  - `StareStavkeAsync` – stavke naloga prije izmjene;
  - `PripremiAsync` – povezuje unesene redove s artiklom (po `MaterijalId` ili tačnom nazivu) i komadom (po SN). Greška (nalog se ne čuva) samo ako: artikal po SN nema upisan SN, SN je utrošen u drugom nalogu ili otpisan, isti SN je dvaput u nalogu, količina negativna. SN koji nije u magacinu → stavka van magacina;
  - `KnjiziAsync` – poredi staro i novo i knjiži **samo razliku** (količinski po artiklu; po SN komadi koji su izašli se vraćaju, novi se troše). Vraća upozorenja (minus, van magacina);
  - sve radi u istoj transakciji kao čuvanje naloga – ako nalog ne uspije, ni magacin se ne mijenja.
- `Magacin/MagacinApi.cs` – API `/api/magacin/...`:
  - `GET artikli` (svi prijavljeni – serviseri vide stanje u nalogu), `GET artikli/{id}/komadi`;
  - samo administrator: dodavanje/izmjena/brisanje artikla (vrsta SN/količina se mijenja i artikal briše samo dok nema kretanja), `prijem`, `korekcija`, `komadi/{id}/otpis`, `kartica`, `van-magacina` (+ `povezi`, `rijeseno`), `sazetak`.
- `Nalozi/NaloziApi.cs` – čuvanje naloga poziva `PripremiAsync` + `KnjiziAsync` i vraća `upozorenja`; brisanje naloga vraća sav materijal (`KnjiziAsync(stare, prazno)`).
- `wwwroot/magacin.html` – stranica magacina; `wwwroot/nalog.html` – redovi materijala (dio „Materijal (iz magacina)”), prikaz upozorenja nakon čuvanja; `wwwroot/zaglavlje.js` – meni Magacin za administratora.
- `Pdf/PdfNaloga.cs` – tabela utrošenog materijala: Materijal (kol. 1–2), PN, SN, Količina, Broj otpremnice.
- Uklonjeno: stari `/api/materijal` iz `Sifarnici/SifarniciApi.cs` i kartica Materijal sa stranice Šifarnici.

### Ideje za kasnije (nije urađeno)

- Više magacina (npr. po serviseru / vozilu) – trebalo bi dodati tabelu `Magacin` i `MagacinId` u stanje i kretanja.
- Na stranici uređaja prikaz dijelova ugrađenih u taj uređaj.
- Izvoz stanja magacina u Excel.

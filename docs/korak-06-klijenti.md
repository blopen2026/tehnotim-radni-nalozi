# Korak 6 – klijenti i poslovne jedinice

Meni **Klijenti** (kancelarija i administrator):

- **Lista klijenata** s pretragom i brojem jedinica, uređaja i naloga; dodavanje novog klijenta.
- **Stranica klijenta**: naziv, skraćenica (za ime PDF fajla), ugovor; tabela poslovnih jedinica s pretragom;
  klik na jedinicu otvara izmjenu; dodavanje i brisanje jedinica.
- **Uvoz iz Excela** (`.xlsx`) ili CSV: server čita prvi list fajla; stranica prepoznaje kolone po zaglavlju
  (ćirilica ili latinica), pretvara ćirilicu u latinicu, razdvaja više telefona u jednoj ćeliji, izvlači mjesto iz naziva
  („PJ Bijeljina” → Bijeljina) i pokazuje pregled prije uvoza.

Pravila uvoza (isto kao u prototipu):

- jedinica s istim nazivom **ili** istom adresom kod tog klijenta se **dopunjava**, ne duplira;
- naziv postojeće jedinice i njena kontakt osoba ostaju; novi telefon se dodaje uz postojeći;
- sve ili ništa: ako nešto pukne, ništa iz tog uvoza ne ostaje u bazi.

Brisanje: klijent ili jedinica koji imaju uređaje ili naloge ne mogu se obrisati.

## Provjera

```
git pull
cd src\Tehnotim.RadniNalozi
dotnet run
```

1. Meni **Klijenti** → **Uvoz iz Excela**.
2. Klijent: `Republička Geodetska Uprava`; fajl: Excel tabela organizacionih jedinica (Book1.xlsx).
3. Pregled: 54 jedinice, sve „nova”, kolone prepoznate, tekst u latinici → **Uvezi**.
4. **Otvori klijenta** → 54 jedinice; upisati skraćenicu (npr. RUGIPP) i km za par jedinica.
5. Ponoviti isti uvoz → pregled kaže „dopuna” za svih 54, poslije uvoza i dalje 54 jedinice (ništa duplo).

# Korak 3 – baza

Aplikacija pri pokretanju sama:

1. napravi bazu `RadniNalozi` ako ne postoji,
2. izvrši SQL skripte iz `src/Tehnotim.RadniNalozi/Baza/Skripte` koje još nisu izvršene (redom po imenu),
3. zapamti izvršene skripte u tabeli `SemaVerzija`.

## Pravila za skripte

- Ime: redni broj + opis, npr. `003_dodaj_polje_x.sql`.
- Izvršena skripta se **nikad ne mijenja**. Svaka izmjena baze = nova skripta.
- Svaka skripta se izvršava u jednoj transakciji: ako nešto pukne, ništa iz nje ne ostaje u bazi.

## Pokretanje

```
git pull
cd src\Tehnotim.RadniNalozi
dotnet run
```

Na http://localhost:5080 treba da piše: baza **RadniNalozi**, 13 tabela
(BrojacNaloga, Klijent, Materijal, Nalog, NalogIzmjena, NalogMaterijal, NalogUredjaj,
Postavke, PoslovnaJedinica, SemaVerzija, Serviser, TipUredjaja, Uredjaj)
i dvije izvršene skripte (001, 002).

## Pregled u SQL Server Management Studio (nije obavezno)

Server: `.\SQL2022`, Windows Authentication → Databases → RadniNalozi → Tables.

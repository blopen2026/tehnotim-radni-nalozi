# Korak 4 – prijava i uloge

- Prijava ide **domenskim (Windows) nalogom** – nema posebne lozinke za aplikaciju.
- Ko smije ući i s kojom ulogom vodi se u tabeli `Korisnik` (stranica **Korisnici**, samo za administratora).
- **Prvi korisnik** koji otvori aplikaciju dok je tabela prazna automatski postaje administrator.
  Na serveru zato aplikaciju prvi otvara administrator, odmah poslije instalacije.
- Korisnik koji nije upisan (ili je isključen) vidi poruku „Nemate pristup” i svoj nalog.
- Uvijek mora ostati bar jedan aktivan administrator.

| Uloga | Smije |
|---|---|
| Serviser | praviti i uređivati naloge |
| Kancelarija | sve kao serviser + klijenti, uređaji, uvoz |
| Administrator | sve + korisnici i postavke |

## Pokretanje

```
git pull
cd src\Tehnotim.RadniNalozi
dotnet run
```

Otvoriti http://localhost:5080 u **Edge ili Chrome** (prijava prolazi sama; Firefox traži ime i lozinku).

Treba da piše: prijavljeni ste kao `DOMEN\vaš.nalog`, uloga **Administrator**; gore meni **Korisnici**;
u stanju sistema 14 tabela (nova: `Korisnik`) i tri skripte (001, 002, 003).

Na stranici Korisnici upisati svoje ime i dodati jednog probnog korisnika.

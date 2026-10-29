# Korak 5 – šifarnici

Stranica **Šifarnici** (vide je kancelarija i administrator):

| Dio | Ko mijenja | Napomena |
|---|---|---|
| Tipovi uređaja | kancelarija, administrator | kvačica „brojač” = upisuje se broj odštampanih strana |
| Materijal i dijelovi | kancelarija, administrator | novi materijal iz naloga dodavaće se sam (korak 8) |
| Nosioci naloga | – (samo pregled) | aktivni korisnici s kvačicom „Može biti nosilac naloga” |
| Numeracija naloga | administrator | sljedeći broj u tekućoj godini; ne može ispod već iskorištenih |
| Podaci firme i tarifa | administrator | štampaju se na nalogu; tarifa KM/km za putne troškove |

Stavka koja se već koristi u uređaju ili nalogu ne može se obrisati – baza to odbija, aplikacija javlja razlog.

Promjena baze: skripta `004_nosilac_je_korisnik.sql` – tabela `Serviser` je ukinuta,
nosilac naloga je korisnik aplikacije (`Korisnik.NosilacNaloga`, `Nalog.NosilacKorisnikId`).

## Provjera

```
git pull
cd src\Tehnotim.RadniNalozi
dotnet run
```

1. Početna: 13 tabela (nema više `Serviser`), četiri skripte (001–004).
2. Meni **Šifarnici**: 7 tipova uređaja; dodati „Ploter” s brojačem, preimenovati, obrisati.
3. Dodati jedan materijal; probati duplikat istog naziva (treba odbiti).
4. Numeracija: piše `0001/2026`; postaviti npr. 25 → piše `0025/2026`; vratiti na 1.
5. Upisati tarifu po km i sačuvati.
6. Korisnici: kvačica „Može biti nosilac naloga”; isključiti je nekome → nestaje sa liste Nosioci naloga.

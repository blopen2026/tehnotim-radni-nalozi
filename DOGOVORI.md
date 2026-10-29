# Dogovori i odluke

Ovaj fajl se čita na početku svakog rada na projektu. Novi dogovori se dopisuju na vrh, s datumom.

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

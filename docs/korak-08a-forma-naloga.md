# Korak 8a – forma naloga i čuvanje

Meni **+ Novi nalog** (sve uloge). Forma prati prototip i papirni obrazac.

Pri čuvanju (sve u jednoj transakciji):

- broj naloga se dodjeljuje pri prvom čuvanju (`0001/2026`), u godini datuma izdavanja; dva korisnika ne mogu dobiti isti broj;
- klijent mora postojati u bazi (bira se sa liste); **nova poslovna jedinica** upisana u nalog dodaje se klijentu;
- **uređaji**: poznati serijski broj se dopunjava (model, inventurni broj, tip), nepoznat se dodaje klijentu i jedinici;
- **brojač uređaja** se ažurira samo iz naloga s datumom jednakim ili novijim od zadnjeg očitanja;
- **materijal** koji nije u šifarniku dodaje se sam;
- nalog čuva kopiju podataka (naziv klijenta, lokacija, adresa, kontakt, nosilac) kakvi su bili u tom trenutku;
- faza: „U radu”, ili „Čeka potpis” ako je označeno da korisnik potpisuje kasnije (potpisi – korak 9);
- bilježi se ko je i kad kreirao ili izmijenio nalog (`NalogIzmjena`).

„Prethodno stanje” brojača uzima se iz zadnjeg **drugog** naloga za taj uređaj; za novi nalog, ako nema naloga, iz ručno upisanog stanja uređaja.

Na Početnoj: posljednjih 15 naloga (puna lista s pretragom – korak 8b).

## Provjera

```
git pull
pokreni.cmd
```

1. **+ Novi nalog**: klijent Geodetska uprava → jedinica sa liste → popune se adresa, kontakt, km; status ugovora DA.
2. Uređaj: upisati serijski broj uređaja iz koraka 7 → popune se model, tip, inventurni; prikaže se prethodno stanje brojača;
   upisati novo stanje → razlika.
3. Drugi uređaj s **novim** serijskim brojem, tip Štampač, brojač.
4. Materijal: jedna stavka koja nije u šifarniku.
5. Interni i eksterni broj, opis, status, intervencija → **Sačuvaj** → dobija broj 0001/2026 (ili sljedeći prema numeraciji).
6. Provjeriti: Uređaji – novi uređaj postoji, zadnji brojač ažuriran; Šifarnici – novi materijal; Početna – nalog u listi.
7. Otvoriti nalog s Početne, izmijeniti nešto, sačuvati → broj ostaje isti; „zadnja izmjena: …”.
8. Upisati novu jedinicu u nalog (koje nema u listi) → poslije čuvanja postoji kod klijenta.

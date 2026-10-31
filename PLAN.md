# Plan izrade

Radi se u malim koracima; svaki korak završava nečim što se može probati.
Kad je korak gotov, označi se `[x]` i upiše datum.

## Faza 1 – Osnova (serviseri počinju raditi u novoj aplikaciji)

- [x] 1. (2026-10-29) Postavljanje računara za razvoj (.NET SDK, SQL Server Express, Git) – vidi `docs/postavljanje-racunara.md`
- [x] 2. (2026-10-29) Prazan ASP.NET Core projekat koji se pokreće lokalno – uputstvo: `docs/korak-02-pokretanje.md`
- [x] 3. (2026-10-29) Baza: tabele iz specifikacije (klijenti, poslovne jedinice, uređaji, tipovi, materijal, serviseri, nalozi, postavke) – uputstvo: `docs/korak-03-baza.md`
- [x] 4. (2026-10-29) Prijava korisnika (Windows/AD) i uloge: serviser, kancelarija, administrator – uputstvo: `docs/korak-04-prijava.md`
- [x] 5. (2026-10-29) Šifarnici: tipovi uređaja, materijal, nosioci naloga, tarifa i podaci firme, numeracija – uputstvo: `docs/korak-05-sifarnici.md`
- [x] 6. (2026-10-29) Klijenti i poslovne jedinice, uvoz iz Excela – uputstvo: `docs/korak-06-klijenti.md`
- [x] 7. (2026-10-29) Uređaji – uputstvo: `docs/korak-07-uredjaji.md`
- [x] 8a. (2026-10-29) Forma naloga (prema prototipu) i čuvanje – uputstvo: `docs/korak-08a-forma-naloga.md`
- [x] 8b. (2026-10-30) Lista naloga, pretraga, filter po fazama, istorija po uređaju, istorija izmjena, brisanje – uputstvo: `docs/korak-08b-lista-naloga.md`
- [x] 9. (2026-10-30) Potpisi na ekranu – uputstvo: `docs/korak-09-potpisi.md`
- [x] 10. PDF prema prototipu i direktna štampa – uputstvo: `docs/korak-10-pdf-stampa.md` (2026-10-30)
- [x] 10b. Magacin rezervnih dijelova – uputstvo: `docs/korak-10b-magacin.md` (2026-10-30)
- [x] 11. Prenos podataka iz probne verzije – nije potreban: probna verzija je imala samo jednog klijenta (već uvezen iz Excela) i jedan probni nalog (2026-10-30)
- [x] 12a. (2026-10-31) Testni server: virtuelna mašina (Hyper-V) s Windows Server 2022 – uputstvo: `docs/korak-12a-testni-server.md`
- [x] 12b. (2026-10-31) Testni server: SQL Server Express, IIS i aplikacija Radni nalozi – uputstvo: `docs/korak-12b-aplikacija-na-testnom-serveru.md`
- [x] 12c. (2026-10-31) Testni server: kopija ServiceDesk Plus 15200 (isključen mail) – uputstvo: `docs/korak-12c-sdp-testna-kopija.md`
- [ ] 12d. Instalacija na produkcioni server (IIS), HTTPS, rezervne kopije

## Faza 2 – Veza sa ServiceDesk Plus

- [x] 13. (2026-10-31) API ključ (poseban tehničar), test poziva sa računara u mreži – uputstvo: `docs/korak-13-sdp-api.md`
- [x] 14a. (2026-10-31) Veza sa SDP-om u Šifarnicima, sektori servisera (SDP nalog/grupa), sektori kod korisnika – uputstvo: `docs/korak-14a-sdp-sektori.md`
- [ ] 14b. Tabele veza: SDP site → klijent, requester → poslovna jedinica (RGU: iz Redmine teksta) – uputstvo: `docs/korak-15-nalog-iz-tiketa.md`
- [ ] 15. Stranica „Iz tiketa” (otvoreni tiketi bez naloga) i forma naloga popunjena iz tiketa; nosioci po sektoru – uputstvo: `docs/korak-15-nalog-iz-tiketa.md`
- [ ] 16. Povrat u tiket: napomena s brojem naloga + PDF – uputstvo: `docs/korak-16-povrat-u-tiket.md`

## Faza 3 – Dodaci po potrebi

- [ ] Učitavanje ručno popunjenog naloga sa skena
- [ ] Ovlaštene osobe klijenta / portal za potvrdu naloga
- [ ] Izvještaji za fakturisanje

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
- [x] 14b. (2026-10-31) Tabele veza: SDP site → klijent, requester → poslovna jedinica (RGU: iz Redmine teksta) – uputstvo: `docs/korak-15-nalog-iz-tiketa.md`
- [x] 15. (2026-10-31) Stranica „Iz tiketa” (otvoreni tiketi bez naloga) i forma naloga popunjena iz tiketa; nosioci po sektoru – uputstvo: `docs/korak-15-nalog-iz-tiketa.md`
- [x] 16. (2026-10-31) Povrat u tiket: napomena s brojem naloga + PDF – uputstvo: `docs/korak-16-povrat-u-tiket.md`

## Faza 3 – Dodaci po potrebi

Urađeno (čeka probu na testnom serveru):

- [x] (2026-10-31) Nova početna stranica prema ulozi (serviser: samo svoji nalozi)
- [ ] Izvještaji radnih naloga (filteri, grupisanje, Excel, štampa, PDF svih naloga) – opis: `docs/izvjestaji.md`

Ideje za poslije testne faze (redoslijed se dogovara; prijedlog: prvo 1, 2, 7):

**Rad servisera**
- [ ] 1. Rad sa telefona na terenu: forma prilagođena telefonu, „instalacija” na početni ekran (PWA), rad bez interneta (nalog se pošalje kad se veza vrati)
- [ ] 2. Potpis klijenta na daljinu: link na mail za naloge „čeka potpis” → klijent pregleda i potpiše → nalog završen, PDF u tiket
- [ ] 3. Skeniranje serijskog broja (bar-kod/QR) kamerom telefona – u nalog i magacin

**Uređaji i ugovori o održavanju**
- [ ] 4. QR naljepnica na uređaju (štampa iz aplikacije) → skeniranjem istorija uređaja / novi nalog
- [ ] 5. Plan preventivnog održavanja za uređaje pod ugovorom: podsjetnik i nacrt naloga kad dođe vrijeme
- [ ] 6. Izvještaj o brojačima štampača (odštampane strane po uređaju i periodu) – za naplatu po strani

**Podsjetnici i automatika**
- [ ] 7. Obavještenja mailom: nalog čeka potpis > X dana, tiket bez naloga > Y dana, artikal ispod minimuma
- [ ] 8. Automatski nacrti naloga iz SDP-a (kad se tiket dodijeli sektoru)

**Magacin i fakturisanje**
- [ ] 9. Više magacina (glavni + „magacin u vozilu” po serviseru), prenos robe između magacina
- [ ] 10. Izvoz za knjigovodstvo (format koji učitava knjigovodstveni program)

**Sigurnost i ostalo**
- [ ] 11. Uloge preko AD grupa (npr. grupa „Serviseri” → uloga serviser)
- [ ] 12. Učitavanje ručno popunjenog papirnog naloga sa skena (OCR)
- [ ] 13. Ovlaštene osobe klijenta / portal za potvrdu naloga

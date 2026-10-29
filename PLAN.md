# Plan izrade

Radi se u malim koracima; svaki korak završava nečim što se može probati.
Kad je korak gotov, označi se `[x]` i upiše datum.

## Faza 1 – Osnova (serviseri počinju raditi u novoj aplikaciji)

- [ ] 1. Postavljanje računara za razvoj (.NET SDK, SQL Server Express, Git) – vidi `docs/postavljanje-racunara.md`
- [ ] 2. Prazan ASP.NET Core projekat koji se pokreće lokalno
- [ ] 3. Baza: tabele iz specifikacije (klijenti, poslovne jedinice, uređaji, tipovi, materijal, serviseri, nalozi, postavke)
- [ ] 4. Prijava korisnika (Windows/AD) i uloge: serviser, kancelarija, administrator
- [ ] 5. Šifarnici: serviseri, tipovi uređaja, materijal, tarifa, numeracija
- [ ] 6. Klijenti i poslovne jedinice, uvoz iz Excela
- [ ] 7. Uređaji
- [ ] 8. Forma naloga (prema prototipu), čuvanje, lista naloga, pretraga, faze
- [ ] 9. Potpisi na ekranu
- [ ] 10. PDF prema prototipu i direktna štampa
- [ ] 11. Prenos podataka iz probne verzije
- [ ] 12. Instalacija na Windows server (IIS), HTTPS, rezervne kopije

## Faza 2 – Veza sa ServiceDesk Plus

- [ ] 13. API ključ (poseban tehničar), test poziva sa računara u mreži
- [ ] 14. Tabela veza: SDP site/department → klijent + poslovna jedinica
- [ ] 15. Čitanje novih tiketa i nacrti naloga („Novo iz tiketa”)
- [ ] 16. Povrat u tiket: napomena s brojem naloga + PDF

## Faza 3 – Dodaci po potrebi

- [ ] Učitavanje ručno popunjenog naloga sa skena
- [ ] Ovlaštene osobe klijenta / portal za potvrdu naloga
- [ ] Izvještaji za fakturisanje

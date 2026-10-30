# Korak 9 – potpisi na ekranu

- U nalogu, dio **Ovjera (potpis)**: dva polja za potpis – prstom (tablet, telefon), olovkom ili mišem.
- **Izvođač (serviser)**: ime se povlači iz nosioca naloga; potpis nije obavezan.
- **Korisnik usluge**: kad je potpis sačuvan, nalog prelazi u fazu **Završen**; oznaka „potpisuje kasnije” se sama skida.
- „Korisnik potpisuje kasnije” bez potpisa → faza **Čeka potpis**. Poslije se nalog otvori, korisnik potpiše, sačuva → **Završen**.
- „Obriši” briše potpis; brisanjem potpisa korisnika i čuvanjem nalog se vraća u „U radu” ili „Čeka potpis”.
- Potpisi se čuvaju u bazi kao PNG slike (do 500 KB), uz nalog.
- U istoriju izmjena se upisuje „Korisnik usluge potpisao nalog” i „Potpis korisnika uklonjen”.

## Provjera

```
git pull
pokreni.cmd
```

1. Otvoriti probni nalog → dno: dva polja za potpis; ime servisera = nosilac naloga.
2. Označiti „Korisnik potpisuje kasnije” → Sačuvaj → faza **Čeka potpis** (i u listi Nalozi).
3. Potpisati mišem u polje korisnika → kvačica „kasnije” se skine → Sačuvaj → faza **Završen**;
   istorija: „Korisnik usluge potpisao nalog”.
4. Zatvoriti i ponovo otvoriti nalog → potpisi su tu.
5. Obriši potpis korisnika → Sačuvaj → faza se vraća; istorija: „Potpis korisnika uklonjen”.
6. Ako postoji tablet ili telefon u istoj mreži – probati potpis prstom (adresa računara umjesto localhost; detalji kod instalacije na server).

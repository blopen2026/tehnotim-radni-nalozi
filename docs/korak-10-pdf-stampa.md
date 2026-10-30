# Korak 10 – PDF i štampa

- PDF se pravi na serveru (biblioteka **PDFsharp**, MIT licenca, bez interneta), kao pravi tekst (Arial iz Windowsa).
- Izgled prema prototipu: logo gore lijevo, podaci firme desno; gornji dio tabele do Garancije s desnom stranom
  podijeljenom napola (Interni / Eksterni iste širine); donji dio s Kolor, C/B, Ukupno iste širine; red sa zbirom
  kad su na nalogu dva ili više uređaja s brojačem; potpisi u ovjeri; JIB, MB i žiro-račun u podnožju (iz Šifarnika).
- Polja za opis rastu s tekstom; ako nalog ne stane na jednu stranu, nastavlja se na drugoj („nastavak”).
- Ime fajla: skraćenica klijenta (ili naziv) + interni + eksterni broj, npr. `RUGIPP_1298_39422.pdf`;
  bez brojeva zahtjeva: `RUGIPP_RN-0001-2026.pdf`.
- U nalogu: **Štampaj** (otvara dijalog za štampu preglednika, bez preuzimanja) i **Sačuvaj PDF** (preuzimanje fajla).
  Ako ima nesačuvanih izmjena, nalog se prvo sačuva.
- Adresa za PDF: `/api/nalozi/{id}/pdf` (u fazi 2 isti PDF ide kao prilog u tiket).
- Logo: `wwwroot/img/logo.png` (koristi ga i zaglavlje aplikacije).

## Provjera

```
git pull
pokreni.cmd
```

Prvo pokretanje preuzima paket PDFsharp.

1. Otvoriti nalog s dva uređaja, materijalom i potpisima → **Sačuvaj PDF** → ime fajla po pravilu.
2. Uporediti PDF s probnom verzijom: logo, poravnanje, DA/NE, statusi, potpisi, podnožje.
3. **Štampaj** → otvara se dijalog za štampu sa nalogom.
4. Nalog s vrlo dugim opisom intervencije → polje se produži; po potrebi druga strana.
5. Izmijeniti nešto u nalogu i odmah kliknuti **Štampaj** → nalog se prvo sačuva.

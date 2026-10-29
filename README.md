# Tehnotim – Radni nalozi

Interna web aplikacija Tehnotim d.o.o. za popunjavanje radnih naloga, sa bazom klijenata,
poslovnih jedinica i uređaja, potpisom na ekranu, PDF-om u izgledu papirnog obrasca i vezom
sa ServiceDesk Plus.

## Tehnologija

| Dio | Izbor |
|---|---|
| Server | Windows Server, IIS |
| Aplikacija | ASP.NET Core (C#) |
| Baza | SQL Server Express |
| Frontend | HTML + JavaScript (preuzeto iz probne verzije) |
| PDF | HTML šablon naloga → PDF na serveru |
| Prijava | Windows / Active Directory nalozi |
| Veza | ServiceDesk Plus 15200, REST API v3 |

## Struktura repozitorija

- `docs/specifikacija.md` – šta aplikacija radi (polja, pravila, veza sa SDP)
- `PLAN.md` – koraci izrade i šta je urađeno
- `DOGOVORI.md` – odluke donesene usput (čitati na početku svakog rada)
- `docs/postavljanje-racunara.md` – šta instalirati za razvoj i testiranje
- `prototip/` – probna verzija iz Claude-a, referenca za izgled i ponašanje

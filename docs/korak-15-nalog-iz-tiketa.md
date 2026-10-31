# Korak 14b + 15 – Nalog iz tiketa

## Šta je novo

- Meni **Iz tiketa**: otvoreni tiketi iz SDP-a (zadnjih 300) za koje još nema naloga (veza po internom broju).
  - Serviser koji ima sektor po pravilu vidi tikete **svog sektora** („Moj sektor”), jednim klikom **Svi tiketi**.
  - Desno se može upisati broj bilo kojeg tiketa → **Otvori**.
- **Napravi nalog** otvara formu popunjenu iz tiketa:
  - interni broj = broj tiketa, eksterni (RGU: broj iza `#` u naslovu), vrijeme zahtjeva = kad je tiket otvoren;
  - opis zahtjeva = tekst maila bez HTML-a, ćirilica → latinica (RGU: samo tekst zahtjeva iz Redmine maila);
  - inventurni broj iz teksta („inv.broj: 15252”) u prvi red uređaja, „Uređaj predao” = ime ispod pozdrava;
  - **klijent**: zapamćena veza SDP site → klijent, inače **prijedlog** po nazivu;
  - **poslovna jedinica**: zapamćena veza requester → jedinica, inače prijedlog po nazivu requestera (RGU: iz „Аутор: … PJ”);
  - **nosilac**: nude se samo serviseri sektora kojem je tiket dodijeljen (tehničar, pa grupa), uz „— prikaži sve nosioce —”.
  - Gore, u dijelu **Iz tiketa**, piše šta je predloženo i treba provjeriti.
- Pri **čuvanju** se pamti: SDP site → klijent i requester → jedinica (kod RGU samo site, jer je requester zajednički).
  Nalog dobija i sektor i vezu na tiket (SdpZahtjevId).
- **Šifarnici → Zapamćene veze sa ServiceDesk Plus-om** (kancelarija/administrator): pregled i brisanje pogrešnih veza.
- Baza: skripta `007_sdp_veze.sql`.

## Postavljanje na testni server

Laptop: `git pull` → `objavi.cmd` → `Copy-VMFile`. Server: `Stop-WebAppPool RadniNalozi` → raspakovati → `Start-WebAppPool RadniNalozi`.

## Provjera

1. **Iz tiketa** → lista otvorenih tiketa. Probati „Moj sektor” / „Svi tiketi” (kao serviser iz nekog sektora).
2. Tiket **1298** (RGU, Kalinovik): **Napravi nalog** →
   - interni 1298, eksterni 39422, vrijeme 28.10.2026. 09:05;
   - klijent: prvi put vjerovatno nije prepoznat (SDP site „RUGIPP”, klijent „Republička Geodetska Uprava”) → izaberite ga;
   - jedinica: **PJ Kalinovik** (iz teksta „Kalinovik PJ”) – ako je klijent izabran ručno, jedinicu izaberite;
   - opis: „Potrebna popravka kompjutera, inv.broj:15252…”, inventurni broj 15252, uređaj predao: Nikola Čorlija;
   - nosilac: samo serviseri sektora Tehnička.
   Sačuvajte → tiket 1298 nestaje sa liste „Iz tiketa”.
3. Drugi tiket RGU-a → klijent se sada prepozna sam (zapamćen site RUGIPP).
4. Tiket nekog drugog klijenta s requesterom po jedinici (npr. Fond PIO Trebinje): provjeriti prijedlog jedinice,
   sačuvati, pa otvoriti drugi tiket istog requestera → jedinica zapamćena.
5. **Šifarnici → Zapamćene veze**: vide se upisane veze; probati brisanje.

Javite šta je pogrešno prepoznato (pošaljite broj tiketa), da doradimo pravila.

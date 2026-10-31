# Korak 14a – Veza sa ServiceDesk Plus-om i sektori

## Šta je novo

- **Šifarnici → Veza sa ServiceDesk Plus** (samo administrator): adresa SDP-a, API ključ (nikad se ne prikazuje nazad),
  kvačica „Ne provjeravaj certifikat” (samo za testni server), dugme **Provjeri vezu**.
- **Šifarnici → Sektori**: Tehnička, Printing, Sistem, Mreže (već upisani). Svakom sektoru administrator izabere
  **SDP nalog (tehničar)** i/ili **SDP grupu** – liste se povlače iz SDP-a.
- **Korisnici**: kvačice **Sektori** – jedan korisnik može biti u više sektora.
- Baza: skripta `006_sdp_sektori.sql` (tabele Sektor, KorisnikSektor; kolone za SDP u Postavke; Nalog.SektorId za kasnije).

## Gdje testirati

Na **testnom serveru** (aplikacija i SDP kopija su na istoj mašini):

1. Laptop: `git pull` → `objavi.cmd` → `Copy-VMFile` paketa na server (vidi 12b, „Nova verzija aplikacije”).
2. Server: `Stop-WebAppPool RadniNalozi` → raspakovati → `Start-WebAppPool RadniNalozi`.

## Provjera

1. `http://localhost` (na serveru) kao administrator → **Šifarnici** → **Veza sa ServiceDesk Plus**:
   - Adresa: `https://localhost` (ili `https://TEST-NALOZI`)
   - API ključ: ključ tehničara „Radni nalozi (API)”
   - kvačica **Ne provjeravaj certifikat** (testni SDP ima samopotpisan certifikat)
   - **Sačuvaj** → **Provjeri vezu** → mora pisati „Veza radi. Zadnji zahtjev: …”.
2. Kartica **Sektori**: u padajućim listama se pojave SDP tehničari (Printing TehnoTim, Sistem TehnoTim, …) i grupe.
   Za svaki sektor izaberite nalog i grupu → **Sačuvaj**.
3. **Korisnici**: otvorite servisera → označite sektor(e) → Sačuvaj. U tabeli se vidi kolona Sektori.

Ako „Provjeri vezu” javi grešku, poruka kaže šta nije u redu (ključ, certifikat, adresa).

# Korak 16 – Povratak u tiket (napomena + PDF)

Važi samo za naloge napravljene **iz tiketa** (stranica „Iz tiketa”).

## Šta radi

- Kad se nalog iz tiketa **prvi put sačuva** → u tiket se upiše napomena:
  „Otvoren radni nalog 0012/2026 · Nosilac · Klijent – jedinica · Napravio”.
- Kad nalog postane **završen** (potpis korisnika) → PDF naloga se priloži tiketu i upiše se napomena:
  status, intervencija, utrošeno vrijeme, opis izvršene intervencije, ime PDF fajla.
- Napomene vide **samo tehničari** (ne šalju se requesteru/klijentu). **Status tiketa se ne mijenja** – zatvara se ručno u SDP-u.
- Svaka napomena se šalje **jednom** (aplikacija pamti da je poslana).
- Ako upis ne uspije (SDP nedostupan i sl.), **nalog je ipak sačuvan**: greška se prikaže ispod dugmeta i upiše u
  istoriju naloga, a u formi se pojavi dugme **Pošalji u tiket** za ponovni pokušaj.
- Baza: skripta `008_sdp_napomene.sql`.

## Provjera (testni server)

Postaviti novu verziju (laptop: `git pull` → `objavi.cmd` → `Copy-VMFile`; server: zaustaviti pool → raspakovati → pokrenuti pool).

1. **Iz tiketa** → izaberite neki otvoreni tiket → **Napravi nalog** → izaberite nosioca → **Sačuvaj**.
   Ispod dugmeta zeleno: „U tiket … upisana je napomena o otvaranju naloga.”
   U SDP-u (`https://localhost`) otvorite tiket → kartica **Notes/Conversations**: napomena „Otvoren radni nalog …”.
2. U istom nalogu dodajte opis intervencije, status, potpis korisnika → **Sačuvaj**.
   Zeleno: „… napomena o završetku i priložen PDF.” U SDP tiketu: nova napomena i PDF u prilozima (Attachments).
3. Ako javi da PDF nije priložen, pošaljite tačnu poruku – način priloga zavisi od verzije SDP-a i podesićemo ga.

## Pravo tehničara „Radni nalozi (API)”

Uloga tog tehničara u SDP-u mora dozvoljavati **dodavanje napomena** i **priloga** zahtjevima.

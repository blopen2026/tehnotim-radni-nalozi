# Korak 12c – Kopija ServiceDesk Plus 15200 na testnom serveru

Cilj: na `TEST-NALOZI` radi kopija produkcionog ServiceDesk Plus-a (isti build **15200**, ugrađeni PostgreSQL),
na kojoj ćemo u fazi 2 probati vezu s aplikacijom Radni nalozi – **bez ikakvog uticaja na produkciju i klijente**.

> Najvažnije pravilo: kopija **ne smije slati ni primati mail** i ne smije zvati produkcione sisteme.
> Zato se odlazni mail blokira firewallom **prije** prvog pokretanja kopije.

---

## 1. Backup produkcije (na produkcionom SDP serveru)

Najjednostavnije i bez prekida rada: **Admin → General Settings → Backup Scheduling** (ili *Data Backup*) → napraviti backup sada.
Ako ta opcija ne postoji ili ne radi: van radnog vremena zaustaviti servis *ManageEngine ServiceDesk Plus*,
pokrenuti `<SDP folder>\bin\backUpData.bat`, zatim ponovo pokrenuti servis.

Backup se nalazi u `<SDP folder>\backup\` – fajl `backup_servicedesk_15200_....data` (ili `.ezip`).
Kopirajte ga na laptop (USB / mreža), pa kao u 12b tačka 5 na server:

```powershell
Copy-VMFile -Name TEST-NALOZI -SourcePath "C:\SDP\backup_servicedesk_15200_xxx.data" `
  -DestinationPath "C:\Instalacija\" -CreateFullPath -FileSource Host -Force
```

Zapišite i: **tačan build** (Help → About, mora biti 15200) i **ediciju** (Standard).

## 2. Checkpoint

Hyper-V → checkpoint `04 prije SDP`.

## 3. Blokirati mail na testnom serveru (PRIJE instalacije)

Na serveru, PowerShell kao administrator:

```powershell
New-NetFirewallRule -DisplayName "TEST - blokiran odlazni mail" -Direction Outbound -Action Block `
  -Protocol TCP -RemotePort 25,465,587,110,995,143,993
```

Blokira slanje (SMTP) i preuzimanje (POP/IMAP) za sve programe na serveru.
(Ako produkcija koristi Microsoft 365 / Exchange Online preko OAuth-a, javite – blokira se i to.)

## 4. Instalacija SDP i nadogradnja na 15200 (na serveru) – kako je stvarno prošlo 31.10.2026.

Imali smo instalacijski fajl 15140 i paket za nadogradnju `ManageEngine_ServiceDesk_Plus_15200_SP-7.ppm`
(to je ZVANIČNI paket za 15100–15150 → 15200; SHA256 `55265970886a8789681551fc2387a96c7711546c6d568110b88de5e5ee3af861`).

Redoslijed koji radi:
1. Instalacija: Standard, ugrađeni PostgreSQL, port 8080; na kraju NE pokretati server.
2. **Prije prvog pokretanja**, administratorski Command Prompt: `cd /d "C:\Program Files\ManageEngine\ServiceDesk\bin"` → `initPgsql.bat`.
   (Bez toga prvo pokretanje padne, a poslije javlja „Previous start failed. Please, Reinitialize DB” – tada je najbrže vratiti checkpoint i ponoviti.)
3. Pokrenuti **samo servis** (ne i „Start ServiceDesk Server”), sačekati 10–15 min. Adresa je **https**://localhost:8080 (i na 8080 je HTTPS).
4. Prijava `administrator` / `administrator` → promijeniti lozinku (nadogradnja to traži).
5. Admin → General Settings → Backup Scheduling → postaviti novu backup lozinku (traži da se napravi backup – na praznoj instalaciji traje kratko).
6. Zaustaviti servis → `UpdateManager.bat`:
   - **Import Certificate**: `latest.crt` (https://updates.manageengine.com/certificates/latest.crt) **i svi `.crt` iz `mecerts.zip`** (stranica *Migration Sequence*, red „16000 and later”).
     Samo `latest.crt` nije dovoljan → „Signature does not match with any available certificates”.
   - **Install** → SP-7. Prvi put javi „Kindly re-invoke the upgrade process…” – pokrenuti UpdateManager ponovo i opet Install.
   - Nadogradnja traje dugo (kod nas ~1,5 h).

**Ne raditi:** vraćati backup druge verzije (15100) na instalaciju 15140 pa nadograđivati – nadogradnja odbije bazu
(„Unknown table name specified GlobalConfig”, „Decryption failed”). Backup se vraća samo na isti build.

## 5. Vraćanje backupa

```powershell
cd "C:\Program Files\ManageEngine\ServiceDesk\bin"   # prilagoditi stvarnoj putanji
.\restoreData.bat
```
Izabrati fajl iz `C:\Instalacija\`. Sačekati poruku da je vraćanje završeno.

## 6. Prvo pokretanje i „gašenje” svega što ide prema van

Pokrenuti servis, u pregledniku na serveru `http://localhost:8080`, prijava produkcionim admin nalogom (podaci su iz produkcije).

Odmah isključiti:
- **Admin → Mail Server Settings**: dolazni mail (*Incoming*) – Stop fetching / onemogućiti; odlazni (*Outgoing*) – prazan server ili neispravna adresa.
- **Admin → Notification Rules**: isključiti obavještenja (bar ona prema requesterima/klijentima).
- **Admin → Custom Triggers / Webhooks / Workflows**: isključiti sve što zove vanjske adrese.
- **Admin → Scheduler / Periodic tasks** (ako postoje zakazani zahtjevi) – isključiti.
- **Server URL / alias** (Admin → General / Self-Service Portal Settings): staviti `http://TEST-NALOZI:8080`, da linkovi ne vode na produkciju.

U naslovu / bannerom (Admin → Customization / Self-Service) po mogućnosti upisati **„TEST KOPIJA”**, da se ne pobrka s produkcijom.

## 7. Licenca

Backup nosi produkcionu licencu. Za testnu instalaciju ManageEngine obično daje probnu/testnu licencu –
ako SDP prijavi problem s licencom, javite se ManageEngine podršci i tražite **test/staging licencu** za build 15200.

## 8. Provjera i checkpoint

- Vide se tiketi iz produkcije, nijedan mail nije otišao (firewall pravilo ostaje uključeno trajno).
- Checkpoint `05 SDP kopija radi`.

Javite kad je gotovo – sljedeće je faza 2 (korak 13: API ključ i prvi pozivi iz aplikacije prema test SDP-u).

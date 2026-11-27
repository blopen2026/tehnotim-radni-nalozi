# Korak 12d – Aplikacija na produkcionom serveru

Server: Windows Server 2022 Standard, **u domenu**, na njemu već radi **ServiceDesk Plus** (port 443, java; PostgreSQL lokalno na 65432).
Zatečeno stanje (27.11.2026.): IIS instaliran (Default Web Site na portu 80, samo IIS početna stranica), ASP.NET Core **10.0.9**, nema SQL Servera, 8 GB RAM, diskovi C i D.

**Odluke:**
- Aplikacija radi kao poseban IIS sajt na **portu 8443 (HTTPS)** – port 443 ostaje SDP-u, SDP se ne dira.
- Iznutra: `https://nalozi.tehnotim.ba:8443` (ime dogovoriti sa sektorom mreža).
- Izvana se objavljuje **samo** `https://nalozi.tehnotim.ba/potvrda/…` (reverse proxy / firewall → server:8443).
- Prijava domenskim nalozima (`DOMENA\ime`).
- Prvo **probni rad**: veza sa SDP-om prazna (da se ništa ne upisuje u prave tikete) dok se sve ne provjeri.

> PowerShell na serveru uvijek **kao administrator**. Prije početka napraviti snimak/backup servera (dogovoriti sa sektorom koji radi backup servera).

---

## 0. Provjera prostora

```powershell
Get-PSDrive -PSProvider FileSystem | Select-Object Name, @{n='Slobodno GB';e={[math]::Round($_.Free/1GB,1)}}
Get-NetTCPConnection -State Listen -LocalPort 8443 -ErrorAction SilentlyContinue   # mora biti prazno (port slobodan)
```

Potrebno: oko 3 GB na C (SQL Express, .NET), baza i rezervne kopije na **D** (`D:\RadniNalozi\Baza`, `D:\RadniNalozi\Backup`).

## 1. ASP.NET Core 8 Hosting Bundle

Na serveru je samo .NET 10, a aplikacija je .NET 8 – instalira se i 8 (rade jedno pored drugog, SDP ih ne koristi).

1. Microsoftova stranica **Download .NET 8.0** → *ASP.NET Core Runtime* → *Windows* → **Hosting Bundle** → instalirati.
2. `iisreset` (kratko restartuje IIS; SDP ne zavisi od IIS-a).
3. Provjera:
```powershell
Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App' | Select-Object Name   # mora biti i 8.0.x
Get-WebGlobalModule -Name AspNetCoreModuleV2
```

## 2. IIS dodaci

```powershell
Install-WindowsFeature Web-Server, Web-Windows-Auth, Web-Mgmt-Console -IncludeManagementTools
```
(Ako su već instalirani, komanda samo javi *NoChangeNeeded*.)

## 3. SQL Server 2022 Express

1. **SQL Server Downloads** → *Express* → instalacija → **Custom** (da se baza stavi na D).
2. *New SQL Server stand-alone installation* → funkcija **Database Engine Services** → instanca `SQLEXPRESS` (podrazumijevano).
3. Na kartici *Data Directories* → **Data root directory**: `D:\RadniNalozi\Baza`.
4. Autentifikacija: **Windows authentication mode**; kao administratora SQL-a dodati sebe (*Add Current User*).

Provjera:
```powershell
Get-Service 'MSSQL$SQLEXPRESS'      # Running
```

## 4. Paket aplikacije na server

Na laptopu: `git pull` → `objavi.cmd` → `publish\RadniNalozi.zip`.
Na server prebaciti preko udaljene radne površine (kopiraj/zalijepi) ili dijeljenog foldera u `C:\Instalacija\RadniNalozi.zip`.

## 5. Sajt u IIS-u (port 8443)

Certifikat za `nalozi.tehnotim.ba` daje sektor mreža (vidi spisak na kraju). Uvoz `.pfx` fajla:

```powershell
$lozinkaPfx = Read-Host "Lozinka certifikata" -AsSecureString
$cert = Import-PfxCertificate -FilePath C:\Instalacija\nalozi.pfx -CertStoreLocation Cert:\LocalMachine\My -Password $lozinkaPfx
$cert.Thumbprint
```

Sajt:

```powershell
New-Item C:\inetpub\RadniNalozi -ItemType Directory -Force | Out-Null
Expand-Archive C:\Instalacija\RadniNalozi.zip -DestinationPath C:\inetpub\RadniNalozi -Force

Import-Module WebAdministration
New-WebAppPool RadniNalozi
Set-ItemProperty IIS:\AppPools\RadniNalozi -Name managedRuntimeVersion -Value ""

# HTTPS na 8443; Default Web Site se ne dira
New-Website -Name RadniNalozi -Port 8443 -Ssl -HostHeader "" -PhysicalPath C:\inetpub\RadniNalozi -ApplicationPool RadniNalozi
(Get-WebBinding -Name RadniNalozi -Protocol https).AddSslCertificate($cert.Thumbprint, "My")

# Windows prijava i anonimni pristup (prijavu traži aplikacija; bez prijave radi samo /potvrda/)
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location RadniNalozi -Filter system.webServer/security/authentication/windowsAuthentication -Name enabled -Value $true
Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Location RadniNalozi -Filter system.webServer/security/authentication/anonymousAuthentication -Name enabled -Value $true

# Firewall: 8443 iz interne mreže
New-NetFirewallRule -DisplayName "Radni nalozi HTTPS 8443" -Direction Inbound -Protocol TCP -LocalPort 8443 -Action Allow
```

> Dok certifikat ne stigne, za probu se može napraviti privremeni (preglednik će upozoravati):
> `$cert = New-SelfSignedCertificate -DnsName nalozi.tehnotim.ba, $env:COMPUTERNAME -CertStoreLocation Cert:\LocalMachine\My`

## 6. Pravo aplikacije na SQL

```powershell
sqlcmd -S .\SQLEXPRESS -E -Q "CREATE LOGIN [IIS APPPOOL\RadniNalozi] FROM WINDOWS; ALTER SERVER ROLE dbcreator ADD MEMBER [IIS APPPOOL\RadniNalozi];"
Restart-WebAppPool RadniNalozi
```

Aplikacija pri prvom pokretanju sama napravi bazu `RadniNalozi` (u `D:\RadniNalozi\Baza`) i izvrši sve skripte.

## 7. Prva prijava

1. Na svom računaru u Edge-u: `https://nalozi.tehnotim.ba:8443` (ili `https://IME-SERVERA:8443` dok DNS nije podešen).
2. **Prvi koji se prijavi postaje administrator** – prvo se prijavite svojim domenskim nalogom.
3. **Korisnici** → dodati servisere i kancelariju (`DOMENA\ime`, uloga, sektori).
4. Provjera bez prijave:
```powershell
curl.exe -k -s -o NUL -w "%{http_code}`n" https://localhost:8443/potvrda/proba   # 200
curl.exe -k -s -o NUL -w "%{http_code}`n" https://localhost:8443/                # 401
```

> Ako preglednik stalno traži korisničko ime i lozinku: adresa nije u zoni *Local intranet* (vidi spisak za sektor mreža, tačka 2).

## 8. Podaci

Baza je prazna. Ponovo se unose (iz istih fajlova kao na testu):
- Šifarnici → **Podaci firme** (kao memorandum), tarifa, tipovi uređaja, sektori, numeracija naloga (ako treba početi od određenog broja);
- **Klijenti → Uvoz iz Excela** (PURS po regijama, RGU jedinice), **Uređaji → Uvoz** (RGU);
- **Ovlaštena lica** klijenata;
- Šifarnici → **Slanje e-maila i potvrda naloga** (SMTP od sektora mreža; javna adresa `https://nalozi.tehnotim.ba`).

Veza sa SDP-om (Šifarnici → Veza sa ServiceDesk Plus) se upisuje **tek kad probni rad prođe** (vidi tačku 10).

## 9. Rezervne kopije baze (svaku noć)

SQL Express nema SQL Agent, pa backup radi Windows zakazani zadatak:

```powershell
New-Item D:\RadniNalozi\Backup -ItemType Directory -Force | Out-Null
sqlcmd -S .\SQLEXPRESS -E -Q "CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS" 2>$null
sqlcmd -S .\SQLEXPRESS -E -Q "ALTER SERVER ROLE sysadmin ADD MEMBER [NT AUTHORITY\SYSTEM]"

$skripta = @'
$dan = Get-Date -Format yyyy-MM-dd
sqlcmd -S .\SQLEXPRESS -E -b -Q "BACKUP DATABASE RadniNalozi TO DISK = N'D:\RadniNalozi\Backup\RadniNalozi_$dan.bak' WITH INIT, CHECKSUM"
Get-ChildItem D:\RadniNalozi\Backup\*.bak | Where-Object LastWriteTime -lt (Get-Date).AddDays(-30) | Remove-Item
'@
Set-Content D:\RadniNalozi\backup.ps1 $skripta

$akcija = New-ScheduledTaskAction -Execute powershell.exe -Argument "-NoProfile -ExecutionPolicy Bypass -File D:\RadniNalozi\backup.ps1"
$okidac = New-ScheduledTaskTrigger -Daily -At 23:30
Register-ScheduledTask -TaskName "RadniNalozi backup" -Action $akcija -Trigger $okidac -User SYSTEM -RunLevel Highest
Start-ScheduledTask "RadniNalozi backup"     # proba – u D:\RadniNalozi\Backup se pojavi .bak
```

> Folder `D:\RadniNalozi\Backup` treba da ulazi u redovni backup servera (sektor mreža).

## 10. Probni rad → pravi rad

1. Probni rad: nalozi se prave ručno, potvrda e-mailom se proba sa svojom adresom i sa jednim klijentom.
2. Kad je sve u redu: SDP tehničar „Radni nalozi (API)” (licenca), API ključ → Šifarnici → Veza sa SDP-om
   (adresa `https://localhost` ili ime servera, port 443), sektori servisera → SDP nalozi/grupe.
3. Probni nalozi se obrišu (Nalozi → nalog → Obriši) i, ako treba, numeracija vrati na početak.

## Nova verzija aplikacije

```powershell
Stop-WebAppPool RadniNalozi
Expand-Archive C:\Instalacija\RadniNalozi.zip -DestinationPath C:\inetpub\RadniNalozi -Force
Start-WebAppPool RadniNalozi
```
Novu verziju prvo probati na testnom serveru.

---

## Spisak za sektor mreža

1. **DNS:** `nalozi.tehnotim.ba` → IP adresa servera (interno).
2. **Prijava bez lozinke:** GPO – `https://nalozi.tehnotim.ba` u zonu *Local intranet* (Edge/Chrome tada šalju domensku prijavu automatski).
   Ako se koristi ime različito od imena servera, za Kerberos: `setspn -S HTTP/nalozi.tehnotim.ba IME-SERVERA` (bez toga radi NTLM).
3. **Certifikat** za `nalozi.tehnotim.ba` (interni CA ili javni / wildcard `*.tehnotim.ba`), kao `.pfx` sa privatnim ključem – za IIS na portu 8443.
4. **Firewall:** port **8443** prema serveru iz interne mreže.
5. **Objava prema van samo za potvrdu naloga:** `https://nalozi.tehnotim.ba/potvrda/…` (javni port 443, javni certifikat)
   → `https://SERVER:8443/potvrda/…`. Sve ostale putanje izvana **zatvorene**. Reverse proxy treba prosljeđivati i `POST` na istu putanju.
6. **Slanje e-maila:** SMTP server, port (587 STARTTLS ili 465 SSL), nalog i lozinka za adresu npr. `nalozi@tehnotim.ba`
   (ili interni relay bez lozinke); izlaz sa servera na taj port dozvoljen.
7. **Rezervne kopije:** `D:\RadniNalozi\Backup` (noćni `.bak` baze) uključiti u backup servera.

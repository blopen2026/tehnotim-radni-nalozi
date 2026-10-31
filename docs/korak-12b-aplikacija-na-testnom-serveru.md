# Korak 12b – Aplikacija Radni nalozi na testnom serveru

Na serveru `TEST-NALOZI` (Windows Server 2022, radna grupa) instaliramo:
SQL Server 2022 Express, IIS, ASP.NET Core 8 Hosting Bundle i aplikaciju.

> **Prije početka:** checkpoint `02 prije aplikacije`.
>
> Na serveru PowerShell uvijek pokrećite **kao administrator** (desni klik → *Run as administrator*).

---

## 1. SQL Server 2022 Express (na serveru)

1. Na serveru u Edge-u otvorite Microsoftovu stranicu **SQL Server Downloads** → *Express* → *Download now*.
2. Pokrenite instalaciju → **Basic** → prihvatite licencu → Install.
3. Na kraju zapišite **Instance name** – mora biti `SQLEXPRESS` (podrazumijevano).

Provjera u PowerShell-u:
```powershell
Get-Service 'MSSQL$SQLEXPRESS'
```
*Status* mora biti **Running**.

(SQL Server Management Studio nije obavezan; može se instalirati kasnije za pregled baze.)

## 2. IIS (na serveru)

```powershell
Install-WindowsFeature Web-Server, Web-Windows-Auth, Web-Mgmt-Console -IncludeManagementTools
```

Provjera: u Edge-u na serveru otvorite `http://localhost` → plava stranica *IIS*.

## 3. ASP.NET Core 8 Hosting Bundle (na serveru)

1. Microsoftova stranica **Download .NET 8.0** → kolona *ASP.NET Core Runtime* → *Windows* → **Hosting Bundle**.
2. Instalirajte, zatim:
```powershell
iisreset
```

> Hosting Bundle se instalira **poslije** IIS-a (tačka 2). Ako je instaliran prije, pokrenite njegovu instalaciju ponovo → **Repair**, pa `iisreset`.
>
> Provjera: `Get-WebGlobalModule -Name AspNetCoreModuleV2` mora vratiti red s putanjom do `aspnetcorev2.dll`.

## 4. Napraviti paket aplikacije (na laptopu)

U folderu projekta na laptopu:
```
git pull
objavi.cmd
```
Na kraju piše `Gotovo: ...\publish\RadniNalozi.zip`.

## 5. Prebaciti paket na server (na laptopu)

Bez mreže između laptopa i servera – preko Hyper-V-a. PowerShell **na laptopu**, kao administrator
(putanju projekta prilagodite):

```powershell
Enable-VMIntegrationService -VMName TEST-NALOZI -Name "Guest Service Interface"
Copy-VMFile -Name TEST-NALOZI -SourcePath "C:\Projekat radni nalozi\tehnotim-radni-nalozi\publish\RadniNalozi.zip" `
  -DestinationPath "C:\Instalacija\RadniNalozi.zip" -CreateFullPath -FileSource Host -Force
```

Na serveru se pojavi `C:\Instalacija\RadniNalozi.zip`.

## 6. Raspakovati i napraviti sajt u IIS-u (na serveru)

```powershell
# Fajlovi aplikacije
New-Item C:\inetpub\RadniNalozi -ItemType Directory -Force | Out-Null
Expand-Archive C:\Instalacija\RadniNalozi.zip -DestinationPath C:\inetpub\RadniNalozi -Force

# Pool aplikacije (.NET Core ne treba "managed code")
Import-Module WebAdministration
New-WebAppPool RadniNalozi
Set-ItemProperty IIS:\AppPools\RadniNalozi -Name managedRuntimeVersion -Value ""

# Podrazumijevani sajt se gasi, naš sajt radi na portu 80
Stop-Website "Default Web Site"
Set-ItemProperty "IIS:\Sites\Default Web Site" -Name serverAutoStart -Value $false
New-Website -Name RadniNalozi -Port 80 -PhysicalPath C:\inetpub\RadniNalozi -ApplicationPool RadniNalozi

# Prijava: Windows nalozi, bez anonimnog pristupa
Set-WebConfigurationProperty -PSPath IIS:\ -Location RadniNalozi -Filter /system.webServer/security/authentication/windowsAuthentication -Name enabled -Value $true
Set-WebConfigurationProperty -PSPath IIS:\ -Location RadniNalozi -Filter /system.webServer/security/authentication/anonymousAuthentication -Name enabled -Value $false
```

## 7. Pravo aplikacije na SQL Server (na serveru)

Aplikacija se na bazu spaja kao nalog pool-a `IIS APPPOOL\RadniNalozi` i **sama napravi bazu** `RadniNalozi` pri prvom pokretanju.

```powershell
sqlcmd -S .\SQLEXPRESS -E -Q "CREATE LOGIN [IIS APPPOOL\RadniNalozi] FROM WINDOWS; ALTER SERVER ROLE dbcreator ADD MEMBER [IIS APPPOOL\RadniNalozi];"
```

> Ako `sqlcmd` nije prepoznat: zatvorite i ponovo otvorite PowerShell (instalacija SQL-a ga dodaje u putanju).

## 8. Lokalni korisnici za probu (na serveru)

Server nije u domeni, pa se u aplikaciju prijavljuje lokalnim nalozima servera.
**Prvi koji se prijavi postaje administrator aplikacije** – zato se prvo prijavite svojim nalogom.

```powershell
$lozinka = Read-Host "Lozinka za probne naloge" -AsSecureString
New-LocalUser bojan      -Password $lozinka -FullName "Bojan Bjelajac"   -PasswordNeverExpires
New-LocalUser serviser1  -Password $lozinka -FullName "Serviser Jedan"   -PasswordNeverExpires
New-LocalUser kancelarija1 -Password $lozinka -FullName "Kancelarija Jedan" -PasswordNeverExpires
```

## 9. Prva proba (na serveru)

1. Restart pool-a: `Restart-WebAppPool RadniNalozi`
2. Edge na serveru → `http://localhost` → kad traži prijavu: `TEST-NALOZI\bojan` + lozinka.
3. Mora se otvoriti Početna, uloga **Administrator**, *Stanje sistema* zeleno (baza napravljena, skripte izvršene).

## 10. Proba sa laptopa (kroz mrežu)

1. Na serveru `ipconfig` → zapišite IPv4 adresu (npr. `192.168.1.50`).
2. Na laptopu u Edge-u: `http://192.168.1.50` → prijava `TEST-NALOZI\serviser1`.
3. Pojavi se poruka „Nemate pristup aplikaciji” – to je ispravno. Kao `bojan` (na serveru) u **Korisnici** dodajte `TEST-NALOZI\serviser1` s ulogom *serviser*, pa ponovo probajte s laptopa.

> Ako se sa laptopa stranica uopšte ne otvara: na serveru
> `Get-NetFirewallRule -DisplayName "World Wide Web Services (HTTP Traffic-In)" | Enable-NetFirewallRule`

## 11. Checkpoint

Sve radi → checkpoint `03 aplikacija radi`.

---

## Ako nešto ne radi

| Šta se vidi | Šta uraditi |
|---|---|
| *The data is invalid (0x8007000D)* kod `Set-WebConfigurationProperty`, ili *HTTP Error 500.19* | Hosting Bundle nije instaliran ili nije urađen `iisreset` (tačka 3). |
| *HTTP Error 500.30 / 500.31* | Aplikacija ne može da se pokrene – vidi ispod (dnevnik). |
| Stalno traži lozinku | Korisničko ime upisati kao `TEST-NALOZI\ime`; provjeriti tačku 6 (Windows prijava uključena). |
| *Stanje sistema* crveno / greška baze | Tačka 7 (pravo na SQL) ili SQL servis ne radi (tačka 1). |

**Dnevnik greške:** u `C:\inetpub\RadniNalozi\web.config` promijenite `stdoutLogEnabled="false"` u `"true"`,
napravite folder `C:\inetpub\RadniNalozi\logs`, osvježite stranicu i pošaljite sadržaj najnovijeg fajla iz `logs`.
Greške se vide i u **Event Viewer → Windows Logs → Application** (izvor *IIS AspNetCore Module V2*).

## Nova verzija aplikacije (kasnije, nakon izmjena)

Laptop: `git pull` → `objavi.cmd` → `Copy-VMFile` (tačka 5). Server:
```powershell
Stop-WebAppPool RadniNalozi
Expand-Archive C:\Instalacija\RadniNalozi.zip -DestinationPath C:\inetpub\RadniNalozi -Force
Start-WebAppPool RadniNalozi
```
Baza se ne dira – nove skripte se izvrše same pri pokretanju.

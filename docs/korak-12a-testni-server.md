# Korak 12a – Testni server (virtuelna mašina na razvojnom laptopu)

Cilj: virtuelni Windows Server koji je što sličniji produkcionom serveru. Na njemu će biti:
SQL Server Express (naša baza), IIS + aplikacija Radni nalozi i kopija ServiceDesk Plus 15200.

Laptop: Windows 11 Pro, 16 GB RAM → virtuelnoj mašini dajemo 8 GB.

## 1. Uključiti Hyper-V na laptopu

PowerShell **kao administrator**:

```powershell
Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All
```

Restart laptopa. Provjera: u Start meniju postoji **Hyper-V Manager**.

> Ako piše da virtualizacija nije uključena: u BIOS-u/UEFI uključiti *Intel VT-x / AMD-V (SVM)*.

## 2. Virtuelni prekidač (mreža)

Da bi server bio vidljiv drugim računarima u mreži (serviseri, domena), treba **External** prekidač:

1. Hyper-V Manager → desno **Virtual Switch Manager…**
2. **New virtual network switch → External → Create Virtual Switch**
3. Ime: `LAN`; izabrati mrežnu karticu laptopa – **po mogućnosti žičanu** (preko Wi-Fi-ja zna biti nestabilno).
4. Ostaviti kvačicu *Allow management operating system to share this network adapter* → OK.

Laptop na trenutak izgubi mrežu – to je normalno.

## 3. Windows Server 2022 – instalacijski ISO

Microsoft Evaluation Center → **Windows Server 2022** → ISO (English, 64-bit).
Probna verzija radi 180 dana, dovoljno za test.

## 4. Napraviti virtuelnu mašinu

Hyper-V Manager → **New → Virtual Machine…**

| Korak čarobnjaka | Vrijednost |
|---|---|
| Name | `TEST-NALOZI` |
| Generation | **Generation 2** |
| Memory | **8192 MB**, isključiti *Use Dynamic Memory* (SDP ne voli kad mu se memorija mijenja) |
| Networking | `LAN` |
| Virtual hard disk | 120 GB |
| Installation | *Install from bootable image file* → izabrati ISO |

Prije prvog pokretanja: desni klik na mašinu → **Settings**
- **Processor**: 4 virtuelna procesora
- **Security**: Secure Boot uključen, template *Microsoft Windows*
- **Checkpoints**: *Standard checkpoints*
- **Automatic Start Action**: *Nothing* (da ne troši RAM kad ne testirate)

## 5. Instalacija Windows Servera

1. **Connect → Start**, odmah pritisnuti tipku kad piše *Press any key to boot from CD*.
2. Izabrati **Windows Server 2022 Standard Evaluation (Desktop Experience)** – sa grafičkim okruženjem.
3. *Custom: Install Windows only* → disk → Next.
4. Lozinka lokalnog administratora (zapišite je).

Nakon instalacije, u **Server Manager → Local Server**:
- **Computer name** → `TEST-NALOZI` (restart)
- **Time zone** → (UTC+01:00) Sarajevo
- **Windows Update** → instalirati sve, restart (ponavljati dok ima ažuriranja)
- **Ethernet** → stalna IP adresa (ili rezervacija na DHCP-u) – zapišite je

## 6. Domena (preporučeno)

Aplikacija prijavljuje korisnike preko domenskih naloga, kao u produkciji.
Server Manager → Local Server → **Workgroup** → *Change… → Domain* → naziv domene → restart.
Nakon toga se prijavljujte domenskim nalogom koji je lokalni administrator servera.

## 7. Prvi snimak stanja (checkpoint)

Hyper-V Manager → desni klik na `TEST-NALOZI` → **Checkpoint**, ime: `01 cist Windows Server`.

Od sada: **prije svakog većeg koraka napravite checkpoint**. Ako nešto krene po zlu → desni klik na checkpoint → **Apply**.

## Sljedeće (12b, 12c)

- 12b: SQL Server Express + IIS + aplikacija Radni nalozi na testnom serveru
- 12c: ServiceDesk Plus 15200 – kopija produkcije (sa isključenim slanjem maila)

Javite kad je urađeno do tačke 7 (i IP adresu / naziv servera).

# Postavljanje računara za razvoj i testiranje

Računar na kojem se aplikacija pokreće i testira prije postavljanja na server.
Najbolje Windows računar u istoj mreži kao ServiceDesk Plus.

1. **Git za Windows** – https://git-scm.com/download/win (podrazumijevane opcije).
2. **.NET SDK 8 (LTS)** – https://dotnet.microsoft.com/download → .NET 8.0 → SDK x64.
   Provjera u Command Promptu: `dotnet --version` (treba početi sa 8).
3. **SQL Server 2022 Express** – https://www.microsoft.com/sql-server/sql-server-downloads → Express 2022 → instalacija **Custom** (ne Basic), kao administrator; ime instance `SQL2022`, Windows autentifikacija + Add Current User. Prije toga: `fsutil fsinfo sectorinfo C:` (vidi DOGOVORI.md).
4. **SQL Server Management Studio (SSMS)** – za pregled baze (nije obavezno, ali korisno).
5. **Visual Studio Code** – https://code.visualstudio.com (za pregled koda; nije obavezno).
6. Preuzimanje projekta:
   ```
   git clone https://github.com/blopen2026/tehnotim-radni-nalozi.git
   ```

Kad je ovo instalirano, javiti u razgovoru – sljedeći korak je pokretanje praznog projekta.

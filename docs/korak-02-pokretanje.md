# Korak 2 – pokretanje praznog projekta

1. Povucite izmjene: Command Prompt u `C:\Projekti\tehnotim-radni-nalozi`, pa `git pull`.
2. Uđite u folder aplikacije:
   ```
   cd src\Tehnotim.RadniNalozi
   ```
3. Pokrenite aplikaciju:
   ```
   dotnet run
   ```
   Prvi put traje duže (preuzima potrebne pakete). Kad piše `Now listening on: http://localhost:5080`, aplikacija radi.
4. U pregledniku otvorite **http://localhost:5080**.
5. Na stranici treba da piše: Aplikacija **radi**, Baza (SQL Server) **povezana**.
6. Zaustavljanje aplikacije: u Command Promptu pritisnite `Ctrl + C`.

## Ako baza piše „nije povezana”

Pošaljite poruku greške sa stranice u razgovor. Najčešći razlozi:

- SQL Server Express nije pokrenut: Start → „Services” → „SQL Server (SQLEXPRESS)” → Start.
- Instanca se drugačije zove: u `appsettings.json` promijeniti `Server=.\\SQLEXPRESS`.

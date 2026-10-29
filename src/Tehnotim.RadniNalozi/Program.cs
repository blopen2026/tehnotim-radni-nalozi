using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Početna stranica i ostali fajlovi iz foldera wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Provjera: radi li aplikacija i može li se spojiti na SQL Server
app.MapGet("/api/status", async (IConfiguration config) =>
{
    var connectionString = config.GetConnectionString("Baza");
    try
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT @@VERSION", conn);
        var verzija = (await cmd.ExecuteScalarAsync()) as string ?? "";

        // Kako je ova veza ostvarena: vrsta veze (Shared memory / TCP) i da li je šifrovana
        string? vrstaVeze = null, sifrovanje = null;
        try
        {
            await using var cmd2 = new SqlCommand(
                "SELECT net_transport, encrypt_option FROM sys.dm_exec_connections WHERE session_id = @@SPID", conn);
            await using var r = await cmd2.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                vrstaVeze = r.GetString(0);
                sifrovanje = r.GetString(1);
            }
        }
        catch { /* korisnik nema pravo čitanja ovog pregleda; nije bitno za rad */ }

        return Results.Ok(new
        {
            aplikacija = "radi",
            baza = "povezana",
            sqlServer = verzija.Split('\n')[0].Trim(),
            vrstaVeze,
            sifrovanje
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            aplikacija = "radi",
            baza = "nije povezana",
            greska = ex.Message
        });
    }
});

app.Run();

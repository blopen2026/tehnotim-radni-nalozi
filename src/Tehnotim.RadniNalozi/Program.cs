using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Baza;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var connectionString = app.Configuration.GetConnectionString("Baza") ?? "";

// Pri pokretanju: napravi bazu i izvrši nove SQL skripte (Baza/Skripte)
string? greskaPriPripremi = null;
try
{
    await Migracije.PrimijeniAsync(connectionString, app.Logger);
}
catch (Exception ex)
{
    greskaPriPripremi = ex.Message;
    app.Logger.LogError(ex, "Baza nije pripremljena");
}

// Početna stranica i ostali fajlovi iz foldera wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Provjera: radi li aplikacija, vidi li bazu i koje tabele i skripte postoje
app.MapGet("/api/status", async () =>
{
    try
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        string sqlServer, imeBaze;
        await using (var cmd = new SqlCommand("SELECT @@VERSION, DB_NAME()", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            await r.ReadAsync();
            sqlServer = r.GetString(0).Split('\n')[0].Trim();
            imeBaze = r.GetString(1);
        }

        var tabele = new List<string>();
        await using (var cmd = new SqlCommand("SELECT name FROM sys.tables ORDER BY name", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            while (await r.ReadAsync()) tabele.Add(r.GetString(0));
        }

        var skripte = new List<object>();
        if (tabele.Contains("SemaVerzija"))
        {
            await using var cmd = new SqlCommand("SELECT Skripta, Primijenjeno FROM dbo.SemaVerzija ORDER BY Skripta", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                skripte.Add(new { skripta = r.GetString(0), primijenjeno = r.GetDateTime(1) });
        }

        return Results.Ok(new
        {
            aplikacija = "radi",
            baza = "povezana",
            sqlServer,
            imeBaze,
            tabele,
            skripte,
            greska = greskaPriPripremi
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            aplikacija = "radi",
            baza = "nije povezana",
            greska = greskaPriPripremi ?? ex.Message
        });
    }
});

app.Run();

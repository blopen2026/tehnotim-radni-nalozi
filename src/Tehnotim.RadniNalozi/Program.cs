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
        return Results.Ok(new
        {
            aplikacija = "radi",
            baza = "povezana",
            sqlServer = verzija.Split('\n')[0].Trim()
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

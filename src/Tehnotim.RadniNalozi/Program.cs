using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Baza;
using Tehnotim.RadniNalozi.Klijenti;
using Tehnotim.RadniNalozi.Magacin;
using Tehnotim.RadniNalozi.Nalozi;
using Tehnotim.RadniNalozi.Pdf;
using Tehnotim.RadniNalozi.Prijava;
using Tehnotim.RadniNalozi.Sdp;
using Tehnotim.RadniNalozi.Sifarnici;
using Tehnotim.RadniNalozi.Uredjaji;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Baza") ?? "";

// Prijava: domenski (Windows) nalog; uloga iz tabele dbo.Korisnik
builder.Services.AddSingleton(new Korisnici(connectionString));
builder.Services.AddTransient<IClaimsTransformation, UlogeIzBaze>();
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
builder.Services.AddAuthorization(o =>
{
    // Bez Windows prijave se ne vidi ništa, ni stranice
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy(Politike.Korisnik, p => p.RequireRole(Uloge.Serviser, Uloge.Kancelarija, Uloge.Administrator));
    o.AddPolicy(Politike.Kancelarija, p => p.RequireRole(Uloge.Kancelarija, Uloge.Administrator));
    o.AddPolicy(Politike.Administrator, p => p.RequireRole(Uloge.Administrator));
});

var app = builder.Build();

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

app.UseAuthentication();
app.UseAuthorization();

// Stranice iz wwwroot – poslije prijave, da ih vide samo prijavljeni
app.UseDefaultFiles();
// Preglednik uvijek provjeri ima li novija verzija stranice/stila/skripte (inače nakon izmjene ostane stari stil)
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
});

// Ko sam ja (svako prijavljen; neupisan korisnik dobija registrovan = false)
app.MapGet("/api/ja", (ClaimsPrincipal u) =>
{
    var uloga = u.FindFirstValue(ClaimTypes.Role);
    return Results.Ok(new
    {
        nalog = u.Identity?.Name,
        ime = u.FindFirstValue(Tvrdnje.Ime),
        uloga,
        registrovan = uloga is not null
    });
});

// Korisnici (samo administrator)
var korisniciApi = app.MapGroup("/api/korisnici").RequireAuthorization(Politike.Administrator);
korisniciApi.MapGet("", async (Korisnici k) => Results.Ok(await k.SviAsync()));
korisniciApi.MapPost("", async (Korisnici k, KorisnikUnos unos) =>
{
    var greska = await k.SacuvajAsync(null, unos);
    return greska is null ? Results.Ok() : Results.BadRequest(new { greska });
});
korisniciApi.MapPut("/{id:int}", async (Korisnici k, int id, KorisnikUnos unos) =>
{
    var greska = await k.SacuvajAsync(id, unos);
    return greska is null ? Results.Ok() : Results.BadRequest(new { greska });
});

// Šifarnici: tipovi uređaja, materijal, postavke, numeracija, nosioci naloga
app.MapSifarnici(connectionString);

// Klijenti, poslovne jedinice, uvoz iz Excela/CSV
app.MapKlijenti(connectionString);
app.MapCitanjeTabele();

// Uređaji
app.MapUredjaji(connectionString);

// Radni nalozi
app.MapNalozi(connectionString);
app.MapPdf(connectionString);

// Magacin rezervnih dijelova
app.MapMagacin(connectionString);

// Veza sa ServiceDesk Plus-om i sektori servisera
app.MapSdp(connectionString);

// Stanje sistema: baza, tabele, skripte (svaki upisan korisnik)
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
}).RequireAuthorization(Politike.Korisnik);

app.Run();

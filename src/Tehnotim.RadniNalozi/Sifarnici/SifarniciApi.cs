using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Prijava;

namespace Tehnotim.RadniNalozi.Sifarnici;

public class TipUredjaja
{
    public int Id { get; set; }
    public string Naziv { get; set; } = "";
    public bool PratiBrojac { get; set; }
}

public class Materijal
{
    public int Id { get; set; }
    public string Naziv { get; set; } = "";
    public string? Sifra { get; set; }
}

public class Postavke
{
    public decimal TarifaKm { get; set; }
    public string? NazivFirme { get; set; }
    public string? Adresa { get; set; }
    public string? Email { get; set; }
    public string? Web { get; set; }
    public string? Jib { get; set; }
    public string? Mb { get; set; }
    public string? ZiroRacun { get; set; }
}

public class Nosilac
{
    public int Id { get; set; }
    public string Ime { get; set; } = "";
    public string KorisnickiNalog { get; set; } = "";
}

public record NumeracijaUnos(int Sljedeci);

/// <summary>
/// API za šifarnike: tipovi uređaja, materijal, postavke firme, numeracija naloga, nosioci naloga.
/// Čitanje: svaki korisnik. Tipovi i materijal: kancelarija. Postavke i numeracija: administrator.
/// </summary>
public static class SifarniciApi
{
    public static void MapSifarnici(this WebApplication app, string connectionString)
    {
        SqlConnection Veza() => new(connectionString);

        // ---------- Tipovi uređaja ----------
        var tipovi = app.MapGroup("/api/tipovi").RequireAuthorization(Politike.Korisnik);

        tipovi.MapGet("", async () =>
        {
            await using var c = Veza();
            return Results.Ok(await c.QueryAsync<TipUredjaja>(
                "SELECT Id, Naziv, PratiBrojac FROM dbo.TipUredjaja ORDER BY Naziv"));
        });

        tipovi.MapPost("", async (TipUredjaja t) =>
        {
            if (string.IsNullOrWhiteSpace(t.Naziv)) return Greska("Upišite naziv tipa.");
            await using var c = Veza();
            return await Izvrsi(async () => await c.ExecuteAsync(
                "INSERT INTO dbo.TipUredjaja (Naziv, PratiBrojac) VALUES (@Naziv, @PratiBrojac)",
                new { Naziv = t.Naziv.Trim(), t.PratiBrojac }));
        }).RequireAuthorization(Politike.Kancelarija);

        tipovi.MapPut("/{id:int}", async (int id, TipUredjaja t) =>
        {
            if (string.IsNullOrWhiteSpace(t.Naziv)) return Greska("Upišite naziv tipa.");
            await using var c = Veza();
            return await Izvrsi(async () => await c.ExecuteAsync(
                "UPDATE dbo.TipUredjaja SET Naziv = @Naziv, PratiBrojac = @PratiBrojac WHERE Id = @id",
                new { id, Naziv = t.Naziv.Trim(), t.PratiBrojac }));
        }).RequireAuthorization(Politike.Kancelarija);

        tipovi.MapDelete("/{id:int}", async (int id) =>
        {
            await using var c = Veza();
            return await Izvrsi(async () => await c.ExecuteAsync("DELETE FROM dbo.TipUredjaja WHERE Id = @id", new { id }));
        }).RequireAuthorization(Politike.Kancelarija);

        // ---------- Materijal ----------
        var materijal = app.MapGroup("/api/materijal").RequireAuthorization(Politike.Korisnik);

        materijal.MapGet("", async () =>
        {
            await using var c = Veza();
            return Results.Ok(await c.QueryAsync<Materijal>(
                "SELECT Id, Naziv, Sifra FROM dbo.Materijal ORDER BY Naziv"));
        });

        materijal.MapPost("", async (Materijal m) =>
        {
            if (string.IsNullOrWhiteSpace(m.Naziv)) return Greska("Upišite naziv materijala.");
            await using var c = Veza();
            return await Izvrsi(async () => await c.ExecuteAsync(
                "INSERT INTO dbo.Materijal (Naziv, Sifra) VALUES (@Naziv, @Sifra)",
                new { Naziv = m.Naziv.Trim(), Sifra = Prazno(m.Sifra) }));
        }).RequireAuthorization(Politike.Kancelarija);

        materijal.MapPut("/{id:int}", async (int id, Materijal m) =>
        {
            if (string.IsNullOrWhiteSpace(m.Naziv)) return Greska("Upišite naziv materijala.");
            await using var c = Veza();
            return await Izvrsi(async () => await c.ExecuteAsync(
                "UPDATE dbo.Materijal SET Naziv = @Naziv, Sifra = @Sifra WHERE Id = @id",
                new { id, Naziv = m.Naziv.Trim(), Sifra = Prazno(m.Sifra) }));
        }).RequireAuthorization(Politike.Kancelarija);

        materijal.MapDelete("/{id:int}", async (int id) =>
        {
            await using var c = Veza();
            return await Izvrsi(async () => await c.ExecuteAsync("DELETE FROM dbo.Materijal WHERE Id = @id", new { id }));
        }).RequireAuthorization(Politike.Kancelarija);

        // ---------- Postavke firme i tarifa ----------
        app.MapGet("/api/postavke", async () =>
        {
            await using var c = Veza();
            return Results.Ok(await c.QuerySingleAsync<Postavke>(
                "SELECT TarifaKm, NazivFirme, Adresa, Email, Web, Jib, Mb, ZiroRacun FROM dbo.Postavke WHERE Id = 1"));
        }).RequireAuthorization(Politike.Korisnik);

        app.MapPut("/api/postavke", async (Postavke p) =>
        {
            if (p.TarifaKm < 0) return Greska("Tarifa ne može biti negativna.");
            await using var c = Veza();
            await c.ExecuteAsync(@"UPDATE dbo.Postavke SET TarifaKm = @TarifaKm, NazivFirme = @NazivFirme, Adresa = @Adresa,
                                   Email = @Email, Web = @Web, Jib = @Jib, Mb = @Mb, ZiroRacun = @ZiroRacun WHERE Id = 1",
                new
                {
                    p.TarifaKm,
                    NazivFirme = Prazno(p.NazivFirme),
                    Adresa = Prazno(p.Adresa),
                    Email = Prazno(p.Email),
                    Web = Prazno(p.Web),
                    Jib = Prazno(p.Jib),
                    Mb = Prazno(p.Mb),
                    ZiroRacun = Prazno(p.ZiroRacun)
                });
            return Results.Ok();
        }).RequireAuthorization(Politike.Administrator);

        // ---------- Numeracija naloga ----------
        app.MapGet("/api/numeracija", async () =>
        {
            var godina = DateTime.Now.Year;
            await using var c = Veza();
            var zadnji = await c.ExecuteScalarAsync<int?>("SELECT Zadnji FROM dbo.BrojacNaloga WHERE Godina = @godina", new { godina }) ?? 0;
            var najveci = await c.ExecuteScalarAsync<int>("SELECT ISNULL(MAX(RedniBroj), 0) FROM dbo.Nalog WHERE Godina = @godina", new { godina });
            return Results.Ok(new { godina, zadnji, sljedeci = zadnji + 1, najveciPostojeci = najveci });
        }).RequireAuthorization(Politike.Kancelarija);

        app.MapPut("/api/numeracija", async (NumeracijaUnos u) =>
        {
            var godina = DateTime.Now.Year;
            if (u.Sljedeci < 1) return Greska("Broj mora biti 1 ili veći.");
            await using var c = Veza();
            var najveci = await c.ExecuteScalarAsync<int>("SELECT ISNULL(MAX(RedniBroj), 0) FROM dbo.Nalog WHERE Godina = @godina", new { godina });
            if (u.Sljedeci <= najveci)
                return Greska($"Već postoji nalog {najveci:0000}/{godina}; sljedeći broj mora biti veći od {najveci}.");
            await c.ExecuteAsync(@"
                MERGE dbo.BrojacNaloga WITH (HOLDLOCK) AS t
                USING (SELECT @godina AS Godina) AS s ON t.Godina = s.Godina
                WHEN MATCHED THEN UPDATE SET Zadnji = @zadnji
                WHEN NOT MATCHED THEN INSERT (Godina, Zadnji) VALUES (@godina, @zadnji);",
                new { godina, zadnji = u.Sljedeci - 1 });
            return Results.Ok();
        }).RequireAuthorization(Politike.Administrator);

        // ---------- Nosioci naloga (aktivni korisnici s uključenom oznakom) ----------
        app.MapGet("/api/nosioci", async () =>
        {
            await using var c = Veza();
            return Results.Ok(await c.QueryAsync<Nosilac>(@"
                SELECT Id, COALESCE(ImePrezime, KorisnickiNalog) AS Ime, KorisnickiNalog
                FROM dbo.Korisnik WHERE Aktivan = 1 AND NosilacNaloga = 1
                ORDER BY COALESCE(ImePrezime, KorisnickiNalog)"));
        }).RequireAuthorization(Politike.Korisnik);
    }

    private static string? Prazno(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static IResult Greska(string poruka) => Results.BadRequest(new { greska = poruka });

    /// <summary>Izvrši izmjenu; greške baze (duplikat, stavka u upotrebi) pretvara u razumljivu poruku.</summary>
    private static async Task<IResult> Izvrsi(Func<Task<int>> akcija)
    {
        try
        {
            var redova = await akcija();
            return redova == 0 ? Results.NotFound(new { greska = "Stavka više ne postoji." }) : Results.Ok();
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Results.Conflict(new { greska = "Već postoji stavka s tim nazivom." });
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            return Results.Conflict(new { greska = "Stavka se koristi u uređajima ili nalozima, pa se ne može obrisati." });
        }
    }
}

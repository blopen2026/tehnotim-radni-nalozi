using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Prijava;

namespace Tehnotim.RadniNalozi.Klijenti;

public class KlijentPregled
{
    public int Id { get; set; }
    public string Naziv { get; set; } = "";
    public string? Skracenica { get; set; }
    public bool Ugovor { get; set; }
    public int BrojJedinica { get; set; }
    public int BrojUredjaja { get; set; }
    public int BrojNaloga { get; set; }
}

public class KlijentUnos
{
    public string Naziv { get; set; } = "";
    public string? Skracenica { get; set; }
    public bool Ugovor { get; set; }
}

public class Jedinica
{
    public int Id { get; set; }
    public int KlijentId { get; set; }
    public string Naziv { get; set; } = "";
    public string? Mjesto { get; set; }
    public string? Adresa { get; set; }
    public string? PostanskiBroj { get; set; }
    public string? Telefon { get; set; }
    public string? RadnoVrijeme { get; set; }
    public string? KontaktOsoba { get; set; }
    public string? Email { get; set; }
    public decimal KmOdBL { get; set; }
    public int BrojUredjaja { get; set; }
    public int BrojNaloga { get; set; }
}

public class JedinicaUnos
{
    public string Naziv { get; set; } = "";
    public string? Mjesto { get; set; }
    public string? Adresa { get; set; }
    public string? PostanskiBroj { get; set; }
    public string? Telefon { get; set; }
    public string? RadnoVrijeme { get; set; }
    public string? KontaktOsoba { get; set; }
    public string? Email { get; set; }
    public decimal? KmOdBL { get; set; }

    /// <summary>Prazni stringovi postaju null, sve se skraćuje (trim).</summary>
    public JedinicaUnos Ocisti() => new()
    {
        Naziv = (Naziv ?? "").Trim(),
        Mjesto = P(Mjesto),
        Adresa = P(Adresa),
        PostanskiBroj = P(PostanskiBroj),
        Telefon = P(Telefon),
        RadnoVrijeme = P(RadnoVrijeme),
        KontaktOsoba = P(KontaktOsoba),
        Email = P(Email),
        KmOdBL = KmOdBL is > 0 ? KmOdBL : null
    };

    private static string? P(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public class UvozZahtjev
{
    public string KlijentNaziv { get; set; } = "";
    public List<JedinicaUnos> Jedinice { get; set; } = [];
}

/// <summary>
/// Klijenti i njihove poslovne jedinice. Čitanje: svaki korisnik (treba i formi naloga).
/// Izmjene i uvoz: kancelarija i administrator.
/// </summary>
public static class KlijentiApi
{
    private const string JediniceSql = @"
        SELECT j.Id, j.KlijentId, j.Naziv, j.Mjesto, j.Adresa, j.PostanskiBroj, j.Telefon, j.RadnoVrijeme,
               j.KontaktOsoba, j.Email, j.KmOdBL,
               (SELECT COUNT(*) FROM dbo.Uredjaj u WHERE u.PoslovnaJedinicaId = j.Id) AS BrojUredjaja,
               (SELECT COUNT(*) FROM dbo.Nalog n WHERE n.PoslovnaJedinicaId = j.Id) AS BrojNaloga
        FROM dbo.PoslovnaJedinica j";

    public static void MapKlijenti(this WebApplication app, string connectionString)
    {
        SqlConnection Veza() => new(connectionString);

        var klijenti = app.MapGroup("/api/klijenti").RequireAuthorization(Politike.Korisnik);

        // Lista klijenata s brojem jedinica, uređaja i naloga
        klijenti.MapGet("", async () =>
        {
            await using var c = Veza();
            return Results.Ok(await c.QueryAsync<KlijentPregled>(@"
                SELECT k.Id, k.Naziv, k.Skracenica, k.Ugovor,
                       (SELECT COUNT(*) FROM dbo.PoslovnaJedinica j WHERE j.KlijentId = k.Id) AS BrojJedinica,
                       (SELECT COUNT(*) FROM dbo.Uredjaj u WHERE u.KlijentId = k.Id) AS BrojUredjaja,
                       (SELECT COUNT(*) FROM dbo.Nalog n WHERE n.KlijentId = k.Id) AS BrojNaloga
                FROM dbo.Klijent k ORDER BY k.Naziv"));
        });

        // Jedan klijent s jedinicama
        klijenti.MapGet("/{id:int}", async (int id) =>
        {
            await using var c = Veza();
            var k = await c.QuerySingleOrDefaultAsync<KlijentPregled>(@"
                SELECT k.Id, k.Naziv, k.Skracenica, k.Ugovor,
                       (SELECT COUNT(*) FROM dbo.PoslovnaJedinica j WHERE j.KlijentId = k.Id) AS BrojJedinica,
                       (SELECT COUNT(*) FROM dbo.Uredjaj u WHERE u.KlijentId = k.Id) AS BrojUredjaja,
                       (SELECT COUNT(*) FROM dbo.Nalog n WHERE n.KlijentId = k.Id) AS BrojNaloga
                FROM dbo.Klijent k WHERE k.Id = @id", new { id });
            if (k is null) return Results.NotFound(new { greska = "Klijent ne postoji." });
            var jedinice = await c.QueryAsync<Jedinica>(JediniceSql + " WHERE j.KlijentId = @id ORDER BY j.Naziv", new { id });
            return Results.Ok(new { klijent = k, jedinice });
        });

        klijenti.MapPost("", async (KlijentUnos u) =>
        {
            var g = Provjeri(u); if (g is not null) return Greska(g);
            await using var c = Veza();
            return await Izvrsi(async () =>
            {
                var id = await c.ExecuteScalarAsync<int>(
                    "INSERT INTO dbo.Klijent (Naziv, Skracenica, Ugovor) OUTPUT INSERTED.Id VALUES (@Naziv, @Skracenica, @Ugovor)",
                    new { Naziv = u.Naziv.Trim(), Skracenica = Prazno(u.Skracenica), u.Ugovor });
                return Results.Ok(new { id });
            });
        }).RequireAuthorization(Politike.Kancelarija);

        klijenti.MapPut("/{id:int}", async (int id, KlijentUnos u) =>
        {
            var g = Provjeri(u); if (g is not null) return Greska(g);
            await using var c = Veza();
            return await Izvrsi(async () =>
            {
                var n = await c.ExecuteAsync(
                    "UPDATE dbo.Klijent SET Naziv = @Naziv, Skracenica = @Skracenica, Ugovor = @Ugovor WHERE Id = @id",
                    new { id, Naziv = u.Naziv.Trim(), Skracenica = Prazno(u.Skracenica), u.Ugovor });
                return n == 0 ? Results.NotFound(new { greska = "Klijent ne postoji." }) : Results.Ok();
            });
        }).RequireAuthorization(Politike.Kancelarija);

        // Brisanje: samo klijent bez uređaja i naloga (jedinice se brišu s njim)
        klijenti.MapDelete("/{id:int}", async (int id) =>
        {
            await using var c = Veza();
            await c.OpenAsync();
            await using var tx = await c.BeginTransactionAsync();
            return await Izvrsi(async () =>
            {
                await c.ExecuteAsync("DELETE FROM dbo.PoslovnaJedinica WHERE KlijentId = @id", new { id }, tx);
                var n = await c.ExecuteAsync("DELETE FROM dbo.Klijent WHERE Id = @id", new { id }, tx);
                await tx.CommitAsync();
                return n == 0 ? Results.NotFound(new { greska = "Klijent ne postoji." }) : Results.Ok();
            }, "Klijent ima uređaje ili naloge, pa se ne može obrisati.");
        }).RequireAuthorization(Politike.Kancelarija);

        // ---------- Poslovne jedinice ----------
        klijenti.MapPost("/{klijentId:int}/jedinice", async (int klijentId, JedinicaUnos unos) =>
        {
            var u = unos.Ocisti();
            if (u.Naziv.Length == 0) return Greska("Upišite naziv poslovne jedinice.");
            await using var c = Veza();
            return await Izvrsi(async () =>
            {
                var id = await c.ExecuteScalarAsync<int>(@"
                    INSERT INTO dbo.PoslovnaJedinica (KlijentId, Naziv, Mjesto, Adresa, PostanskiBroj, Telefon, RadnoVrijeme, KontaktOsoba, Email, KmOdBL)
                    OUTPUT INSERTED.Id
                    VALUES (@klijentId, @Naziv, @Mjesto, @Adresa, @PostanskiBroj, @Telefon, @RadnoVrijeme, @KontaktOsoba, @Email, @Km)",
                    new { klijentId, u.Naziv, u.Mjesto, u.Adresa, u.PostanskiBroj, u.Telefon, u.RadnoVrijeme, u.KontaktOsoba, u.Email, Km = u.KmOdBL ?? 0 });
                return Results.Ok(new { id });
            });
        }).RequireAuthorization(Politike.Kancelarija);

        var jedinice = app.MapGroup("/api/jedinice").RequireAuthorization(Politike.Kancelarija);

        jedinice.MapPut("/{id:int}", async (int id, JedinicaUnos unos) =>
        {
            var u = unos.Ocisti();
            if (u.Naziv.Length == 0) return Greska("Upišite naziv poslovne jedinice.");
            await using var c = Veza();
            return await Izvrsi(async () =>
            {
                var n = await c.ExecuteAsync(@"
                    UPDATE dbo.PoslovnaJedinica SET Naziv = @Naziv, Mjesto = @Mjesto, Adresa = @Adresa, PostanskiBroj = @PostanskiBroj,
                           Telefon = @Telefon, RadnoVrijeme = @RadnoVrijeme, KontaktOsoba = @KontaktOsoba, Email = @Email, KmOdBL = @Km
                    WHERE Id = @id",
                    new { id, u.Naziv, u.Mjesto, u.Adresa, u.PostanskiBroj, u.Telefon, u.RadnoVrijeme, u.KontaktOsoba, u.Email, Km = u.KmOdBL ?? 0 });
                return n == 0 ? Results.NotFound(new { greska = "Jedinica ne postoji." }) : Results.Ok();
            });
        });

        jedinice.MapDelete("/{id:int}", async (int id) =>
        {
            await using var c = Veza();
            return await Izvrsi(async () =>
            {
                var n = await c.ExecuteAsync("DELETE FROM dbo.PoslovnaJedinica WHERE Id = @id", new { id });
                return n == 0 ? Results.NotFound(new { greska = "Jedinica ne postoji." }) : Results.Ok();
            }, "Jedinica ima uređaje ili naloge, pa se ne može obrisati.");
        });

        // ---------- Uvoz jedinica iz tabele ----------
        // Postojeća jedinica (isti naziv ili ista adresa) se dopunjava; ostale se dodaju. Sve ili ništa.
        app.MapPost("/api/klijenti/uvoz", async (UvozZahtjev z) =>
        {
            var naziv = (z.KlijentNaziv ?? "").Trim();
            if (naziv.Length == 0) return Greska("Upišite ili izaberite klijenta.");
            var redovi = (z.Jedinice ?? []).Select(j => j.Ocisti()).Where(j => j.Naziv.Length > 0).ToList();
            if (redovi.Count == 0) return Greska("Nema redova sa nazivom jedinice.");

            await using var c = Veza();
            await c.OpenAsync();
            await using var tx = await c.BeginTransactionAsync();

            var klijentId = await c.ExecuteScalarAsync<int?>(
                "SELECT Id FROM dbo.Klijent WITH (UPDLOCK) WHERE Naziv = @naziv", new { naziv }, tx)
                ?? await c.ExecuteScalarAsync<int>(
                    "INSERT INTO dbo.Klijent (Naziv) OUTPUT INSERTED.Id VALUES (@naziv)", new { naziv }, tx);

            var postojece = (await c.QueryAsync<Jedinica>(
                "SELECT Id, KlijentId, Naziv, Mjesto, Adresa, PostanskiBroj, Telefon, RadnoVrijeme, KontaktOsoba, Email, KmOdBL FROM dbo.PoslovnaJedinica WHERE KlijentId = @klijentId",
                new { klijentId }, tx)).ToList();

            // Po adresi se prepoznaje samo jedinica koja je postojala prije uvoza i u istom je mjestu
            // (dvije jedinice iz istog fajla na istoj adresi, npr. područni centar i područna jedinica, ostaju odvojene)
            var prijeUvoza = postojece.ToList();
            int novih = 0, azuriranih = 0;
            foreach (var r in redovi)
            {
                var j = postojece.FirstOrDefault(x => Isto(x.Naziv, r.Naziv))
                     ?? (r.Adresa is null ? null : prijeUvoza.FirstOrDefault(x => Isto(x.Adresa, r.Adresa)
                            && (string.IsNullOrWhiteSpace(x.Mjesto) || string.IsNullOrWhiteSpace(r.Mjesto) || Isto(x.Mjesto, r.Mjesto))));
                if (j is null)
                {
                    var id = await c.ExecuteScalarAsync<int>(@"
                        INSERT INTO dbo.PoslovnaJedinica (KlijentId, Naziv, Mjesto, Adresa, PostanskiBroj, Telefon, RadnoVrijeme, KontaktOsoba, Email, KmOdBL)
                        OUTPUT INSERTED.Id
                        VALUES (@klijentId, @Naziv, @Mjesto, @Adresa, @PostanskiBroj, @Telefon, @RadnoVrijeme, @KontaktOsoba, @Email, @Km)",
                        new { klijentId, r.Naziv, r.Mjesto, r.Adresa, r.PostanskiBroj, r.Telefon, r.RadnoVrijeme, r.KontaktOsoba, r.Email, Km = r.KmOdBL ?? 0 }, tx);
                    postojece.Add(new Jedinica
                    {
                        Id = id, KlijentId = klijentId, Naziv = r.Naziv, Mjesto = r.Mjesto, Adresa = r.Adresa, PostanskiBroj = r.PostanskiBroj,
                        Telefon = r.Telefon, RadnoVrijeme = r.RadnoVrijeme, KontaktOsoba = r.KontaktOsoba, Email = r.Email, KmOdBL = r.KmOdBL ?? 0
                    });
                    novih++;
                }
                else
                {
                    // Naziv ostaje; kontakt se ne gazi; novi telefon se dodaje uz postojeći; ostalo se dopunjava
                    j.Mjesto = r.Mjesto ?? j.Mjesto;
                    j.Adresa = r.Adresa ?? j.Adresa;
                    j.PostanskiBroj = r.PostanskiBroj ?? j.PostanskiBroj;
                    j.RadnoVrijeme = r.RadnoVrijeme ?? j.RadnoVrijeme;
                    j.Email = r.Email ?? j.Email;
                    j.KontaktOsoba ??= r.KontaktOsoba;
                    if (r.KmOdBL is > 0) j.KmOdBL = r.KmOdBL.Value;
                    if (r.Telefon is not null)
                        j.Telefon = string.IsNullOrWhiteSpace(j.Telefon) ? r.Telefon
                                  : j.Telefon.Contains(r.Telefon, StringComparison.OrdinalIgnoreCase) ? j.Telefon
                                  : j.Telefon + ", " + r.Telefon;
                    await c.ExecuteAsync(@"
                        UPDATE dbo.PoslovnaJedinica SET Mjesto = @Mjesto, Adresa = @Adresa, PostanskiBroj = @PostanskiBroj, Telefon = @Telefon,
                               RadnoVrijeme = @RadnoVrijeme, KontaktOsoba = @KontaktOsoba, Email = @Email, KmOdBL = @KmOdBL
                        WHERE Id = @Id", j, tx);
                    azuriranih++;
                }
            }

            await tx.CommitAsync();
            return Results.Ok(new { klijentId, novih, azuriranih });
        }).RequireAuthorization(Politike.Kancelarija);
    }

    private static bool Isto(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? Provjeri(KlijentUnos u) =>
        string.IsNullOrWhiteSpace(u.Naziv) ? "Upišite naziv klijenta." : null;

    private static string? Prazno(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static IResult Greska(string poruka) => Results.BadRequest(new { greska = poruka });

    private static async Task<IResult> Izvrsi(Func<Task<IResult>> akcija, string porukaUUpotrebi = "Stavka se koristi, pa se ne može obrisati.")
    {
        try
        {
            return await akcija();
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Results.Conflict(new { greska = "Već postoji stavka s tim nazivom." });
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            return Results.Conflict(new { greska = porukaUUpotrebi });
        }
    }
}

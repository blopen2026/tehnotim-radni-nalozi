using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Prijava;

namespace Tehnotim.RadniNalozi.Uredjaji;

public class UredjajPregled
{
    public int Id { get; set; }
    public string? SerijskiBroj { get; set; }
    public string? InventurniBroj { get; set; }
    public string? Model { get; set; }
    public int? TipUredjajaId { get; set; }
    public string? TipNaziv { get; set; }
    public bool PratiBrojac { get; set; }
    public bool SamoCrnoBijeli { get; set; }
    public int? KlijentId { get; set; }
    public string? KlijentNaziv { get; set; }
    public int? PoslovnaJedinicaId { get; set; }
    public string? JedinicaNaziv { get; set; }
    public DateTime? GarancijaDo { get; set; }
    public int? ZadnjiKolor { get; set; }
    public int? ZadnjiCB { get; set; }
    public DateTime? BrojacDatum { get; set; }
    public int BrojNaloga { get; set; }
}

public class UredjajUnos
{
    public string? SerijskiBroj { get; set; }
    public string? InventurniBroj { get; set; }
    public string? Model { get; set; }
    public int? TipUredjajaId { get; set; }
    public bool SamoCrnoBijeli { get; set; }
    public int? KlijentId { get; set; }
    public int? PoslovnaJedinicaId { get; set; }
    public DateTime? GarancijaDo { get; set; }
    public int? ZadnjiKolor { get; set; }
    public int? ZadnjiCB { get; set; }
    public DateTime? BrojacDatum { get; set; }
}

public class UvozUredjaja
{
    public int KlijentId { get; set; }
    public List<UvozUredjajaRed>? Redovi { get; set; }
}

public class UvozUredjajaRed
{
    public string? SerijskiBroj { get; set; }
    public string? InventurniBroj { get; set; }
    public string? Model { get; set; }
    public int? TipUredjajaId { get; set; }
    public int? PoslovnaJedinicaId { get; set; }
}

public class ModelTip
{
    public string Model { get; set; } = "";
    public int? TipUredjajaId { get; set; }
    public bool SamoCrnoBijeli { get; set; }
}

/// <summary>
/// Uređaji klijenata. Čitanje i traženje: svaki korisnik (treba formi naloga).
/// Dodavanje, izmjena, brisanje na stranici Uređaji: kancelarija i administrator.
/// </summary>
public static class UredjajiApi
{
    private const string PregledSql = @"
        SELECT u.Id, u.SerijskiBroj, u.InventurniBroj, u.Model, u.TipUredjajaId, t.Naziv AS TipNaziv,
               ISNULL(t.PratiBrojac, 0) AS PratiBrojac, u.SamoCrnoBijeli,
               u.KlijentId, k.Naziv AS KlijentNaziv, u.PoslovnaJedinicaId, j.Naziv AS JedinicaNaziv,
               u.GarancijaDo, u.ZadnjiKolor, u.ZadnjiCB, u.BrojacDatum,
               (SELECT COUNT(DISTINCT nu.NalogId) FROM dbo.NalogUredjaj nu WHERE nu.UredjajId = u.Id) AS BrojNaloga
        FROM dbo.Uredjaj u
        LEFT JOIN dbo.TipUredjaja t ON t.Id = u.TipUredjajaId
        LEFT JOIN dbo.Klijent k ON k.Id = u.KlijentId
        LEFT JOIN dbo.PoslovnaJedinica j ON j.Id = u.PoslovnaJedinicaId";

    public static void MapUredjaji(this WebApplication app, string connectionString)
    {
        SqlConnection Veza() => new(connectionString);
        var uredjaji = app.MapGroup("/api/uredjaji").RequireAuthorization(Politike.Korisnik);

        // Lista s pretragom (serijski, inventurni, model, klijent, jedinica) i filterom po klijentu
        uredjaji.MapGet("", async (string? q, int? klijentId) =>
        {
            await using var c = Veza();
            var trazi = string.IsNullOrWhiteSpace(q) ? null : "%" + q.Trim() + "%";
            return Results.Ok(await c.QueryAsync<UredjajPregled>(PregledSql + @"
                WHERE (@klijentId IS NULL OR u.KlijentId = @klijentId)
                  AND (@trazi IS NULL OR u.SerijskiBroj LIKE @trazi OR u.InventurniBroj LIKE @trazi OR u.Model LIKE @trazi
                       OR k.Naziv LIKE @trazi OR j.Naziv LIKE @trazi)
                ORDER BY k.Naziv, j.Naziv, u.SerijskiBroj
                OFFSET 0 ROWS FETCH NEXT 500 ROWS ONLY", new { klijentId, trazi }));
        });

        // Tačan pogodak po serijskom ili inventurnom broju (za formu naloga)
        // Inventurni brojevi se mogu ponavljati kod različitih klijenata – ako je klijent poznat, prednost ima njegov uređaj
        uredjaji.MapGet("/po-broju", async (string broj, int? klijentId) =>
        {
            if (string.IsNullOrWhiteSpace(broj)) return Results.NotFound();
            await using var c = Veza();
            var u = await c.QueryFirstOrDefaultAsync<UredjajPregled>(PregledSql + @"
                WHERE u.SerijskiBroj = @broj OR (u.InventurniBroj = @broj AND (@klijentId IS NULL OR u.KlijentId = @klijentId OR u.KlijentId IS NULL))
                ORDER BY CASE WHEN u.SerijskiBroj = @broj THEN 0 WHEN u.KlijentId = @klijentId THEN 1 ELSE 2 END", new { broj = broj.Trim(), klijentId });
            return u is null ? Results.NotFound() : Results.Ok(u);
        });

        // Poznati modeli i njihov tip (prijedlog tipa za novi uređaj istog modela)
        uredjaji.MapGet("/modeli", async () =>
        {
            await using var c = Veza();
            return Results.Ok(await c.QueryAsync<ModelTip>(@"
                SELECT Model, MAX(TipUredjajaId) AS TipUredjajaId, CAST(MAX(CAST(SamoCrnoBijeli AS int)) AS bit) AS SamoCrnoBijeli
                FROM dbo.Uredjaj WHERE Model IS NOT NULL AND Model <> ''
                GROUP BY Model ORDER BY Model"));
        });

        // Istorija: svi nalozi u kojima se uređaj pojavljuje, najnoviji prvi
        uredjaji.MapGet("/{id:int}/nalozi", async (int id) =>
        {
            await using var c = Veza();
            return Results.Ok(await c.QueryAsync<Nalozi.NalogUredjajaStavka>(@"
                SELECT n.Id, n.Broj, n.Faza, n.DatumIzdavanja, n.PozivInterni, n.NosilacIme, n.Status,
                       LEFT(COALESCE(NULLIF(n.OpisIntervencije, ''), n.OpisZahtjeva), 120) AS Opis,
                       nu.Kolor, nu.CB
                FROM dbo.NalogUredjaj nu JOIN dbo.Nalog n ON n.Id = nu.NalogId
                WHERE nu.UredjajId = @id
                ORDER BY n.DatumIzdavanja DESC, n.Id DESC", new { id }));
        });

        uredjaji.MapGet("/{id:int}", async (int id) =>
        {
            await using var c = Veza();
            var u = await c.QuerySingleOrDefaultAsync<UredjajPregled>(PregledSql + " WHERE u.Id = @id", new { id });
            return u is null ? Results.NotFound(new { greska = "Uređaj ne postoji." }) : Results.Ok(u);
        });

        uredjaji.MapPost("", async (UredjajUnos u) =>
        {
            await using var c = Veza();
            var g = await Provjeri(c, u) ?? await DuplikatInventurnog(c, u, null); if (g is not null) return Greska(g);
            return await Izvrsi(async () =>
            {
                var id = await c.ExecuteScalarAsync<int>(@"
                    INSERT INTO dbo.Uredjaj (SerijskiBroj, InventurniBroj, Model, TipUredjajaId, SamoCrnoBijeli, KlijentId, PoslovnaJedinicaId,
                                             GarancijaDo, ZadnjiKolor, ZadnjiCB, BrojacDatum)
                    OUTPUT INSERTED.Id
                    VALUES (@SerijskiBroj, @InventurniBroj, @Model, @TipUredjajaId, @SamoCrnoBijeli, @KlijentId, @PoslovnaJedinicaId,
                            @GarancijaDo, @ZadnjiKolor, @ZadnjiCB, @BrojacDatum)", u);
                return Results.Ok(new { id });
            });
        }).RequireAuthorization(Politike.Kancelarija);

        uredjaji.MapPut("/{id:int}", async (int id, UredjajUnos u) =>
        {
            await using var c = Veza();
            var g = await Provjeri(c, u) ?? await DuplikatInventurnog(c, u, id); if (g is not null) return Greska(g);
            return await Izvrsi(async () =>
            {
                var n = await c.ExecuteAsync(@"
                    UPDATE dbo.Uredjaj SET SerijskiBroj = @SerijskiBroj, InventurniBroj = @InventurniBroj, Model = @Model,
                           TipUredjajaId = @TipUredjajaId, SamoCrnoBijeli = @SamoCrnoBijeli, KlijentId = @KlijentId,
                           PoslovnaJedinicaId = @PoslovnaJedinicaId, GarancijaDo = @GarancijaDo,
                           ZadnjiKolor = @ZadnjiKolor, ZadnjiCB = @ZadnjiCB, BrojacDatum = @BrojacDatum
                    WHERE Id = @Id",
                    new
                    {
                        Id = id, u.SerijskiBroj, u.InventurniBroj, u.Model, u.TipUredjajaId, u.SamoCrnoBijeli, u.KlijentId,
                        u.PoslovnaJedinicaId, u.GarancijaDo, u.ZadnjiKolor, u.ZadnjiCB, u.BrojacDatum
                    });
                return n == 0 ? Results.NotFound(new { greska = "Uređaj ne postoji." }) : Results.Ok();
            });
        }).RequireAuthorization(Politike.Kancelarija);

        uredjaji.MapDelete("/{id:int}", async (int id) =>
        {
            await using var c = Veza();
            return await Izvrsi(async () =>
            {
                var n = await c.ExecuteAsync("DELETE FROM dbo.Uredjaj WHERE Id = @id", new { id });
                return n == 0 ? Results.NotFound(new { greska = "Uređaj ne postoji." }) : Results.Ok();
            });
        }).RequireAuthorization(Politike.Kancelarija);

        // Uvoz uređaja iz Excela (npr. popis osnovnih sredstava klijenta). Pregled i mapiranje radi stranica;
        // ovdje: uređaj se prepoznaje po serijskom broju, inače po inventurnom broju kod klijenta → ažurira; ostali se dodaju.
        uredjaji.MapPost("/uvoz", async (UvozUredjaja unos) =>
        {
            if (unos.KlijentId <= 0) return Greska("Izaberite klijenta.");
            await using var c = Veza();
            await c.OpenAsync();
            await using var tx = await c.BeginTransactionAsync();
            var jedinice = (await c.QueryAsync<int>("SELECT Id FROM dbo.PoslovnaJedinica WHERE KlijentId = @k", new { k = unos.KlijentId }, tx)).ToHashSet();
            var tipovi = (await c.QueryAsync<int>("SELECT Id FROM dbo.TipUredjaja", transaction: tx)).ToHashSet();
            int novih = 0, azuriranih = 0, preskocenih = 0;
            foreach (var r in unos.Redovi ?? [])
            {
                var ser = Prazno(r.SerijskiBroj);
                var inv = Prazno(r.InventurniBroj);
                if (ser is null && inv is null) { preskocenih++; continue; }
                int? jed = r.PoslovnaJedinicaId is int j && jedinice.Contains(j) ? j : null;
                int? tip = r.TipUredjajaId is int t && tipovi.Contains(t) ? t : null;
                var model = Prazno(r.Model);

                int? id = null;
                if (ser is not null)
                    id = await c.ExecuteScalarAsync<int?>("SELECT Id FROM dbo.Uredjaj WHERE SerijskiBroj = @ser", new { ser }, tx);
                if (id is null && inv is not null)
                    id = await c.ExecuteScalarAsync<int?>("SELECT TOP 1 Id FROM dbo.Uredjaj WHERE KlijentId = @k AND InventurniBroj = @inv",
                        new { k = unos.KlijentId, inv }, tx);

                if (id is null)
                {
                    await c.ExecuteAsync(@"INSERT INTO dbo.Uredjaj (SerijskiBroj, InventurniBroj, Model, TipUredjajaId, KlijentId, PoslovnaJedinicaId)
                                           VALUES (@ser, @inv, @model, @tip, @k, @jed)", new { ser, inv, model, tip, k = unos.KlijentId, jed }, tx);
                    novih++;
                }
                else
                {
                    await c.ExecuteAsync(@"UPDATE dbo.Uredjaj SET SerijskiBroj = COALESCE(SerijskiBroj, @ser), InventurniBroj = COALESCE(@inv, InventurniBroj),
                                               Model = COALESCE(@model, Model), TipUredjajaId = COALESCE(@tip, TipUredjajaId),
                                               KlijentId = @k, PoslovnaJedinicaId = COALESCE(@jed, PoslovnaJedinicaId)
                                           WHERE Id = @id", new { id, ser, inv, model, tip, k = unos.KlijentId, jed }, tx);
                    azuriranih++;
                }
            }
            await tx.CommitAsync();
            return Results.Ok(new { novih, azuriranih, preskocenih });
        }).RequireAuthorization(Politike.Kancelarija);
    }

    /// <summary>Uređaj bez serijskog broja: inventurni broj mora biti jedinstven kod klijenta.</summary>
    private static async Task<string?> DuplikatInventurnog(SqlConnection c, UredjajUnos u, int? id)
    {
        if (u.InventurniBroj is null || u.KlijentId is null) return null;
        var drugi = await c.ExecuteScalarAsync<int?>(
            "SELECT TOP 1 Id FROM dbo.Uredjaj WHERE KlijentId = @k AND InventurniBroj = @inv AND (@id IS NULL OR Id <> @id)",
            new { k = u.KlijentId, inv = u.InventurniBroj, id });
        return drugi is null ? null : "Kod ovog klijenta već postoji uređaj s tim inventurnim brojem.";
    }

    /// <summary>Čisti unos i provjerava ga; vraća poruku greške ili null.</summary>
    private static async Task<string?> Provjeri(SqlConnection c, UredjajUnos u)
    {
        u.SerijskiBroj = Prazno(u.SerijskiBroj);
        u.InventurniBroj = Prazno(u.InventurniBroj);
        u.Model = Prazno(u.Model);
        if (u.SerijskiBroj is null && u.InventurniBroj is null) return "Upišite serijski ili inventurni broj.";
        if (u.ZadnjiKolor < 0 || u.ZadnjiCB < 0) return "Stanje brojača ne može biti negativno.";
        if (u.GarancijaDo is not null) u.GarancijaDo = u.GarancijaDo.Value.Date;
        if (u.BrojacDatum is not null) u.BrojacDatum = u.BrojacDatum.Value.Date;
        if (u.ZadnjiKolor is null && u.ZadnjiCB is null) u.BrojacDatum = null;

        if (u.PoslovnaJedinicaId is not null)
        {
            // Jedinica određuje klijenta; ne smije pripadati drugom klijentu
            var klijentJedinice = await c.ExecuteScalarAsync<int?>(
                "SELECT KlijentId FROM dbo.PoslovnaJedinica WHERE Id = @id", new { id = u.PoslovnaJedinicaId });
            if (klijentJedinice is null) return "Izabrana poslovna jedinica ne postoji.";
            if (u.KlijentId is not null && u.KlijentId != klijentJedinice) return "Poslovna jedinica ne pripada izabranom klijentu.";
            u.KlijentId = klijentJedinice;
        }
        if (u.SerijskiBroj is null && u.KlijentId is null) return "Uređaj bez serijskog broja mora imati klijenta (prepoznaje se po inventurnom broju kod klijenta).";
        return null;
    }

    private static string? Prazno(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static IResult Greska(string poruka) => Results.BadRequest(new { greska = poruka });

    private static async Task<IResult> Izvrsi(Func<Task<IResult>> akcija)
    {
        try
        {
            return await akcija();
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Results.Conflict(new { greska = "Uređaj s tim serijskim brojem već postoji." });
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            return Results.Conflict(new { greska = "Uređaj se pojavljuje u nalozima, pa se ne može obrisati." });
        }
    }
}

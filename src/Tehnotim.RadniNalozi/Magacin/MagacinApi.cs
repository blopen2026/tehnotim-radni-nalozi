using System.Data.Common;
using System.Security.Claims;
using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Prijava;

namespace Tehnotim.RadniNalozi.Magacin;

public class Artikal
{
    public int Id { get; set; }
    public string Naziv { get; set; } = "";
    public string? PN { get; set; }
    public bool VodiSeSN { get; set; }
    public decimal Stanje { get; set; }
    public decimal? MinKolicina { get; set; }
    public bool Aktivan { get; set; }
}

public class ArtikalUnos
{
    public string Naziv { get; set; } = "";
    public string? PN { get; set; }
    public bool VodiSeSN { get; set; }
    public decimal? MinKolicina { get; set; }
    public bool Aktivan { get; set; } = true;
}

public class KomadMagacina
{
    public int Id { get; set; }
    public string SerijskiBroj { get; set; } = "";
    public string Status { get; set; } = "";
    public int? NalogId { get; set; }
    public string? NalogBroj { get; set; }
    public DateTime Primljeno { get; set; }
}

public class PromjenaMagacina
{
    public DateTime Vrijeme { get; set; }
    public string? Korisnik { get; set; }
    public string Vrsta { get; set; } = "";
    public decimal Kolicina { get; set; }
    public decimal StanjePoslije { get; set; }
    public int? NalogId { get; set; }
    public string? NalogBroj { get; set; }
    public string? SerijskiBroj { get; set; }
    public string? Dokument { get; set; }
    public string? Napomena { get; set; }
}

public class StavkaVanMagacina
{
    public int Id { get; set; }               // NalogMaterijal.Id
    public int NalogId { get; set; }
    public string NalogBroj { get; set; } = "";
    public DateTime DatumIzdavanja { get; set; }
    public string KlijentNaziv { get; set; } = "";
    public int? MaterijalId { get; set; }
    public string Naziv { get; set; } = "";
    public string? PN { get; set; }
    public string? SerijskiBroj { get; set; }
    public decimal? Kolicina { get; set; }
}

public record PrijemUnos(decimal? Kolicina, List<string>? SerijskiBrojevi, string? Dokument, string? Napomena);
public record KorekcijaUnos(decimal NovoStanje, string? Napomena);
public record NapomenaUnos(string? Napomena);
public record PoveziUnos(int MaterijalId, string? SerijskiBroj);

/// <summary>Magacin rezervnih dijelova. Mijenja samo administrator; artikle i stanje vide svi (treba formi naloga).</summary>
public static class MagacinApi
{
    private const string ArtikalKolone = "Id, Naziv, PN, VodiSeSN, Stanje, MinKolicina, Aktivan";
    private static string? P(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static IResult Greska(string poruka) => Results.BadRequest(new { greska = poruka });
    private static string? Ko(ClaimsPrincipal k) => k.FindFirstValue(Tvrdnje.Ime) ?? k.Identity?.Name;

    public static void MapMagacin(this WebApplication app, string connectionString)
    {
        SqlConnection Veza() => new(connectionString);
        var g = app.MapGroup("/api/magacin").RequireAuthorization(Politike.Korisnik);

        // ---------- Artikli ----------
        g.MapGet("/artikli", async (bool? sve) =>
        {
            await using var c = Veza();
            return Results.Ok(await c.QueryAsync<Artikal>(
                $"SELECT {ArtikalKolone} FROM dbo.Materijal WHERE (@sve = 1 OR Aktivan = 1) ORDER BY Naziv", new { sve = sve == true }));
        });

        g.MapGet("/artikli/{id:int}/komadi", async (int id, string? status) =>
        {
            await using var c = Veza();
            return Results.Ok(await c.QueryAsync<KomadMagacina>(@"
                SELECT k.Id, k.SerijskiBroj, k.Status, k.NalogId, n.Broj AS NalogBroj, k.Primljeno
                FROM dbo.MagacinKomad k LEFT JOIN dbo.Nalog n ON n.Id = k.NalogId
                WHERE k.MaterijalId = @id AND (@status IS NULL OR k.Status = @status)
                ORDER BY CASE k.Status WHEN 'na_stanju' THEN 0 WHEN 'utrosen' THEN 1 ELSE 2 END, k.SerijskiBroj", new { id, status }));
        });

        g.MapPost("/artikli", async (ArtikalUnos u) =>
        {
            if (string.IsNullOrWhiteSpace(u.Naziv)) return Greska("Upišite naziv artikla.");
            if (u.MinKolicina < 0) return Greska("Minimalna količina ne može biti negativna.");
            await using var c = Veza();
            try
            {
                var id = await c.ExecuteScalarAsync<int>(@"
                    INSERT INTO dbo.Materijal (Naziv, PN, VodiSeSN, MinKolicina, Aktivan) OUTPUT INSERTED.Id
                    VALUES (@Naziv, @PN, @VodiSeSN, @MinKolicina, 1)",
                    new { Naziv = u.Naziv.Trim(), PN = P(u.PN), u.VodiSeSN, u.MinKolicina });
                return Results.Ok(new { id });
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627) { return Results.Conflict(new { greska = "Artikal s tim nazivom već postoji." }); }
        }).RequireAuthorization(Politike.Administrator);

        g.MapPut("/artikli/{id:int}", async (int id, ArtikalUnos u) =>
        {
            if (string.IsNullOrWhiteSpace(u.Naziv)) return Greska("Upišite naziv artikla.");
            if (u.MinKolicina < 0) return Greska("Minimalna količina ne može biti negativna.");
            await using var c = Veza();
            var post = await c.QuerySingleOrDefaultAsync<Artikal>($"SELECT {ArtikalKolone} FROM dbo.Materijal WHERE Id = @id", new { id });
            if (post is null) return Results.NotFound(new { greska = "Artikal ne postoji." });
            if (post.VodiSeSN != u.VodiSeSN)
            {
                var imaKretanja = await c.ExecuteScalarAsync<int>(
                    "SELECT (SELECT COUNT(*) FROM dbo.MagacinPromjena WHERE MaterijalId = @id) + (SELECT COUNT(*) FROM dbo.MagacinKomad WHERE MaterijalId = @id)", new { id });
                if (imaKretanja > 0 || post.Stanje != 0)
                    return Greska("Način vođenja (po količini / po SN) može se promijeniti samo dok artikal nema nijedno kretanje u magacinu.");
            }
            try
            {
                await c.ExecuteAsync(@"UPDATE dbo.Materijal SET Naziv = @Naziv, PN = @PN, VodiSeSN = @VodiSeSN, MinKolicina = @MinKolicina, Aktivan = @Aktivan WHERE Id = @id",
                    new { id, Naziv = u.Naziv.Trim(), PN = P(u.PN), u.VodiSeSN, u.MinKolicina, u.Aktivan });
                return Results.Ok();
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627) { return Results.Conflict(new { greska = "Artikal s tim nazivom već postoji." }); }
        }).RequireAuthorization(Politike.Administrator);

        // Brisanje samo artikla bez ikakvog kretanja; inače ga treba isključiti (Aktivan = ne)
        g.MapDelete("/artikli/{id:int}", async (int id) =>
        {
            await using var c = Veza();
            var imaKretanja = await c.ExecuteScalarAsync<int>(
                "SELECT (SELECT COUNT(*) FROM dbo.MagacinPromjena WHERE MaterijalId = @id) + (SELECT COUNT(*) FROM dbo.MagacinKomad WHERE MaterijalId = @id)", new { id });
            if (imaKretanja > 0) return Results.Conflict(new { greska = "Artikal ima kretanja u magacinu, pa se ne može obrisati. Isključite ga (Aktivan: ne)." });
            try
            {
                var n = await c.ExecuteAsync("DELETE FROM dbo.Materijal WHERE Id = @id", new { id });
                return n == 0 ? Results.NotFound(new { greska = "Artikal ne postoji." }) : Results.Ok();
            }
            catch (SqlException ex) when (ex.Number == 547) { return Results.Conflict(new { greska = "Artikal je upisan u nalozima, pa se ne može obrisati. Isključite ga (Aktivan: ne)." }); }
        }).RequireAuthorization(Politike.Administrator);

        // ---------- Prijem robe ----------
        g.MapPost("/artikli/{id:int}/prijem", async (int id, PrijemUnos u, ClaimsPrincipal ko) =>
        {
            await using var c = Veza();
            await c.OpenAsync();
            await using DbTransaction tx = await c.BeginTransactionAsync();
            var a = await c.QuerySingleOrDefaultAsync<Artikal>($"SELECT {ArtikalKolone} FROM dbo.Materijal WITH (UPDLOCK) WHERE Id = @id", new { id }, tx);
            if (a is null) return Results.NotFound(new { greska = "Artikal ne postoji." });
            var dokument = P(u.Dokument); var napomena = P(u.Napomena);

            if (!a.VodiSeSN)
            {
                if (u.Kolicina is not > 0) return Greska("Upišite primljenu količinu (veću od 0).");
                await Knjizenje.PromijeniStanjeAsync(c, tx, id, u.Kolicina.Value, "prijem", Ko(ko), null, null, null, dokument, napomena);
            }
            else
            {
                var brojevi = (u.SerijskiBrojevi ?? []).Select(P).Where(s => s is not null).Select(s => s!).ToList();
                if (brojevi.Count == 0) return Greska("Upišite serijske brojeve primljenih komada (svaki u svom redu).");
                var duplikat = brojevi.GroupBy(s => s, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
                if (duplikat is not null) return Greska($"Serijski broj {duplikat.Key} je upisan više puta.");
                foreach (var sn in brojevi)
                {
                    var postoji = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.MagacinKomad WHERE MaterijalId = @id AND SerijskiBroj = @sn", new { id, sn }, tx);
                    if (postoji > 0) return Greska($"Komad sa serijskim brojem {sn} već postoji kod ovog artikla.");
                    await c.ExecuteAsync("INSERT INTO dbo.MagacinKomad (MaterijalId, SerijskiBroj) VALUES (@id, @sn)", new { id, sn }, tx);
                    await Knjizenje.PromijeniStanjeAsync(c, tx, id, 1, "prijem", Ko(ko), null, null, sn, dokument, napomena);
                }
            }
            await tx.CommitAsync();
            return Results.Ok();
        }).RequireAuthorization(Politike.Administrator);

        // ---------- Korekcija stanja (inventura), samo artikli po količini ----------
        g.MapPost("/artikli/{id:int}/korekcija", async (int id, KorekcijaUnos u, ClaimsPrincipal ko) =>
        {
            var napomena = P(u.Napomena);
            if (napomena is null) return Greska("Upišite razlog korekcije (npr. inventura, oštećeno, greška pri unosu).");
            await using var c = Veza();
            await c.OpenAsync();
            await using DbTransaction tx = await c.BeginTransactionAsync();
            var a = await c.QuerySingleOrDefaultAsync<Artikal>($"SELECT {ArtikalKolone} FROM dbo.Materijal WITH (UPDLOCK) WHERE Id = @id", new { id }, tx);
            if (a is null) return Results.NotFound(new { greska = "Artikal ne postoji." });
            if (a.VodiSeSN) return Greska("Artikal se vodi po serijskom broju: komad se dodaje prijemom, a uklanja otpisom.");
            var razlika = u.NovoStanje - a.Stanje;
            if (razlika == 0) return Greska("Novo stanje je isto kao trenutno.");
            await Knjizenje.PromijeniStanjeAsync(c, tx, id, razlika, "korekcija", Ko(ko), null, null, null, null, napomena);
            await tx.CommitAsync();
            return Results.Ok();
        }).RequireAuthorization(Politike.Administrator);

        // ---------- Otpis komada (artikli po SN) ----------
        g.MapPost("/komadi/{kid:int}/otpis", async (int kid, NapomenaUnos u, ClaimsPrincipal ko) =>
        {
            var napomena = P(u.Napomena);
            if (napomena is null) return Greska("Upišite razlog otpisa.");
            await using var c = Veza();
            await c.OpenAsync();
            await using DbTransaction tx = await c.BeginTransactionAsync();
            var k = await c.QuerySingleOrDefaultAsync<(int MaterijalId, string SerijskiBroj, string Status)>(
                "SELECT MaterijalId, SerijskiBroj, Status FROM dbo.MagacinKomad WITH (UPDLOCK) WHERE Id = @kid", new { kid }, tx);
            if (k.SerijskiBroj is null) return Results.NotFound(new { greska = "Komad ne postoji." });
            if (k.Status != "na_stanju") return Greska("Otpisati se može samo komad koji je na stanju.");
            await c.ExecuteAsync("UPDATE dbo.MagacinKomad SET Status = 'otpisan' WHERE Id = @kid", new { kid }, tx);
            await Knjizenje.PromijeniStanjeAsync(c, tx, k.MaterijalId, -1, "otpis", Ko(ko), null, null, k.SerijskiBroj, null, napomena);
            await tx.CommitAsync();
            return Results.Ok();
        }).RequireAuthorization(Politike.Administrator);

        // ---------- Kartica artikla ----------
        g.MapGet("/artikli/{id:int}/kartica", async (int id) =>
        {
            await using var c = Veza();
            return Results.Ok(await c.QueryAsync<PromjenaMagacina>(@"
                SELECT TOP 500 Vrijeme, Korisnik, Vrsta, Kolicina, StanjePoslije, NalogId, NalogBroj, SerijskiBroj, Dokument, Napomena
                FROM dbo.MagacinPromjena WHERE MaterijalId = @id ORDER BY Vrijeme DESC, Id DESC", new { id }));
        }).RequireAuthorization(Politike.Administrator);

        // ---------- Utrošeno van magacina ----------
        const string vanUslov = @"(nm.MaterijalId IS NULL OR (m.VodiSeSN = 1 AND nm.KomadId IS NULL)) AND nm.VanMagacinaRijeseno = 0";

        g.MapGet("/van-magacina", async () =>
        {
            await using var c = Veza();
            return Results.Ok(await c.QueryAsync<StavkaVanMagacina>($@"
                SELECT nm.Id, n.Id AS NalogId, n.Broj AS NalogBroj, n.DatumIzdavanja, n.KlijentNaziv, nm.MaterijalId, nm.Naziv, nm.PN, nm.SerijskiBroj, nm.Kolicina
                FROM dbo.NalogMaterijal nm JOIN dbo.Nalog n ON n.Id = nm.NalogId LEFT JOIN dbo.Materijal m ON m.Id = nm.MaterijalId
                WHERE {vanUslov}
                ORDER BY n.DatumIzdavanja DESC, n.Id DESC"));
        }).RequireAuthorization(Politike.Administrator);

        // Poveži stavku s artiklom (i komadom po SN) → skida se sa stanja
        g.MapPost("/van-magacina/{nmId:int}/povezi", async (int nmId, PoveziUnos u, ClaimsPrincipal ko) =>
        {
            await using var c = Veza();
            await c.OpenAsync();
            await using DbTransaction tx = await c.BeginTransactionAsync();
            var s = await c.QuerySingleOrDefaultAsync<StavkaVanMagacina>($@"
                SELECT nm.Id, n.Id AS NalogId, n.Broj AS NalogBroj, n.DatumIzdavanja, n.KlijentNaziv, nm.MaterijalId, nm.Naziv, nm.PN, nm.SerijskiBroj, nm.Kolicina
                FROM dbo.NalogMaterijal nm WITH (UPDLOCK) JOIN dbo.Nalog n ON n.Id = nm.NalogId LEFT JOIN dbo.Materijal m ON m.Id = nm.MaterijalId
                WHERE nm.Id = @nmId AND {vanUslov}", new { nmId }, tx);
            if (s is null) return Results.NotFound(new { greska = "Stavka više nije na listi (već povezana ili razriješena)." });
            var a = await c.QuerySingleOrDefaultAsync<Artikal>($"SELECT {ArtikalKolone} FROM dbo.Materijal WHERE Id = @id", new { id = u.MaterijalId }, tx);
            if (a is null) return Greska("Izabrani artikal ne postoji.");

            var upozorenje = (string?)null;
            if (!a.VodiSeSN)
            {
                var kol = s.Kolicina is > 0 ? s.Kolicina.Value : 1;
                await c.ExecuteAsync("UPDATE dbo.NalogMaterijal SET MaterijalId = @m, Naziv = @n, PN = COALESCE(@pn, PN), Kolicina = @kol, KomadId = NULL WHERE Id = @nmId",
                    new { m = a.Id, n = a.Naziv, pn = a.PN, kol, nmId }, tx);
                var (stanje, _) = await Knjizenje.PromijeniStanjeAsync(c, tx, a.Id, -kol, "utrosak", Ko(ko), s.NalogId, s.NalogBroj, null, null, "Povezano iz liste „Utrošeno van magacina”");
                if (stanje < 0) upozorenje = $"{a.Naziv}: stanje je sada {stanje:0.##} (u minusu).";
            }
            else
            {
                var sn = P(u.SerijskiBroj) ?? P(s.SerijskiBroj);
                if (sn is null) return Greska("Izaberite serijski broj komada.");
                var k = await c.QuerySingleOrDefaultAsync<(int Id, string Status)>(
                    "SELECT Id, Status FROM dbo.MagacinKomad WITH (UPDLOCK) WHERE MaterijalId = @m AND SerijskiBroj = @sn", new { m = a.Id, sn }, tx);
                if (k.Status is null) return Greska($"Komad {sn} ne postoji kod artikla „{a.Naziv}”. Prvo ga primite u magacin.");
                if (k.Status != "na_stanju") return Greska($"Komad {sn} nije na stanju (već utrošen ili otpisan).");
                await c.ExecuteAsync("UPDATE dbo.MagacinKomad SET Status = 'utrosen', NalogId = @nalogId WHERE Id = @kid", new { nalogId = s.NalogId, kid = k.Id }, tx);
                await c.ExecuteAsync("UPDATE dbo.NalogMaterijal SET MaterijalId = @m, Naziv = @n, PN = COALESCE(@pn, PN), SerijskiBroj = @sn, KomadId = @kid, Kolicina = 1 WHERE Id = @nmId",
                    new { m = a.Id, n = a.Naziv, pn = a.PN, sn, kid = k.Id, nmId }, tx);
                await Knjizenje.PromijeniStanjeAsync(c, tx, a.Id, -1, "utrosak", Ko(ko), s.NalogId, s.NalogBroj, sn, null, "Povezano iz liste „Utrošeno van magacina”");
            }
            await tx.CommitAsync();
            return Results.Ok(new { upozorenje });
        }).RequireAuthorization(Politike.Administrator);

        // Označi kao riješeno (npr. dio donio klijent) – ništa se ne skida sa stanja
        g.MapPost("/van-magacina/{nmId:int}/rijeseno", async (int nmId, NapomenaUnos u) =>
        {
            var napomena = P(u.Napomena);
            if (napomena is null) return Greska("Upišite napomenu (npr. „dio donio klijent”).");
            await using var c = Veza();
            var n = await c.ExecuteAsync("UPDATE dbo.NalogMaterijal SET VanMagacinaRijeseno = 1, VanMagacinaNapomena = @napomena WHERE Id = @nmId", new { nmId, napomena });
            return n == 0 ? Results.NotFound(new { greska = "Stavka ne postoji." }) : Results.Ok();
        }).RequireAuthorization(Politike.Administrator);

        // Sažetak za početnu stranicu magacina
        g.MapGet("/sazetak", async () =>
        {
            await using var c = Veza();
            var r = await c.QuerySingleAsync<(int UMinusu, int IspodMinimuma, int VanMagacina)>($@"
                SELECT (SELECT COUNT(*) FROM dbo.Materijal WHERE Aktivan = 1 AND Stanje < 0),
                       (SELECT COUNT(*) FROM dbo.Materijal WHERE Aktivan = 1 AND MinKolicina IS NOT NULL AND Stanje >= 0 AND Stanje < MinKolicina),
                       (SELECT COUNT(*) FROM dbo.NalogMaterijal nm LEFT JOIN dbo.Materijal m ON m.Id = nm.MaterijalId WHERE {vanUslov})");
            return Results.Ok(new { uMinusu = r.UMinusu, ispodMinimuma = r.IspodMinimuma, vanMagacina = r.VanMagacina });
        }).RequireAuthorization(Politike.Administrator);
    }
}

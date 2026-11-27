using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Prijava;

namespace Tehnotim.RadniNalozi.Potvrda;

public class OvlastenoLice
{
    public int Id { get; set; }
    public int KlijentId { get; set; }
    public string ImePrezime { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Funkcija { get; set; }
    public bool SveJedinice { get; set; }
    public bool Aktivan { get; set; }
    public int BrojNaloga { get; set; }
    public List<int> Jedinice { get; set; } = [];
}

public class OvlastenoLiceUnos
{
    public string? ImePrezime { get; set; }
    public string? Email { get; set; }
    public string? Funkcija { get; set; }
    public bool SveJedinice { get; set; } = true;
    public bool Aktivan { get; set; } = true;
    public List<int>? Jedinice { get; set; }
}

/// <summary>
/// Ovlaštena lica klijenta: osobe kojima se nalog šalje na elektronsku potvrdu.
/// Čitanje: svaki korisnik (treba formi naloga). Izmjene: kancelarija i administrator.
/// </summary>
public static class OvlastenaLicaApi
{
    private static readonly Regex EmailRx = new(@"^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$", RegexOptions.Compiled);

    public static async Task<List<OvlastenoLice>> ZaKlijentaAsync(SqlConnection c, int klijentId)
    {
        var lica = (await c.QueryAsync<OvlastenoLice>(@"
            SELECT l.Id, l.KlijentId, l.ImePrezime, l.Email, l.Funkcija, l.SveJedinice, l.Aktivan,
                   (SELECT COUNT(*) FROM dbo.Nalog n WHERE n.PotvrdaLiceId = l.Id) AS BrojNaloga
            FROM dbo.OvlastenoLice l WHERE l.KlijentId = @klijentId
            ORDER BY l.Aktivan DESC, l.ImePrezime", new { klijentId })).ToList();
        var veze = await c.QueryAsync<(int LiceId, int JedinicaId)>(@"
            SELECT v.OvlastenoLiceId, v.PoslovnaJedinicaId FROM dbo.OvlastenoLiceJedinica v
            JOIN dbo.OvlastenoLice l ON l.Id = v.OvlastenoLiceId WHERE l.KlijentId = @klijentId", new { klijentId });
        foreach (var v in veze) lica.FirstOrDefault(l => l.Id == v.LiceId)?.Jedinice.Add(v.JedinicaId);
        return lica;
    }

    public static void MapOvlastenaLica(this WebApplication app, string cs)
    {
        IResult Greska(string p) => Results.BadRequest(new { greska = p });

        app.MapGet("/api/klijenti/{klijentId:int}/ovlastena-lica", async (int klijentId) =>
        {
            await using var c = new SqlConnection(cs);
            return Results.Ok(await ZaKlijentaAsync(c, klijentId));
        }).RequireAuthorization(Politike.Korisnik);

        async Task<IResult> Sacuvaj(int? id, int? klijentIdNovi, OvlastenoLiceUnos u)
        {
            var ime = (u.ImePrezime ?? "").Trim();
            var email = (u.Email ?? "").Trim();
            if (ime.Length == 0) return Greska("Upišite ime i prezime.");
            if (!EmailRx.IsMatch(email)) return Greska("Upišite ispravnu e-mail adresu.");
            var jedinice = (u.Jedinice ?? []).Distinct().ToList();
            if (!u.SveJedinice && jedinice.Count == 0) return Greska("Izaberite bar jednu poslovnu jedinicu ili označite „sve jedinice”.");

            await using var c = new SqlConnection(cs);
            await c.OpenAsync();
            await using var tx = await c.BeginTransactionAsync();

            var klijentId = klijentIdNovi
                ?? await c.ExecuteScalarAsync<int?>("SELECT KlijentId FROM dbo.OvlastenoLice WHERE Id = @id", new { id }, tx);
            if (klijentId is null) return Results.NotFound(new { greska = "Ovlašteno lice ne postoji." });
            if (klijentIdNovi is not null && !await c.ExecuteScalarAsync<bool>(
                    "SELECT CAST(COUNT(*) AS bit) FROM dbo.Klijent WHERE Id = @klijentId", new { klijentId }, tx))
                return Results.NotFound(new { greska = "Klijent ne postoji." });
            if (!u.SveJedinice)
            {
                var tudje = await c.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM dbo.PoslovnaJedinica WHERE Id IN @jedinice AND KlijentId <> @klijentId", new { jedinice, klijentId }, tx);
                var postoje = await c.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM dbo.PoslovnaJedinica WHERE Id IN @jedinice", new { jedinice }, tx);
                if (tudje > 0 || postoje != jedinice.Count) return Greska("Izabrana jedinica ne pripada ovom klijentu.");
            }

            var p = new { id, klijentId, ime, email, funkcija = string.IsNullOrWhiteSpace(u.Funkcija) ? null : u.Funkcija.Trim(), sve = u.SveJedinice, u.Aktivan };
            int liceId;
            if (id is null)
                liceId = await c.ExecuteScalarAsync<int>(@"
                    INSERT INTO dbo.OvlastenoLice (KlijentId, ImePrezime, Email, Funkcija, SveJedinice, Aktivan)
                    OUTPUT INSERTED.Id VALUES (@klijentId, @ime, @email, @funkcija, @sve, @Aktivan)", p, tx);
            else
            {
                liceId = id.Value;
                await c.ExecuteAsync(@"UPDATE dbo.OvlastenoLice SET ImePrezime = @ime, Email = @email, Funkcija = @funkcija,
                                       SveJedinice = @sve, Aktivan = @Aktivan WHERE Id = @id", p, tx);
            }
            await c.ExecuteAsync("DELETE FROM dbo.OvlastenoLiceJedinica WHERE OvlastenoLiceId = @liceId", new { liceId }, tx);
            if (!u.SveJedinice)
                foreach (var j in jedinice)
                    await c.ExecuteAsync("INSERT INTO dbo.OvlastenoLiceJedinica (OvlastenoLiceId, PoslovnaJedinicaId) VALUES (@liceId, @j)", new { liceId, j }, tx);
            await tx.CommitAsync();
            return Results.Ok(new { id = liceId });
        }

        app.MapPost("/api/klijenti/{klijentId:int}/ovlastena-lica", (int klijentId, OvlastenoLiceUnos u) => Sacuvaj(null, klijentId, u))
            .RequireAuthorization(Politike.Kancelarija);
        app.MapPut("/api/ovlastena-lica/{id:int}", (int id, OvlastenoLiceUnos u) => Sacuvaj(id, null, u))
            .RequireAuthorization(Politike.Kancelarija);

        // Brisanje samo ako lice nije korišteno ni na jednom nalogu; inače se isključi (Aktivan = ne)
        app.MapDelete("/api/ovlastena-lica/{id:int}", async (int id) =>
        {
            await using var c = new SqlConnection(cs);
            try
            {
                var n = await c.ExecuteAsync("DELETE FROM dbo.OvlastenoLice WHERE Id = @id", new { id });
                return n == 0 ? Results.NotFound(new { greska = "Ovlašteno lice ne postoji." }) : Results.Ok();
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                return Results.Conflict(new { greska = "Ovo lice je već potvrđivalo naloge, pa se ne briše – isključite ga (skinite oznaku „aktivan”)." });
            }
        }).RequireAuthorization(Politike.Kancelarija);
    }
}

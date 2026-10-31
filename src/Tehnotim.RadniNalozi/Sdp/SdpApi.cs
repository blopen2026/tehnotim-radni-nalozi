using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Prijava;

namespace Tehnotim.RadniNalozi.Sdp;

public record SdpPostavkeUnos(string? Url, string? ApiKljuc, bool PreskociCertifikat);

public class Sektor
{
    public int Id { get; set; }
    public string Naziv { get; set; } = "";
    public string? SdpTehnicarId { get; set; }
    public string? SdpTehnicarNaziv { get; set; }
    public string? SdpGrupaId { get; set; }
    public string? SdpGrupaNaziv { get; set; }
    public bool Aktivan { get; set; } = true;
    public int[] Clanovi { get; set; } = [];
}

/// <summary>Podešavanje veze sa SDP-om (administrator) i sektori servisera.</summary>
public static class SdpApi
{
    public static void MapSdp(this WebApplication app, string cs)
    {
        SqlConnection Veza() => new(cs);
        IResult Greska(string p) => Results.BadRequest(new { greska = p });

        // ---------- Veza sa SDP-om (samo administrator) ----------
        var sdp = app.MapGroup("/api/sdp").RequireAuthorization(Politike.Administrator);

        sdp.MapGet("/postavke", async () =>
        {
            var p = await SdpVeza.PostavkeAsync(cs);
            return Results.Ok(new { url = p.SdpUrl, kljucPostavljen = !string.IsNullOrWhiteSpace(p.SdpApiKljuc), preskociCertifikat = p.SdpPreskociCertifikat });
        });

        sdp.MapPut("/postavke", async (SdpPostavkeUnos u) =>
        {
            var url = (u.Url ?? "").Trim().TrimEnd('/');
            if (url != "" && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http")))
                return Greska("Adresa mora biti u obliku https://server (npr. https://TEST-NALOZI).");
            await using var c = Veza();
            await c.ExecuteAsync(@"UPDATE dbo.Postavke SET SdpUrl = @url, SdpPreskociCertifikat = @cert,
                                   SdpApiKljuc = CASE WHEN @kljuc IS NULL THEN SdpApiKljuc ELSE @kljuc END WHERE Id = 1",
                new { url = url == "" ? null : url, cert = u.PreskociCertifikat, kljuc = string.IsNullOrWhiteSpace(u.ApiKljuc) ? null : u.ApiKljuc.Trim() });
            return Results.Ok();
        });

        sdp.MapPost("/provjera", async () =>
        {
            try
            {
                var k = await SdpVeza.GetAsync(cs, "/api/v3/requests", new { list_info = new { row_count = 1, sort_field = "id", sort_order = "desc" } });
                string? zadnji = null;
                if (k.TryGetProperty("requests", out var z) && z.GetArrayLength() > 0)
                    zadnji = $"{SdpVeza.Tekst(z[0], "id")} – {SdpVeza.Tekst(z[0], "subject")}";
                return Results.Ok(new { ok = true, poruka = zadnji is null ? "Veza radi." : $"Veza radi. Zadnji zahtjev: {zadnji}" });
            }
            catch (SdpGreska ex) { return Results.Ok(new { ok = false, poruka = ex.Message }); }
        });

        sdp.MapGet("/tehnicari", async () =>
        {
            try { return Results.Ok(await SdpVeza.ListaAsync(cs, "tehnicari")); }
            catch (SdpGreska ex) { return Greska(ex.Message); }
        });

        sdp.MapGet("/grupe", async () =>
        {
            try { return Results.Ok(await SdpVeza.ListaAsync(cs, "grupe")); }
            catch (SdpGreska ex) { return Greska(ex.Message); }
        });

        // ---------- Sektori ----------
        var sektori = app.MapGroup("/api/sektori").RequireAuthorization(Politike.Korisnik);

        sektori.MapGet("", async () =>
        {
            await using var c = Veza();
            var lista = (await c.QueryAsync<Sektor>(@"SELECT Id, Naziv, SdpTehnicarId, SdpTehnicarNaziv, SdpGrupaId, SdpGrupaNaziv, Aktivan
                                                      FROM dbo.Sektor ORDER BY Aktivan DESC, Naziv")).ToList();
            var clanovi = (await c.QueryAsync<(int SektorId, int KorisnikId)>("SELECT SektorId, KorisnikId FROM dbo.KorisnikSektor")).ToList();
            foreach (var s in lista) s.Clanovi = clanovi.Where(x => x.SektorId == s.Id).Select(x => x.KorisnikId).ToArray();
            return Results.Ok(lista);
        });

        async Task<IResult> SacuvajSektor(int? id, Sektor s)
        {
            if (string.IsNullOrWhiteSpace(s.Naziv)) return Greska("Upišite naziv sektora.");
            static string? P(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim();
            var param = new { id, Naziv = s.Naziv.Trim(), SdpTehnicarId = P(s.SdpTehnicarId), SdpTehnicarNaziv = P(s.SdpTehnicarNaziv),
                              SdpGrupaId = P(s.SdpGrupaId), SdpGrupaNaziv = P(s.SdpGrupaNaziv), s.Aktivan };
            await using var c = Veza();
            try
            {
                var n = await c.ExecuteAsync(id is null
                    ? @"INSERT INTO dbo.Sektor (Naziv, SdpTehnicarId, SdpTehnicarNaziv, SdpGrupaId, SdpGrupaNaziv, Aktivan)
                        VALUES (@Naziv, @SdpTehnicarId, @SdpTehnicarNaziv, @SdpGrupaId, @SdpGrupaNaziv, @Aktivan)"
                    : @"UPDATE dbo.Sektor SET Naziv = @Naziv, SdpTehnicarId = @SdpTehnicarId, SdpTehnicarNaziv = @SdpTehnicarNaziv,
                        SdpGrupaId = @SdpGrupaId, SdpGrupaNaziv = @SdpGrupaNaziv, Aktivan = @Aktivan WHERE Id = @id", param);
                return n == 0 ? Results.NotFound(new { greska = "Sektor više ne postoji." }) : Results.Ok();
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                return Results.Conflict(new { greska = "Već postoji sektor s tim nazivom." });
            }
        }

        sektori.MapPost("", (Sektor s) => SacuvajSektor(null, s)).RequireAuthorization(Politike.Administrator);
        sektori.MapPut("/{id:int}", (int id, Sektor s) => SacuvajSektor(id, s)).RequireAuthorization(Politike.Administrator);

        sektori.MapDelete("/{id:int}", async (int id) =>
        {
            await using var c = Veza();
            try
            {
                return await c.ExecuteAsync("DELETE FROM dbo.Sektor WHERE Id = @id", new { id }) == 0
                    ? Results.NotFound(new { greska = "Sektor više ne postoji." }) : Results.Ok();
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                return Results.Conflict(new { greska = "Sektor je upisan u nalozima, pa se ne može obrisati – isključite ga (Aktivan)." });
            }
        }).RequireAuthorization(Politike.Administrator);
    }
}

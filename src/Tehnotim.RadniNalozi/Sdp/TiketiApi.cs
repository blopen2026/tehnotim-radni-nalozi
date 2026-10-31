using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Prijava;

namespace Tehnotim.RadniNalozi.Sdp;

public class TiketUListi
{
    public string Id { get; set; } = "";
    public string? Naslov { get; set; }
    public string? Site { get; set; }
    public string? Requester { get; set; }
    public string? Tehnicar { get; set; }
    public string? Grupa { get; set; }
    public int? SektorId { get; set; }
    public string? Sektor { get; set; }
    public string? Status { get; set; }
    public DateTime? Kreirano { get; set; }
}

/// <summary>Podaci za novi nalog izvučeni iz tiketa.</summary>
public class NacrtIzTiketa
{
    public string Interni { get; set; } = "";
    public string? Eksterni { get; set; }
    public string? Naslov { get; set; }
    public DateTime? ZahtjevVrijeme { get; set; }
    public string? OpisZahtjeva { get; set; }
    public string? UredjajPredao { get; set; }
    public string? InventurniBroj { get; set; }

    public string? SiteId { get; set; }
    public string? SiteNaziv { get; set; }
    public string? KlijentNaziv { get; set; }
    public bool KlijentZapamcen { get; set; }

    public string? RequesterId { get; set; }
    public string? RequesterNaziv { get; set; }
    public string? RequesterEmail { get; set; }
    public bool ZajednickiRequester { get; set; }   // RGU/Redmine: jedan requester za sve jedinice
    public string? JedinicaNaziv { get; set; }
    public bool JedinicaZapamcena { get; set; }
    public string? JedinicaIzTeksta { get; set; }    // npr. „Kalinovik PJ” iz Redmine maila

    public string? Tehnicar { get; set; }
    public string? Grupa { get; set; }
    public int? SektorId { get; set; }
    public string? Sektor { get; set; }
    public int[] NosiociSektora { get; set; } = [];
    public string? PostojeciNalog { get; set; }      // ako za ovaj tiket već postoji nalog
    public int? PostojeciNalogId { get; set; }
}

public class SdpSiteVeza { public string SdpSiteId { get; set; } = ""; public string? Naziv { get; set; } public int KlijentId { get; set; } public string KlijentNaziv { get; set; } = ""; }
public class SdpRequesterVeza
{
    public string SdpRequesterId { get; set; } = ""; public string? Naziv { get; set; } public string? Email { get; set; }
    public int KlijentId { get; set; } public string KlijentNaziv { get; set; } = ""; public int PoslovnaJedinicaId { get; set; } public string JedinicaNaziv { get; set; } = "";
}

/// <summary>Veza naloga s tiketima: lista otvorenih tiketa bez naloga, nacrt naloga iz tiketa, zapamćene veze.</summary>
public static class TiketiApi
{
    private static readonly HashSet<string> Zatvoreni = new(StringComparer.OrdinalIgnoreCase) { "Closed", "Resolved", "Canceled", "Cancelled" };

    private class PostojeciNalogRed { public int Id { get; set; } public string Broj { get; set; } = ""; }
    private class Sek { public int Id { get; set; } public string Naziv { get; set; } = ""; public string? SdpTehnicarId { get; set; } public string? SdpGrupaId { get; set; } }

    private static string? Pod(JsonElement e, string objekat, string polje) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(objekat, out var o) && o.ValueKind == JsonValueKind.Object ? SdpVeza.Tekst(o, polje) : null;

    private static DateTime? Vrijeme(JsonElement e, string polje) =>
        long.TryParse(Pod(e, polje, "value"), out var ms) ? DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().DateTime : null;

    private static Sek? SektorTiketa(List<Sek> sektori, JsonElement t)
    {
        var teh = Pod(t, "technician", "id");
        var grp = Pod(t, "group", "id");
        return (teh is null ? null : sektori.FirstOrDefault(s => s.SdpTehnicarId == teh))
            ?? (grp is null ? null : sektori.FirstOrDefault(s => s.SdpGrupaId == grp));
    }

    public static void MapTiketi(this WebApplication app, string cs)
    {
        SqlConnection Veza() => new(cs);
        var tiketi = app.MapGroup("/api/tiketi").RequireAuthorization(Politike.Korisnik);

        // ---------- Otvoreni tiketi bez naloga ----------
        tiketi.MapGet("", async (bool? svi, ClaimsPrincipal ko) =>
        {
            await using var c = Veza();
            var ja = await c.QuerySingleOrDefaultAsync<(int Id, string Uloga)>(
                "SELECT Id, Uloga FROM dbo.Korisnik WHERE KorisnickiNalog = @n", new { n = ko.Identity?.Name });
            var mojiSektori = (await c.QueryAsync<int>("SELECT SektorId FROM dbo.KorisnikSektor WHERE KorisnikId = @id", new { id = ja.Id })).ToArray();
            var sektori = (await c.QueryAsync<Sek>("SELECT Id, Naziv, SdpTehnicarId, SdpGrupaId FROM dbo.Sektor WHERE Aktivan = 1")).ToList();
            var saNalogom = (await c.QueryAsync<string>(
                "SELECT PozivInterni FROM dbo.Nalog WHERE PozivInterni IS NOT NULL UNION SELECT CAST(SdpZahtjevId AS nvarchar(30)) FROM dbo.Nalog WHERE SdpZahtjevId IS NOT NULL"))
                .Select(x => x.Trim()).ToHashSet();

            // Serviser sa sektorom po pravilu vidi samo tikete svog sektora
            var podrazumijevanoSvi = ja.Uloga != Uloge.Serviser || mojiSektori.Length == 0;
            var prikaziSve = svi ?? podrazumijevanoSvi;

            var lista = new List<TiketUListi>();
            try
            {
                for (var start = 1; start <= 201; start += 100)
                {
                    var k = await SdpVeza.GetAsync(cs, "/api/v3/requests",
                        new { list_info = new { row_count = 100, start_index = start, sort_field = "id", sort_order = "desc" } });
                    if (!k.TryGetProperty("requests", out var zahtjevi) || zahtjevi.ValueKind != JsonValueKind.Array) break;
                    foreach (var t in zahtjevi.EnumerateArray())
                    {
                        var id = SdpVeza.Tekst(t, "id");
                        var status = Pod(t, "status", "name");
                        if (id is null || saNalogom.Contains(id) || (status is not null && Zatvoreni.Contains(status))) continue;
                        var sek = SektorTiketa(sektori, t);
                        if (!prikaziSve && (sek is null || !mojiSektori.Contains(sek.Id))) continue;
                        lista.Add(new TiketUListi
                        {
                            Id = id, Naslov = SdpVeza.Tekst(t, "subject"), Site = Pod(t, "site", "name"), Requester = Pod(t, "requester", "name"),
                            Tehnicar = Pod(t, "technician", "name"), Grupa = Pod(t, "group", "name"), SektorId = sek?.Id, Sektor = sek?.Naziv,
                            Status = status, Kreirano = Vrijeme(t, "created_time")
                        });
                    }
                    if (zahtjevi.GetArrayLength() < 100) break;
                }
            }
            catch (SdpGreska ex) { return Results.BadRequest(new { greska = ex.Message }); }

            return Results.Ok(new { tiketi = lista, prikaziSve, podrazumijevanoSvi, imaSektor = mojiSektori.Length > 0 });
        });

        // ---------- Nacrt naloga iz jednog tiketa ----------
        tiketi.MapGet("/{id:long}/nacrt", async (long id) =>
        {
            JsonElement t;
            try
            {
                var k = await SdpVeza.GetAsync(cs, $"/api/v3/requests/{id}");
                if (!k.TryGetProperty("request", out t)) return Results.NotFound(new { greska = $"Tiket {id} nije pronađen u ServiceDesk Plus-u." });
            }
            catch (SdpGreska ex) { return Results.BadRequest(new { greska = ex.Message }); }

            await using var c = Veza();
            var n = new NacrtIzTiketa
            {
                Interni = id.ToString(),
                Naslov = TekstTiketa.Latinica(SdpVeza.Tekst(t, "subject")),
                ZahtjevVrijeme = Vrijeme(t, "created_time"),
                SiteId = Pod(t, "site", "id"), SiteNaziv = Pod(t, "site", "name"),
                RequesterId = Pod(t, "requester", "id"), RequesterNaziv = Pod(t, "requester", "name"), RequesterEmail = Pod(t, "requester", "email_id"),
                Tehnicar = Pod(t, "technician", "name"), Grupa = Pod(t, "group", "name")
            };

            var postojeci = await c.QueryFirstOrDefaultAsync<PostojeciNalogRed>(
                "SELECT TOP 1 Id, Broj FROM dbo.Nalog WHERE SdpZahtjevId = @id OR PozivInterni = @s ORDER BY Id", new { id, s = id.ToString() });
            if (postojeci is not null) { n.PostojeciNalogId = postojeci.Id; n.PostojeciNalog = postojeci.Broj; }

            // Opis i podaci iz teksta
            var html = SdpVeza.Tekst(t, "description") ?? "";
            if (TekstTiketa.JeRedmine(html))
            {
                var (autor, opis) = TekstTiketa.Redmine(html);
                n.ZajednickiRequester = true;
                n.JedinicaIzTeksta = autor;
                n.OpisZahtjeva = opis;
                n.Eksterni = TekstTiketa.EksterniBroj(SdpVeza.Tekst(t, "subject"));
            }
            else
            {
                var tekst = TekstTiketa.Latinica(TekstTiketa.IzHtml(html));
                n.OpisZahtjeva = string.IsNullOrWhiteSpace(tekst) ? n.Naslov : $"{n.Naslov}\n\n{tekst}";
            }
            n.InventurniBroj = TekstTiketa.InventurniBroj(n.OpisZahtjeva ?? "");
            n.UredjajPredao = TekstTiketa.Potpisnik(n.OpisZahtjeva ?? "");

            // Klijent: zapamćena veza site → klijent, inače prijedlog po nazivu
            var klijenti = (await c.QueryAsync<(int Id, string Naziv, string? Skracenica)>("SELECT Id, Naziv, Skracenica FROM dbo.Klijent")).ToList();
            int? klijentId = n.SiteId is null ? null
                : await c.ExecuteScalarAsync<int?>("SELECT KlijentId FROM dbo.SdpSite WHERE SdpSiteId = @s", new { s = n.SiteId });
            n.KlijentZapamcen = klijentId is not null;
            if (klijentId is null)
            {
                var r = TekstTiketa.Rijeci(n.SiteNaziv).Union(TekstTiketa.Rijeci(n.RequesterNaziv)).ToHashSet();
                klijentId = klijenti
                    .Select(k => (k.Id, Bod: TekstTiketa.Rijeci(k.Naziv).Count(r.Contains)
                                             + (k.Skracenica is { Length: > 1 } sk && r.Contains(sk.ToLowerInvariant()) ? 2 : 0)))
                    .Where(x => x.Bod > 0).OrderByDescending(x => x.Bod).Select(x => (int?)x.Id).FirstOrDefault();
            }
            n.KlijentNaziv = klijenti.FirstOrDefault(k => k.Id == klijentId).Naziv;

            // Poslovna jedinica: zapamćeni requester (ako nije zajednički), inače prijedlog po nazivu requestera / tekstu
            if (klijentId is int kid)
            {
                var jedinice = (await c.QueryAsync<(int Id, string Naziv, string? Mjesto)>(
                    "SELECT Id, Naziv, Mjesto FROM dbo.PoslovnaJedinica WHERE KlijentId = @kid", new { kid })).ToList();
                if (!n.ZajednickiRequester && n.RequesterId is not null)
                {
                    var jid = await c.ExecuteScalarAsync<int?>(
                        "SELECT PoslovnaJedinicaId FROM dbo.SdpRequester WHERE SdpRequesterId = @r AND KlijentId = @kid", new { r = n.RequesterId, kid });
                    if (jid is not null) { n.JedinicaNaziv = jedinice.FirstOrDefault(j => j.Id == jid).Naziv; n.JedinicaZapamcena = n.JedinicaNaziv is not null; }
                }
                if (n.JedinicaNaziv is null)
                {
                    var izvor = n.JedinicaIzTeksta ?? n.RequesterNaziv;
                    var opste = TekstTiketa.Rijeci(n.KlijentNaziv).Union(TekstTiketa.Rijeci(n.SiteNaziv)).ToHashSet();
                    var r = TekstTiketa.Rijeci(izvor).Where(w => !opste.Contains(w)).ToHashSet();
                    if (r.Count > 0)
                        n.JedinicaNaziv = jedinice
                            .Select(j => (j.Naziv, Bod: TekstTiketa.Rijeci(j.Naziv).Union(TekstTiketa.Rijeci(j.Mjesto)).Count(r.Contains)))
                            .Where(x => x.Bod > 0).OrderByDescending(x => x.Bod).Select(x => x.Naziv).FirstOrDefault();
                }
            }

            // Sektor i nosioci iz sektora
            var sektori = (await c.QueryAsync<Sek>("SELECT Id, Naziv, SdpTehnicarId, SdpGrupaId FROM dbo.Sektor WHERE Aktivan = 1")).ToList();
            if (SektorTiketa(sektori, t) is { } sek)
            {
                n.SektorId = sek.Id; n.Sektor = sek.Naziv;
                n.NosiociSektora = (await c.QueryAsync<int>(@"
                    SELECT k.Id FROM dbo.KorisnikSektor ks JOIN dbo.Korisnik k ON k.Id = ks.KorisnikId
                    WHERE ks.SektorId = @s AND k.Aktivan = 1 AND k.NosilacNaloga = 1", new { s = sek.Id })).ToArray();
            }
            return Results.Ok(n);
        });

        // ---------- Zapamćene veze (pregled i brisanje: kancelarija/administrator) ----------
        tiketi.MapGet("/veze", async () =>
        {
            await using var c = Veza();
            var siteovi = await c.QueryAsync<SdpSiteVeza>(@"
                SELECT s.SdpSiteId, s.Naziv, s.KlijentId, k.Naziv AS KlijentNaziv FROM dbo.SdpSite s JOIN dbo.Klijent k ON k.Id = s.KlijentId ORDER BY s.Naziv");
            var requesteri = await c.QueryAsync<SdpRequesterVeza>(@"
                SELECT r.SdpRequesterId, r.Naziv, r.Email, r.KlijentId, k.Naziv AS KlijentNaziv, r.PoslovnaJedinicaId, j.Naziv AS JedinicaNaziv
                FROM dbo.SdpRequester r JOIN dbo.Klijent k ON k.Id = r.KlijentId JOIN dbo.PoslovnaJedinica j ON j.Id = r.PoslovnaJedinicaId
                ORDER BY k.Naziv, r.Naziv");
            return Results.Ok(new { siteovi, requesteri });
        }).RequireAuthorization(Politike.Kancelarija);

        tiketi.MapDelete("/veze/site/{sid}", async (string sid) =>
        {
            await using var c = Veza();
            await c.ExecuteAsync("DELETE FROM dbo.SdpSite WHERE SdpSiteId = @sid", new { sid });
            return Results.Ok();
        }).RequireAuthorization(Politike.Kancelarija);

        tiketi.MapDelete("/veze/requester/{rid}", async (string rid) =>
        {
            await using var c = Veza();
            await c.ExecuteAsync("DELETE FROM dbo.SdpRequester WHERE SdpRequesterId = @rid", new { rid });
            return Results.Ok();
        }).RequireAuthorization(Politike.Kancelarija);
    }

    /// <summary>Pri čuvanju naloga iz tiketa: zapamti site → klijent i (ako requester nije zajednički) requester → jedinica.</summary>
    public static async Task ZapamtiVezeAsync(System.Data.Common.DbConnection c, System.Data.Common.DbTransaction tx,
        SdpIzvorUnos s, int klijentId, int? jedinicaId, string? korisnik)
    {
        if (!string.IsNullOrWhiteSpace(s.SiteId))
            await c.ExecuteAsync(@"
                MERGE dbo.SdpSite AS t USING (SELECT @id AS SdpSiteId) AS x ON t.SdpSiteId = x.SdpSiteId
                WHEN MATCHED THEN UPDATE SET Naziv = @naziv, KlijentId = @k, Izmijenjeno = SYSUTCDATETIME(), Izmijenio = @korisnik
                WHEN NOT MATCHED THEN INSERT (SdpSiteId, Naziv, KlijentId, Izmijenio) VALUES (@id, @naziv, @k, @korisnik);",
                new { id = s.SiteId.Trim(), naziv = s.SiteNaziv, k = klijentId, korisnik }, tx);

        if (!s.ZajednickiRequester && !string.IsNullOrWhiteSpace(s.RequesterId) && jedinicaId is int jid)
            await c.ExecuteAsync(@"
                MERGE dbo.SdpRequester AS t USING (SELECT @id AS SdpRequesterId) AS x ON t.SdpRequesterId = x.SdpRequesterId
                WHEN MATCHED THEN UPDATE SET Naziv = @naziv, Email = @email, KlijentId = @k, PoslovnaJedinicaId = @j, Izmijenjeno = SYSUTCDATETIME(), Izmijenio = @korisnik
                WHEN NOT MATCHED THEN INSERT (SdpRequesterId, Naziv, Email, KlijentId, PoslovnaJedinicaId, Izmijenio) VALUES (@id, @naziv, @email, @k, @j, @korisnik);",
                new { id = s.RequesterId.Trim(), naziv = s.RequesterNaziv, email = s.RequesterEmail, k = klijentId, j = jid, korisnik }, tx);
    }
}

/// <summary>Podaci o tiketu iz kojeg je nalog napravljen (šalje forma pri prvom čuvanju).</summary>
public class SdpIzvorUnos
{
    public long? ZahtjevId { get; set; }
    public string? SiteId { get; set; }
    public string? SiteNaziv { get; set; }
    public string? RequesterId { get; set; }
    public string? RequesterNaziv { get; set; }
    public string? RequesterEmail { get; set; }
    public bool ZajednickiRequester { get; set; }
}

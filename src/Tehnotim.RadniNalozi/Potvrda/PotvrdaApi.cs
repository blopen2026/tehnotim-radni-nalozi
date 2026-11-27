using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Nalozi;
using Tehnotim.RadniNalozi.Pdf;
using Tehnotim.RadniNalozi.Prijava;
using Tehnotim.RadniNalozi.Sifarnici;

namespace Tehnotim.RadniNalozi.Potvrda;

public record SlanjePotvrdeUnos(int LiceId);
public record OdgovorKorisnikaUnos(string? Akcija, string? Tekst);
public record PostaPostavkeUnos(string? SmtpServer, int? SmtpPort, string? SmtpSigurnost, string? SmtpKorisnik, string? SmtpLozinka,
                                string? SmtpPosiljalac, string? SmtpPosiljalacIme, string? JavnaAdresa);
public record ProbaPosteUnos(string? Email);

/// <summary>
/// Elektronska potvrda naloga: nalog se e-mailom šalje ovlaštenom licu klijenta; ono preko linka pregleda nalog i potvrdi
/// (nalog postaje „Završen”) ili pošalje primjedbu (nalog se vraća u „U radu”).
/// Stranica potvrde (/potvrda/…) je jedina dostupna bez Windows prijave; pristup daje slučajan ključ iz linka.
/// </summary>
public static class PotvrdaApi
{
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string Hbr(string? s) => H(s).Replace("\r", "").Replace("\n", "<br>");
    private static string Vrijeme(DateTime? d) => d is null ? "" : d.Value.ToString("dd.MM.yyyy.") + " u " + d.Value.ToString("HH:mm");
    private const string Tirkizna = "#208293";

    private static byte[] Otisak(string kljuc) => SHA256.HashData(Encoding.UTF8.GetBytes(kljuc));

    private static string NoviKljuc() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string NoviKod()
    {
        const string slova = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var z = Enumerable.Range(0, 8).Select(_ => slova[RandomNumberGenerator.GetInt32(slova.Length)]).ToArray();
        return new string(z, 0, 4) + "-" + new string(z, 4, 4);
    }

    private static async Task<Postavke> FirmaAsync(SqlConnection c) =>
        await c.QuerySingleAsync<Postavke>("SELECT TarifaKm, NazivFirme, Adresa, Email, Web, Jib, Mb, ZiroRacun FROM dbo.Postavke WHERE Id = 1");

    private static async Task Istorija(SqlConnection c, int nalogId, string? korisnik, string opis, System.Data.IDbTransaction? tx = null) =>
        await c.ExecuteAsync("INSERT INTO dbo.NalogIzmjena (NalogId, Korisnik, Opis) VALUES (@nalogId, @korisnik, @opis)",
            new { nalogId, korisnik, opis = opis.Length > 400 ? opis[..400] + "…" : opis }, tx);

    public static void MapPotvrda(this WebApplication app, string cs)
    {
        IResult Greska(string p) => Results.BadRequest(new { greska = p });

        // ---------- Podešavanje slanja e-maila (administrator) ----------
        var posta = app.MapGroup("/api/posta").RequireAuthorization(Politike.Administrator);

        posta.MapGet("/postavke", async () =>
        {
            var p = await Posta.PostavkeAsync(cs);
            return Results.Ok(new
            {
                p.SmtpServer, p.SmtpPort, smtpSigurnost = p.SmtpSigurnost ?? "auto", p.SmtpKorisnik,
                lozinkaPostavljena = !string.IsNullOrEmpty(p.SmtpLozinka), p.SmtpPosiljalac, p.SmtpPosiljalacIme, p.JavnaAdresa
            });
        });

        posta.MapPut("/postavke", async (PostaPostavkeUnos u) =>
        {
            string? P(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            var javna = P(u.JavnaAdresa)?.TrimEnd('/');
            if (javna is not null && !(Uri.TryCreate(javna, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http")))
                return Greska("Javna adresa mora biti u obliku https://nalozi.firma.ba (ili http://TEST-NALOZI za test).");
            var sigurnost = u.SmtpSigurnost is "ssl" or "starttls" or "bez" ? u.SmtpSigurnost : "auto";
            if (u.SmtpPort is < 0 or > 65535) return Greska("Port nije ispravan.");
            await using var c = new SqlConnection(cs);
            await c.ExecuteAsync(@"
                UPDATE dbo.Postavke SET SmtpServer = @server, SmtpPort = @port, SmtpSigurnost = @sigurnost, SmtpKorisnik = @korisnik,
                       SmtpLozinka = CASE WHEN @lozinka IS NULL THEN SmtpLozinka ELSE @lozinka END,
                       SmtpPosiljalac = @posiljalac, SmtpPosiljalacIme = @posiljalacIme, JavnaAdresa = @javna
                WHERE Id = 1",
                new
                {
                    server = P(u.SmtpServer), port = u.SmtpPort is > 0 ? u.SmtpPort : null, sigurnost, korisnik = P(u.SmtpKorisnik),
                    lozinka = string.IsNullOrEmpty(u.SmtpLozinka) ? null : u.SmtpLozinka,
                    posiljalac = P(u.SmtpPosiljalac), posiljalacIme = P(u.SmtpPosiljalacIme), javna
                });
            return Results.Ok();
        });

        posta.MapPost("/proba", async (ProbaPosteUnos u) =>
        {
            if (string.IsNullOrWhiteSpace(u.Email)) return Greska("Upišite adresu na koju ide probni e-mail.");
            try
            {
                var p = await Posta.PostavkeAsync(cs);
                await Posta.PosaljiAsync(p, "", u.Email, "Probni e-mail – Radni nalozi",
                    $"<p>Ovo je probni e-mail aplikacije Radni nalozi ({H(p.NazivFirme)}).</p><p>Slanje e-maila radi.</p>",
                    "Ovo je probni e-mail aplikacije Radni nalozi. Slanje e-maila radi.");
                return Results.Ok(new { ok = true, poruka = $"Probni e-mail je poslan na {u.Email.Trim()}." });
            }
            catch (PostaGreska ex) { return Results.Ok(new { ok = false, poruka = ex.Message }); }
        });

        // Da li je slanje podešeno (forma naloga pokazuje upozorenje umjesto dugmeta)
        app.MapGet("/api/posta/stanje", async () =>
        {
            var p = await Posta.PostavkeAsync(cs);
            var nedostaje = Posta.StaNedostaje(p)
                ?? (string.IsNullOrWhiteSpace(p.JavnaAdresa) ? "Nije upisana javna adresa aplikacije (Šifarnici → Slanje e-maila i potvrda naloga)." : null);
            return Results.Ok(new { spremno = nedostaje is null, poruka = nedostaje });
        }).RequireAuthorization(Politike.Korisnik);

        // ---------- Slanje naloga na potvrdu ----------
        app.MapPost("/api/nalozi/{id:int}/potvrda", async (int id, SlanjePotvrdeUnos u, ClaimsPrincipal ko, IWebHostEnvironment env) =>
        {
            var postavke = await Posta.PostavkeAsync(cs);
            if (Posta.StaNedostaje(postavke) is string nedostaje) return Greska(nedostaje);
            if (string.IsNullOrWhiteSpace(postavke.JavnaAdresa))
                return Greska("Nije upisana javna adresa aplikacije (Šifarnici → Slanje e-maila i potvrda naloga).");

            var n = await NaloziApi.UcitajAsync(cs, id);
            if (n is null) return Results.NotFound(new { greska = "Nalog ne postoji." });
            if (n.Potvrdjeno is not null) return Greska("Korisnik je već potvrdio ovaj nalog.");
            if (n.PotpisKorisnik is not null) return Greska("Korisnik je već potpisao nalog na ekranu – potvrda nije potrebna.");
            if (string.IsNullOrWhiteSpace(n.Status)) return Greska("Prije slanja izaberite status naloga (obavljena, djelimično …) i sačuvajte nalog.");
            if (string.IsNullOrWhiteSpace(n.OpisIntervencije)) return Greska("Prije slanja upišite opis izvršene intervencije i sačuvajte nalog.");

            await using var c = new SqlConnection(cs);
            await c.OpenAsync();
            var lice = await c.QuerySingleOrDefaultAsync<OvlastenoLice>(
                "SELECT Id, KlijentId, ImePrezime, Email, Funkcija, SveJedinice, Aktivan FROM dbo.OvlastenoLice WHERE Id = @liceId",
                new { liceId = u.LiceId });
            if (lice is null || lice.KlijentId != n.KlijentId) return Greska("Izaberite ovlašteno lice ovog klijenta.");
            if (!lice.Aktivan) return Greska("Izabrano lice je isključeno (Klijenti → ovlaštena lica).");

            var kljuc = NoviKljuc();
            var sada = DateTime.Now;
            var korisnik = ko.FindFirstValue(Tvrdnje.Ime) ?? ko.Identity?.Name;
            int.TryParse(ko.FindFirstValue(Tvrdnje.KorisnikId), out var korisnikId);
            n.PotvrdaLiceId = lice.Id; n.PotvrdaIme = lice.ImePrezime; n.PotvrdaEmail = lice.Email;
            n.PotvrdaKod = NoviKod(); n.PotvrdaPoslano = sada; n.PotvrdaPoslao = korisnik;
            n.PotvrdaPrimjedba = null; n.PotvrdaPrimjedbaVrijeme = null;

            var firma = await FirmaAsync(c);
            await using var tx = await c.BeginTransactionAsync();
            var izmijenjeno = await c.ExecuteAsync(@"
                UPDATE dbo.Nalog SET PotvrdaLiceId = @liceId, PotvrdaIme = @ime, PotvrdaEmail = @email, PotvrdaKljucHash = @hash,
                       PotvrdaKod = @kod, PotvrdaPoslano = @sada, PotvrdaPoslao = @korisnik, PotvrdaPoslaoKorisnikId = @korisnikId,
                       PotvrdaPrimjedba = NULL, PotvrdaPrimjedbaVrijeme = NULL, PotvrdaAdresa = NULL, Faza = 'ceka_potpis'
                WHERE Id = @id AND Potvrdjeno IS NULL",
                new { id, liceId = lice.Id, ime = lice.ImePrezime, email = lice.Email, hash = Otisak(kljuc), kod = n.PotvrdaKod, sada, korisnik,
                      korisnikId = korisnikId > 0 ? (int?)korisnikId : null }, tx);
            if (izmijenjeno == 0) return Greska("Nalog je u međuvremenu potvrđen.");
            await Istorija(c, id, korisnik, $"Poslano na potvrdu: {lice.ImePrezime} <{lice.Email}>", tx);

            var (pdf, imePdf) = await PdfApi.NapraviAsync(cs, n, env.WebRootPath);
            var link = postavke.JavnaAdresa!.Trim().TrimEnd('/') + "/potvrda/" + kljuc;
            try
            {
                await Posta.PosaljiAsync(postavke, lice.ImePrezime, lice.Email,
                    $"Radni nalog {n.Broj} – molimo potvrdu ({n.KlijentNaziv})",
                    EmailZaPotvrdu(n, firma, link), EmailZaPotvrduTekst(n, firma, link), new PrilogPoste(imePdf, pdf));
            }
            catch (PostaGreska ex) { return Greska(ex.Message); }   // transakcija se poništava: nalog ostaje kakav je bio

            await tx.CommitAsync();
            return Results.Ok(new { poslano = sada, ime = lice.ImePrezime, email = lice.Email });
        }).RequireAuthorization(Politike.Korisnik);

        // ---------- Stranica potvrde za korisnika (bez prijave) ----------
        var javno = app.MapGroup("/potvrda").AllowAnonymous();

        javno.MapGet("/{kljuc}", async (string kljuc, IWebHostEnvironment env) =>
        {
            var n = await PoKljucuAsync(cs, kljuc);
            await using var c = new SqlConnection(cs);
            var firma = await FirmaAsync(c);
            var logo = Logo(env.WebRootPath);
            return Results.Content(StranicaPotvrde.Napravi(n, firma, logo), "text/html; charset=utf-8");
        });

        javno.MapGet("/{kljuc}/pdf", async (string kljuc, HttpContext http, IWebHostEnvironment env) =>
        {
            var n = await PoKljucuAsync(cs, kljuc);
            if (n is null) return Results.NotFound("Link više ne važi.");
            var (pdf, ime) = await PdfApi.NapraviAsync(cs, n, env.WebRootPath);
            http.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(ime)}";
            return Results.Bytes(pdf, "application/pdf");
        });

        javno.MapPost("/{kljuc}", async (string kljuc, OdgovorKorisnikaUnos u, HttpContext http, IWebHostEnvironment env) =>
        {
            var n = await PoKljucuAsync(cs, kljuc);
            if (n is null) return Results.NotFound(new { greska = "Link više ne važi. Možda vam je poslan noviji link – provjerite e-mail." });
            if (n.Potvrdjeno is not null) return Results.Ok(new { ok = true });
            if (n.PotvrdaPrimjedbaVrijeme is not null) return Greska("Za ovaj nalog ste već poslali primjedbu. Kad nalog bude ispravljen, dobićete novi link.");

            var hash = Otisak(kljuc);
            var sada = DateTime.Now;
            var postavke = await Posta.PostavkeAsync(cs);
            await using var c = new SqlConnection(cs);
            await c.OpenAsync();
            var firma = await FirmaAsync(c);
            var osoba = $"{n.PotvrdaIme} <{n.PotvrdaEmail}>";

            if (u.Akcija == "potvrdi")
            {
                var ip = http.Connection.RemoteIpAddress?.ToString();
                var red = await c.ExecuteAsync(@"
                    UPDATE dbo.Nalog SET Potvrdjeno = @sada, PotvrdaAdresa = @ip, Faza = 'zavrsen'
                    WHERE Id = @id AND PotvrdaKljucHash = @hash AND Potvrdjeno IS NULL AND PotvrdaPrimjedbaVrijeme IS NULL",
                    new { sada, ip, id = n.Id, hash });
                if (red == 0) return Results.Ok(new { ok = true });
                await Istorija(c, n.Id, osoba, "Korisnik potvrdio nalog preko linka iz e-maila");

                // Korisniku ide završni PDF (sa potvrdom) – za predaju službi finansija; greška ne poništava potvrdu
                var konacni = await NaloziApi.UcitajAsync(cs, n.Id);
                if (konacni is not null && n.PotvrdaEmail is not null)
                {
                    try
                    {
                        var (pdf, ime) = await PdfApi.NapraviAsync(cs, konacni, env.WebRootPath);
                        await Posta.PosaljiAsync(postavke, n.PotvrdaIme ?? "", n.PotvrdaEmail, $"Radni nalog {n.Broj} je potvrđen",
                            EmailHvala(konacni, firma), EmailHvalaTekst(konacni, firma), new PrilogPoste(ime, pdf));
                    }
                    catch (PostaGreska ex) { await Istorija(c, n.Id, "aplikacija", $"Završni PDF nije poslan korisniku: {ex.Message}"); }
                }
                // Tiket iz kojeg je nalog napravljen: napomena o završetku + PDF
                try { await Tehnotim.RadniNalozi.Sdp.PovratUTiket.PosaljiAsync(cs, n.Id, env.WebRootPath, osoba); }
                catch (Exception ex) { await Istorija(c, n.Id, "aplikacija", $"Upis u tiket nije uspio: {ex.Message}"); }
                return Results.Ok(new { ok = true });
            }

            if (u.Akcija == "primjedba")
            {
                var tekst = (u.Tekst ?? "").Trim();
                if (tekst.Length < 3) return Greska("Upišite primjedbu.");
                if (tekst.Length > 2000) tekst = tekst[..2000];
                var red = await c.ExecuteAsync(@"
                    UPDATE dbo.Nalog SET PotvrdaPrimjedba = @tekst, PotvrdaPrimjedbaVrijeme = @sada, Faza = 'u_radu'
                    WHERE Id = @id AND PotvrdaKljucHash = @hash AND Potvrdjeno IS NULL AND PotvrdaPrimjedbaVrijeme IS NULL",
                    new { tekst, sada, id = n.Id, hash });
                if (red == 0) return Results.Ok(new { ok = true });
                await Istorija(c, n.Id, osoba, "Korisnik poslao primjedbu: " + tekst);

                // Obavještenje nosiocu naloga i onome ko je poslao na potvrdu
                var primaoci = (await c.QueryAsync<(string? Ime, string? Email)>(@"
                    SELECT k.ImePrezime, k.Email FROM dbo.Korisnik k
                    WHERE k.Email IS NOT NULL AND k.Email <> '' AND k.Id IN (
                        SELECT NosilacKorisnikId FROM dbo.Nalog WHERE Id = @id
                        UNION SELECT PotvrdaPoslaoKorisnikId FROM dbo.Nalog WHERE Id = @id)", new { id = n.Id })).ToList();
                var linkNaloga = string.IsNullOrWhiteSpace(postavke.JavnaAdresa) ? null : postavke.JavnaAdresa.Trim().TrimEnd('/') + "/nalog.html?id=" + n.Id;
                foreach (var (ime, email) in primaoci.DistinctBy(p => p.Email!.Trim().ToLowerInvariant()))
                {
                    try
                    {
                        await Posta.PosaljiAsync(postavke, ime ?? "", email!, $"Primjedba korisnika na nalog {n.Broj} ({n.KlijentNaziv})",
                            $"<p><b>{H(n.PotvrdaIme)}</b> ({H(n.PotvrdaEmail)}) nije potvrdio/la radni nalog <b>{H(n.Broj)}</b> i poslao/la je primjedbu:</p>"
                            + $"<blockquote style=\"border-left:3px solid {Tirkizna};margin:0;padding:6px 12px;background:#f2f8f9\">{Hbr(tekst)}</blockquote>"
                            + $"<p>Nalog je vraćen u „U radu”. Kad ga ispravite, ponovo ga pošaljite na potvrdu.</p>"
                            + (linkNaloga is null ? "" : $"<p><a href=\"{H(linkNaloga)}\">Otvori nalog {H(n.Broj)}</a></p>"),
                            $"{n.PotvrdaIme} ({n.PotvrdaEmail}) nije potvrdio/la radni nalog {n.Broj} i poslao/la je primjedbu:\n\n{tekst}\n\n"
                            + "Nalog je vraćen u „U radu”. Kad ga ispravite, ponovo ga pošaljite na potvrdu." + (linkNaloga is null ? "" : "\n" + linkNaloga));
                    }
                    catch (PostaGreska ex) { await Istorija(c, n.Id, "aplikacija", $"Obavještenje o primjedbi nije poslano na {email}: {ex.Message}"); }
                }
                return Results.Ok(new { ok = true });
            }

            return Greska("Nepoznata akcija.");
        });
    }

    private static async Task<NalogDetalji?> PoKljucuAsync(string cs, string kljuc)
    {
        if (kljuc.Length is < 20 or > 100) return null;
        await using var c = new SqlConnection(cs);
        var id = await c.ExecuteScalarAsync<int?>("SELECT Id FROM dbo.Nalog WHERE PotvrdaKljucHash = @hash", new { hash = Otisak(kljuc) });
        return id is int i ? await NaloziApi.UcitajAsync(cs, i) : null;
    }

    private static string? logoBase64;
    private static string? Logo(string webRoot)
    {
        if (logoBase64 is not null) return logoBase64;
        var putanja = Path.Combine(webRoot, "img", "logo.png");
        return logoBase64 = File.Exists(putanja) ? Convert.ToBase64String(File.ReadAllBytes(putanja)) : null;
    }

    // ---------- Sadržaj e-mailova ----------

    private static string Uredjaji(NalogDetalji n) =>
        string.Join("; ", n.Uredjaji.Select(u => string.Join(" · ", new[] { u.Model, u.SerijskiBroj is null ? null : "SN " + u.SerijskiBroj, u.InventurniBroj is null ? null : "inv. " + u.InventurniBroj }
            .Where(s => !string.IsNullOrWhiteSpace(s)))).Where(s => s.Length > 0));

    private static List<(string Naziv, string? Vrijednost)> Sazetak(NalogDetalji n) =>
    [
        ("Radni nalog", n.Broj),
        ("Korisnik", n.KlijentNaziv + (string.IsNullOrWhiteSpace(n.Lokacija) ? "" : " – " + n.Lokacija)),
        ("Datum", n.ZavrsetakRada?.ToString("dd.MM.yyyy.") ?? n.DatumIzdavanja.ToString("dd.MM.yyyy.")),
        ("Vaš broj zahtjeva", n.PozivEksterni),
        ("Uređaj", Uredjaji(n)),
        ("Opis zahtjeva", n.OpisZahtjeva),
        ("Izvršeno", n.OpisIntervencije),
        ("Status", n.Status),
        ("Serviser", n.NosilacIme)
    ];

    private static string TabelaHtml(NalogDetalji n) =>
        "<table style=\"border-collapse:collapse;font-size:14px;margin:12px 0\">"
        + string.Concat(Sazetak(n).Where(r => !string.IsNullOrWhiteSpace(r.Vrijednost)).Select(r =>
            $"<tr><td style=\"padding:5px 14px 5px 0;color:#555;vertical-align:top;white-space:nowrap\">{H(r.Naziv)}</td><td style=\"padding:5px 0;vertical-align:top\">{Hbr(r.Vrijednost)}</td></tr>"))
        + "</table>";

    private static string TabelaTekst(NalogDetalji n) =>
        string.Join("\n", Sazetak(n).Where(r => !string.IsNullOrWhiteSpace(r.Vrijednost)).Select(r => $"{r.Naziv}: {r.Vrijednost}"));

    private static string Potpis(Postavke f) =>
        $"<p style=\"color:{Tirkizna};font-size:13px;border-top:1px solid {Tirkizna};padding-top:8px;margin-top:24px\"><b>{H(f.NazivFirme)}</b><br>"
        + H(string.Join(" | ", new[] { f.Adresa, f.Email, f.Web }.Where(s => !string.IsNullOrWhiteSpace(s)))) + "</p>";

    private static string PotpisTekst(Postavke f) =>
        "\n--\n" + string.Join("\n", new[] { f.NazivFirme, f.Adresa, f.Email, f.Web }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string Okvir(string sadrzaj) =>
        $"<div style=\"font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;max-width:640px;line-height:1.45\">{sadrzaj}</div>";

    private static string EmailZaPotvrdu(NalogDetalji n, Postavke f, string link) => Okvir(
        $"<p>Poštovani/a {H(n.PotvrdaIme)},</p>"
        + $"<p>{H(f.NazivFirme ?? "Naša firma")} je izvršio intervenciju po radnom nalogu <b>{H(n.Broj)}</b>. "
        + "Molimo vas da pregledate nalog i potvrdite da ste upoznati s njim. Nalog je i u prilogu (PDF).</p>"
        + TabelaHtml(n)
        + $"<p style=\"margin:22px 0\"><a href=\"{H(link)}\" style=\"background:{Tirkizna};color:#fff;text-decoration:none;padding:11px 20px;border-radius:6px;font-weight:bold;display:inline-block\">Pregledaj i potvrdi nalog</a></p>"
        + "<p style=\"font-size:13px;color:#555\">Ako nešto u nalogu nije tačno, na istoj stranici možete upisati primjedbu umjesto potvrde.<br>"
        + $"Ako dugme ne radi, otvorite ovaj link: <a href=\"{H(link)}\">{H(link)}</a></p>"
        + Potpis(f));

    private static string EmailZaPotvrduTekst(NalogDetalji n, Postavke f, string link) =>
        $"Poštovani/a {n.PotvrdaIme},\n\n{f.NazivFirme ?? "Naša firma"} je izvršio intervenciju po radnom nalogu {n.Broj}. "
        + "Molimo vas da pregledate nalog i potvrdite da ste upoznati s njim. Nalog je i u prilogu (PDF).\n\n"
        + TabelaTekst(n) + $"\n\nPregled i potvrda naloga:\n{link}\n\n"
        + "Ako nešto u nalogu nije tačno, na istoj stranici možete upisati primjedbu umjesto potvrde." + PotpisTekst(f);

    private static string EmailHvala(NalogDetalji n, Postavke f) => Okvir(
        $"<p>Poštovani/a {H(n.PotvrdaIme)},</p>"
        + $"<p>hvala – potvrdili ste radni nalog <b>{H(n.Broj)}</b> ({H(Vrijeme(n.Potvrdjeno))}). "
        + "U prilogu je nalog u PDF-u sa upisanom potvrdom. Ako ga predajete službi finansija, odštampajte ga i potpišite u polju „Ovjera korisnika usluge”.</p>"
        + TabelaHtml(n) + Potpis(f));

    private static string EmailHvalaTekst(NalogDetalji n, Postavke f) =>
        $"Poštovani/a {n.PotvrdaIme},\n\nhvala – potvrdili ste radni nalog {n.Broj} ({Vrijeme(n.Potvrdjeno)}). "
        + "U prilogu je nalog u PDF-u sa upisanom potvrdom. Ako ga predajete službi finansija, odštampajte ga i potpišite u polju „Ovjera korisnika usluge”.\n\n"
        + TabelaTekst(n) + PotpisTekst(f);
}

using System.Net;
using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Nalozi;
using Tehnotim.RadniNalozi.Pdf;

namespace Tehnotim.RadniNalozi.Sdp;

public record PorukaTiketa(bool Ok, string Poruka);

/// <summary>
/// Upis u tiket iz kojeg je nalog napravljen: napomena kad se nalog otvori i napomena + PDF kad se završi.
/// Napomene vide samo tehničari (show_to_requester = false). Status tiketa mijenja samo dugme „Zatvori nalog” (ZatvoriTiketAsync).
/// Ne smeta čuvanju naloga: greška se samo javi i upiše u istoriju, a može se ponoviti dugmetom „Pošalji u tiket”.
/// </summary>
public static class PovratUTiket
{
    private class Stanje
    {
        public long? SdpZahtjevId { get; set; }
        public string Faza { get; set; } = "";
        public DateTime? SdpNapomenaOtvoren { get; set; }
        public DateTime? SdpNapomenaZavrsen { get; set; }
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "").Replace("\n", "<br>");

    private static object Napomena(string html) => new
    {
        note = new { description = html, show_to_requester = false, notify_technician = false, mark_first_response = false, add_to_linked_requests = false }
    };

    public static async Task<List<PorukaTiketa>> PosaljiAsync(string cs, int nalogId, string webRoot, string? korisnik)
    {
        var poruke = new List<PorukaTiketa>();
        await using var c = new SqlConnection(cs);
        var st = await c.QuerySingleOrDefaultAsync<Stanje>(
            "SELECT SdpZahtjevId, Faza, SdpNapomenaOtvoren, SdpNapomenaZavrsen FROM dbo.Nalog WHERE Id = @nalogId", new { nalogId });
        if (st?.SdpZahtjevId is not long tiket) return poruke;
        if (st.SdpNapomenaOtvoren is not null && (st.Faza != "zavrsen" || st.SdpNapomenaZavrsen is not null)) return poruke;

        var n = await NaloziApi.UcitajAsync(cs, nalogId);
        if (n is null) return poruke;

        async Task Istorija(string opis) =>
            await c.ExecuteAsync("INSERT INTO dbo.NalogIzmjena (NalogId, Korisnik, Opis) VALUES (@nalogId, @korisnik, @opis)", new { nalogId, korisnik, opis });

        try
        {
            if (st.SdpNapomenaOtvoren is null)
            {
                var html = $"<b>Otvoren radni nalog {H(n.Broj)}</b><br>Nosilac: {H(n.NosilacIme ?? "nije određen")}<br>"
                         + $"Klijent: {H(n.KlijentNaziv)}{(string.IsNullOrWhiteSpace(n.Lokacija) ? "" : " – " + H(n.Lokacija))}<br>"
                         + $"Napravio: {H(n.Kreirao ?? korisnik)}<br><i>Upisala aplikacija Radni nalozi.</i>";
                await SdpVeza.PostAsync(cs, $"/api/v3/requests/{tiket}/notes", Napomena(html));
                await c.ExecuteAsync("UPDATE dbo.Nalog SET SdpNapomenaOtvoren = SYSUTCDATETIME() WHERE Id = @nalogId", new { nalogId });
                await Istorija($"Napomena o otvaranju upisana u tiket {tiket}");
                poruke.Add(new(true, $"U tiket {tiket} upisana je napomena o otvaranju naloga."));
            }

            if (n.Faza == "zavrsen" && st.SdpNapomenaZavrsen is null)
            {
                // PDF u prilog tiketa (pokušava se nekoliko oblika poziva, zavisno od verzije SDP-a)
                var (pdf, ime) = await PdfApi.NapraviAsync(cs, n, webRoot);
                string? greskaPdf = null;
                var prilozen = false;
                // SDP v3 (help.servicedeskplus.com/v3-attachment-api-changes): PUT /api/v3/requests/{id}/upload, polje input_file
                foreach (var (metod, putanja, polje) in new[]
                         {
                             (HttpMethod.Put, $"/api/v3/requests/{tiket}/upload", "input_file"),
                             (HttpMethod.Post, $"/api/v3/requests/{tiket}/upload", "input_file")
                         })
                {
                    try { await SdpVeza.PostFajlAsync(cs, metod, putanja, pdf, ime, polje); prilozen = true; break; }
                    catch (SdpGreska ex) { greskaPdf ??= ex.Message; }
                }

                var html = $"<b>Radni nalog {H(n.Broj)} je završen</b> "
                         + (n.Potvrdjeno is DateTime potvrdjeno
                             ? $"(potvrdio korisnik: {H(n.PotvrdaIme)}, {H(n.PotvrdaEmail)}, {potvrdjeno:dd.MM.yyyy. HH:mm})<br>"
                             : n.Zatvoreno is DateTime zatvoreno
                                 ? $"(zatvorio: {H(n.Zatvorio)}, {zatvoreno:dd.MM.yyyy. HH:mm})<br>"
                                 : "(potpisan od korisnika usluge)<br>")
                         + $"Nosilac: {H(n.NosilacIme)}<br>"
                         + (string.IsNullOrWhiteSpace(n.Status) ? "" : $"Status: {H(n.Status)}<br>")
                         + (string.IsNullOrWhiteSpace(n.Intervencija) ? "" : $"Intervencija: {H(n.Intervencija)}<br>")
                         + (string.IsNullOrWhiteSpace(n.UtrosenoVrijeme) ? "" : $"Utrošeno vrijeme: {H(n.UtrosenoVrijeme)}<br>")
                         + (string.IsNullOrWhiteSpace(n.OpisIntervencije) ? "" : $"<br><b>Opis izvršene intervencije:</b><br>{H(n.OpisIntervencije)}<br>")
                         + (prilozen ? $"<br>PDF naloga je u prilogu tiketa ({H(ime)})." : "<br>PDF naloga nije priložen (greška pri slanju) – preuzmite ga iz aplikacije.")
                         + "<br><i>Upisala aplikacija Radni nalozi.</i>";
                await SdpVeza.PostAsync(cs, $"/api/v3/requests/{tiket}/notes", Napomena(html));
                await c.ExecuteAsync("UPDATE dbo.Nalog SET SdpNapomenaZavrsen = SYSUTCDATETIME() WHERE Id = @nalogId", new { nalogId });
                await Istorija(prilozen ? $"Napomena o završetku i PDF upisani u tiket {tiket}" : $"Napomena o završetku upisana u tiket {tiket}, PDF nije priložen: {greskaPdf}");
                poruke.Add(new(true, prilozen ? $"U tiket {tiket} upisana je napomena o završetku i priložen PDF." : $"U tiket {tiket} upisana je napomena o završetku."));
                if (!prilozen) poruke.Add(new(false, $"PDF nije priložen tiketu: {greskaPdf}"));
            }
        }
        catch (SdpGreska ex)
        {
            await Istorija($"Upis u tiket {tiket} nije uspio: {ex.Message}");
            poruke.Add(new(false, $"Upis u tiket {tiket} nije uspio: {ex.Message} Nalog je sačuvan – pokušajte ponovo dugmetom „Pošalji u tiket”."));
        }
        return poruke;
    }

    /// <summary>
    /// Zatvara tiket iz kojeg je nalog napravljen (dugme „Zatvori nalog”), kao što to radi tehničar u SDP-u:
    /// popuni obavezna polja (Inženjer / Tehničar; za Printing i serijski broj i brojač), upiše rješenje
    /// i prebaci tiket u Closed ili Resolved. Ako za isti tiket postoji još nezatvoren nalog, tiket ostaje otvoren
    /// dok se ne zatvori i posljednji nalog.
    /// </summary>
    public static async Task<List<PorukaTiketa>> ZatvoriTiketAsync(string cs, int nalogId, string? korisnik, string? statusTiketa)
    {
        var poruke = new List<PorukaTiketa>();
        var n = await NaloziApi.UcitajAsync(cs, nalogId);
        if (n?.SdpZahtjevId is not long tiket || n.Zatvoreno is null || n.SdpTiketZatvoren is not null) return poruke;
        var resolved = statusTiketa == "Resolved";

        await using var c = new SqlConnection(cs);
        async Task Istorija(string opis) =>
            await c.ExecuteAsync("INSERT INTO dbo.NalogIzmjena (NalogId, Korisnik, Opis) VALUES (@nalogId, @korisnik, @opis)", new { nalogId, korisnik, opis });

        // Svi nalozi ovog tiketa (serviseri ponekad za isti tiket pišu dva naloga)
        var nalozi = (await c.QueryAsync<(int Id, string Broj, DateTime? Zatvoreno)>(@"
            SELECT Id, Broj, Zatvoreno FROM dbo.Nalog WHERE SdpZahtjevId = @tiket ORDER BY Id", new { tiket })).ToList();
        var otvoreni = nalozi.Where(x => x.Zatvoreno is null).Select(x => x.Broj).ToList();
        if (otvoreni.Count > 0)
        {
            poruke.Add(new(true, $"Tiket {tiket} ostaje otvoren dok se ne zatvori i nalog {string.Join(", ", otvoreni)}."));
            return poruke;
        }

        // Inženjer / Tehničar = nosilac naloga (ime iz SDP-a ako je upisano kod korisnika)
        var tehnicar = await c.ExecuteScalarAsync<string?>(@"
            SELECT COALESCE(NULLIF(k.SdpIme, ''), k.ImePrezime) FROM dbo.Nalog n JOIN dbo.Korisnik k ON k.Id = n.NosilacKorisnikId WHERE n.Id = @nalogId",
            new { nalogId }) ?? n.NosilacIme;
        if (string.IsNullOrWhiteSpace(tehnicar))
        {
            poruke.Add(new(false, $"Tiket {tiket} nije zatvoren: nalog nema nosioca (polje „Inženjer / Tehničar” u SDP-u je obavezno)."));
            return poruke;
        }

        // Uređaji sa svih naloga tiketa
        var svi = new List<NalogDetalji>();
        foreach (var x in nalozi) if (await NaloziApi.UcitajAsync(cs, x.Id) is { } d) svi.Add(d);
        var uredjaji = svi.SelectMany(d => d.Uredjaji).Where(u => !string.IsNullOrWhiteSpace(u.SerijskiBroj)).ToList();
        var serijski = uredjaji.Select(u => u.SerijskiBroj!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var brojac = uredjaji.Select(u => (u.Kolor ?? 0) + (u.CB ?? 0)).FirstOrDefault(b => b > 0);

        // Šablon tiketa: Printing traži i serijski broj i brojač
        string? sablon = null;
        try { sablon = SdpVeza.Tekst((await SdpVeza.GetAsync(cs, $"/api/v3/requests/{tiket}")).GetProperty("request").GetProperty("template"), "name"); }
        catch (Exception ex) when (ex is SdpGreska or KeyNotFoundException or InvalidOperationException) { }
        var printing = sablon?.Contains("Printing", StringComparison.OrdinalIgnoreCase) == true;

        var udf = new Dictionary<string, object> { ["udf_pick_602"] = new { name = tehnicar } };
        if (printing)
        {
            var sn = serijski.Count == 0 ? "0" : string.Join(", ", serijski);
            udf["udf_sline_301"] = sn.Length > 250 ? sn[..250] : sn;
            udf["udf_long_303"] = brojac.ToString();
        }

        var rjesenje = string.Join("<br><br>", svi.Select(d =>
                           $"<b>Radni nalog {H(d.Broj)}</b> ({H(d.NosilacIme)}): {H(d.OpisIntervencije)}"
                           + string.Concat(d.Uredjaji.Where(u => u.Kolor is not null || u.CB is not null)
                               .Select(u => $"<br>Brojač {H(u.SerijskiBroj ?? u.Model)}: {(u.Kolor ?? 0) + (u.CB ?? 0)}"))))
                     + "<br><i>Upisala aplikacija Radni nalozi.</i>";
        try
        {
            await SdpVeza.PutAsync(cs, $"/api/v3/requests/{tiket}", new { request = new { udf_fields = udf, resolution = new { content = rjesenje } } });
            var komentar = $"Zatvoreno iz aplikacije Radni nalozi – {string.Join(", ", nalozi.Select(x => x.Broj))} ({korisnik}).";
            if (resolved)
                await SdpVeza.PutAsync(cs, $"/api/v3/requests/{tiket}", new { request = new { status = new { name = "Resolved" } } });
            else
                await SdpVeza.PutAsync(cs, $"/api/v3/requests/{tiket}/close",
                    new { request = new { closure_info = new { requester_ack_resolution = false, closure_comments = komentar } } });

            await c.ExecuteAsync("UPDATE dbo.Nalog SET SdpTiketZatvoren = SYSDATETIME() WHERE SdpZahtjevId = @tiket", new { tiket });
            var stanje = resolved ? "riješen (Resolved)" : "zatvoren (Closed)";
            await Istorija($"Tiket {tiket} {stanje} u ServiceDesk Plus-u; Inženjer / Tehničar: {tehnicar}" + (printing ? $", SN: {udf["udf_sline_301"]}, brojač: {brojac}" : ""));
            poruke.Add(new(true, $"Tiket {tiket} je {stanje} u ServiceDesk Plus-u."));
        }
        catch (SdpGreska ex)
        {
            await Istorija($"Tiket {tiket} nije zatvoren: {ex.Message}");
            poruke.Add(new(false, $"Tiket {tiket} nije zatvoren: {ex.Message} Nalog je zatvoren – tiket zatvorite u SDP-u ili kliknite „Ponovo zatvori tiket”."));
        }
        return poruke;
    }
}

using System.Net;
using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Nalozi;
using Tehnotim.RadniNalozi.Pdf;

namespace Tehnotim.RadniNalozi.Sdp;

public record PorukaTiketa(bool Ok, string Poruka);

/// <summary>
/// Upis u tiket iz kojeg je nalog napravljen: napomena kad se nalog otvori i napomena + PDF kad se završi.
/// Napomene vide samo tehničari (show_to_requester = false). Status tiketa se ne mijenja.
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

                var html = $"<b>Radni nalog {H(n.Broj)} je završen</b> (potpisan od korisnika usluge)<br>"
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
}

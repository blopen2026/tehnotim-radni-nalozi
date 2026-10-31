using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Nalozi;
using Tehnotim.RadniNalozi.Prijava;
using Tehnotim.RadniNalozi.Sifarnici;

namespace Tehnotim.RadniNalozi.Pdf;

public static class PdfApi
{
    public static void MapPdf(this WebApplication app, string connectionString)
    {
        // PDF naloga. ?preuzmi=true → preuzimanje fajla; inače se otvara u pregledniku (za štampu).
        app.MapGet("/api/nalozi/{id:int}/pdf", async (int id, bool? preuzmi, HttpContext http, IWebHostEnvironment env) =>
        {
            var n = await NaloziApi.UcitajAsync(connectionString, id);
            if (n is null) return Results.NotFound(new { greska = "Nalog ne postoji." });

            var (pdf, ime) = await NapraviAsync(connectionString, n, env.WebRootPath);
            http.Response.Headers.ContentDisposition =
                $"{(preuzmi == true ? "attachment" : "inline")}; filename=\"{Ascii(ime)}\"; filename*=UTF-8''{Uri.EscapeDataString(ime)}";
            return Results.Bytes(pdf, "application/pdf");
        }).RequireAuthorization(Politike.Korisnik);
    }

    /// <summary>PDF naloga i ime fajla (za preglednik i za prilog tiketu).</summary>
    public static async Task<(byte[] Pdf, string Ime)> NapraviAsync(string connectionString, NalogDetalji n, string webRoot)
    {
        await using var c = new SqlConnection(connectionString);
        var firma = await c.QuerySingleAsync<Postavke>(
            "SELECT TarifaKm, NazivFirme, Adresa, Email, Web, Jib, Mb, ZiroRacun FROM dbo.Postavke WHERE Id = 1");
        var skracenica = await c.ExecuteScalarAsync<string?>("SELECT Skracenica FROM dbo.Klijent WHERE Id = @k", new { k = n.KlijentId });
        var logoPutanja = Path.Combine(webRoot, "img", "logo.png");
        var logo = File.Exists(logoPutanja) ? await File.ReadAllBytesAsync(logoPutanja) : null;
        return (PdfNaloga.Napravi(n, firma, logo), ImeFajla(n, skracenica));
    }

    /// <summary>Skraćenica klijenta (ili naziv) + interni + eksterni broj; bez brojeva zahtjeva + broj naloga.</summary>
    public static string ImeFajla(NalogDetalji n, string? skracenica)
    {
        var dijelovi = new List<string?> { string.IsNullOrWhiteSpace(skracenica) ? n.KlijentNaziv : skracenica, n.PozivInterni, n.PozivEksterni }
            .Select(Ocisti).Where(s => s.Length > 0).ToList();
        if (string.IsNullOrWhiteSpace(n.PozivInterni) && string.IsNullOrWhiteSpace(n.PozivEksterni))
            dijelovi.Add("RN-" + Ocisti(n.Broj));
        return (dijelovi.Count > 0 ? string.Join("_", dijelovi) : "Radni-nalog") + ".pdf";
    }

    // Znakovi koje Windows ne dozvoljava u imenu fajla → crtica; razmaci → crtica
    private static string Ocisti(string? s) =>
        Regex.Replace(Regex.Replace((s ?? "").Trim(), @"[\\/:*?""<>|\s]+", "-"), "-{2,}", "-").Trim('-');

    // Rezervno ime samo s ASCII slovima (š→s, č→c …) za starije preglednike
    private static string Ascii(string s)
    {
        var zamjene = new Dictionary<char, string> { ['š'] = "s", ['Š'] = "S", ['č'] = "c", ['Č'] = "C", ['ć'] = "c", ['Ć'] = "C", ['ž'] = "z", ['Ž'] = "Z", ['đ'] = "dj", ['Đ'] = "Dj" };
        var sb = new StringBuilder();
        foreach (var ch in s)
            sb.Append(zamjene.TryGetValue(ch, out var z) ? z : ch < 128 && ch != '"' ? ch.ToString() : "_");
        return sb.ToString();
    }
}

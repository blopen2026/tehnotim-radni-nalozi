using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Nalozi;
using Tehnotim.RadniNalozi.Pdf;
using Tehnotim.RadniNalozi.Prijava;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Tehnotim.RadniNalozi.Izvjestaji;

public class IzvjestajNalog
{
    public int Id { get; set; }
    public string Broj { get; set; } = "";
    public DateTime DatumIzdavanja { get; set; }
    public string Faza { get; set; } = "";
    public int KlijentId { get; set; }
    public string KlijentNaziv { get; set; } = "";
    public string? KlijentSkracenica { get; set; }
    public int? PoslovnaJedinicaId { get; set; }
    public string? Lokacija { get; set; }
    public int? NosilacKorisnikId { get; set; }
    public string? NosilacIme { get; set; }
    public string? PozivInterni { get; set; }
    public string? PozivEksterni { get; set; }
    public string? Naplata { get; set; }
    public string? Ugovor { get; set; }
    public string? Intervencija { get; set; }
    public string? Status { get; set; }
    public DateTime? PocetakRada { get; set; }
    public DateTime? ZavrsetakRada { get; set; }
    public string? UtrosenoVrijeme { get; set; }
    public bool UtrosenoRucno { get; set; }
    public decimal? KmTamo { get; set; }
    public decimal? KmNazad { get; set; }
    public decimal? PutniIznos { get; set; }
    public string? PrviUredjaj { get; set; }
    public string? OpisIntervencije { get; set; }
    public int? Minuta { get; set; }      // izračunato
    public decimal Km => (KmTamo ?? 0) + (KmNazad ?? 0);
}

public class IzvjestajGrupa
{
    public string Naziv { get; set; } = "";
    public int Broj { get; set; }
    public int Zavrseno { get; set; }
    public int ZaNaplatu { get; set; }
    public int Minuta { get; set; }
    public int BezVremena { get; set; }
    public decimal Km { get; set; }
    public decimal Putni { get; set; }
}

public class IzvjestajMaterijal
{
    public string Naziv { get; set; } = "";
    public string? PN { get; set; }
    public decimal Kolicina { get; set; }
    public int Naloga { get; set; }
}

public record IzvjestajUpit(DateTime? Od, DateTime? Do, int? KlijentId, int? JedinicaId, int? NosilacId, int? SektorId,
    string? Faza, string? Naplata, string? Ugovor, string? Intervencija, string? Grupa);

/// <summary>Izvještaj radnih naloga: filteri, zbir, grupisanje, spisak, materijal; izvoz u Excel.
/// Kancelarija i administrator vide sve, serviser samo naloge kojima je nosilac.</summary>
public static class IzvjestajiApi
{
    public static void MapIzvjestaji(this WebApplication app, string cs)
    {
        var g = app.MapGroup("/api/izvjestaj").RequireAuthorization(Politike.Korisnik);

        g.MapGet("", async (ClaimsPrincipal ko, DateTime? od, DateTime? @do, int? klijentId, int? jedinicaId, int? nosilacId, int? sektorId,
                            string? faza, string? naplata, string? ugovor, string? intervencija, string? grupa) =>
        {
            var u = new IzvjestajUpit(od, @do, klijentId, jedinicaId, nosilacId, sektorId, faza, naplata, ugovor, intervencija, grupa);
            var r = await NapraviAsync(cs, ko, u);
            return Results.Ok(new { r.Zbir, r.Grupe, nalozi = r.Nalozi, r.Materijal, r.SamoSvoji });
        });

        g.MapGet("/excel", async (ClaimsPrincipal ko, DateTime? od, DateTime? @do, int? klijentId, int? jedinicaId, int? nosilacId, int? sektorId,
                                  string? faza, string? naplata, string? ugovor, string? intervencija, string? grupa) =>
        {
            var u = new IzvjestajUpit(od, @do, klijentId, jedinicaId, nosilacId, sektorId, faza, naplata, ugovor, intervencija, grupa);
            var r = await NapraviAsync(cs, ko, u);
            var bajtovi = Excel(r, u);
            var ime = $"Izvjestaj-nalozi_{(u.Od ?? DateTime.Today):yyyy-MM-dd}_{(u.Do ?? DateTime.Today):yyyy-MM-dd}.xlsx";
            return Results.File(bajtovi, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ime);
        });

        // PDF svih naloga iz izvještaja: ?nacin=jedan (svi u jednom PDF-u) ili ?nacin=zip (svaki nalog poseban PDF u ZIP-u)
        g.MapGet("/pdf", async (ClaimsPrincipal ko, IWebHostEnvironment env, string? nacin, DateTime? od, DateTime? @do, int? klijentId, int? jedinicaId,
                                int? nosilacId, int? sektorId, string? faza, string? naplata, string? ugovor, string? intervencija) =>
        {
            var u = new IzvjestajUpit(od, @do, klijentId, jedinicaId, nosilacId, sektorId, faza, naplata, ugovor, intervencija, null);
            var r = await NapraviAsync(cs, ko, u);
            if (r.Nalozi.Count == 0) return Results.BadRequest(new { greska = "Nema naloga za izabrane filtere." });
            if (r.Nalozi.Count > MaxPdf) return Results.BadRequest(new { greska = $"Izabrano je {r.Nalozi.Count} naloga – za PDF je najviše {MaxPdf}. Suzite period ili filtere." });

            var period = $"{(u.Od ?? r.Nalozi.Min(n => n.DatumIzdavanja)):yyyy-MM-dd}_{(u.Do ?? DateTime.Today):yyyy-MM-dd}";
            if (nacin == "zip")
            {
                using var zipTok = new MemoryStream();
                using (var zip = new ZipArchive(zipTok, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var imena = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var stavka in r.Nalozi)
                    {
                        var n = await NaloziApi.UcitajAsync(cs, stavka.Id);
                        if (n is null) continue;
                        var (pdf, ime) = await PdfApi.NapraviAsync(cs, n, env.WebRootPath);
                        if (!imena.Add(ime)) { ime = Path.GetFileNameWithoutExtension(ime) + "_RN-" + n.Broj.Replace('/', '-') + ".pdf"; imena.Add(ime); }
                        var ulaz = zip.CreateEntry(ime, CompressionLevel.Fastest);
                        await using var t = ulaz.Open();
                        await t.WriteAsync(pdf);
                    }
                }
                return Results.File(zipTok.ToArray(), "application/zip", $"Radni-nalozi_{period}.zip");
            }

            using var izlaz = new PdfDocument();
            foreach (var stavka in r.Nalozi)
            {
                var n = await NaloziApi.UcitajAsync(cs, stavka.Id);
                if (n is null) continue;
                var (pdf, _) = await PdfApi.NapraviAsync(cs, n, env.WebRootPath);
                using var izvor = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
                foreach (var strana in izvor.Pages) izlaz.AddPage(strana);
            }
            using var tok = new MemoryStream();
            izlaz.Save(tok, false);
            return Results.File(tok.ToArray(), "application/pdf", $"Radni-nalozi_{period}.pdf");
        });
    }

    private const int MaxPdf = 300;

    public record Rezultat(object Zbir, List<IzvjestajGrupa> Grupe, List<IzvjestajNalog> Nalozi, List<IzvjestajMaterijal> Materijal,
                           bool SamoSvoji, string NazivGrupe);

    private static async Task<Rezultat> NapraviAsync(string cs, ClaimsPrincipal ko, IzvjestajUpit u)
    {
        await using var c = new SqlConnection(cs);
        var ja = await c.QuerySingleAsync<(int Id, string Uloga)>("SELECT Id, Uloga FROM dbo.Korisnik WHERE KorisnickiNalog = @n", new { n = ko.Identity?.Name });
        var samoSvoji = ja.Uloga == Uloge.Serviser;
        var nosilac = samoSvoji ? ja.Id : u.NosilacId;
        static string? DaNe(string? x) => x is "DA" or "NE" ? x : null;

        var nalozi = (await c.QueryAsync<IzvjestajNalog>(@"
            SELECT n.Id, n.Broj, n.DatumIzdavanja, n.Faza, n.KlijentId, n.KlijentNaziv, k.Skracenica AS KlijentSkracenica, n.PoslovnaJedinicaId, n.Lokacija,
                   n.NosilacKorisnikId, n.NosilacIme, n.PozivInterni, n.PozivEksterni, n.Naplata, n.Ugovor, n.Intervencija, n.Status,
                   n.PocetakRada, n.ZavrsetakRada, n.UtrosenoVrijeme, n.UtrosenoRucno, n.KmTamo, n.KmNazad, n.PutniIznos, n.OpisIntervencije,
                   (SELECT TOP 1 CONCAT(nu.Model, CASE WHEN nu.Model IS NOT NULL AND nu.SerijskiBroj IS NOT NULL THEN ' · ' END, nu.SerijskiBroj)
                    FROM dbo.NalogUredjaj nu WHERE nu.NalogId = n.Id ORDER BY nu.Redoslijed) AS PrviUredjaj
            FROM dbo.Nalog n LEFT JOIN dbo.Klijent k ON k.Id = n.KlijentId
            WHERE (@od IS NULL OR n.DatumIzdavanja >= @od) AND (@doD IS NULL OR n.DatumIzdavanja <= @doD)
              AND (@klijentId IS NULL OR n.KlijentId = @klijentId) AND (@jedinicaId IS NULL OR n.PoslovnaJedinicaId = @jedinicaId)
              AND (@nosilac IS NULL OR n.NosilacKorisnikId = @nosilac) AND (@sektorId IS NULL OR n.SektorId = @sektorId)
              AND (@faza IS NULL OR n.Faza = @faza OR (@faza = 'u_radu' AND n.Faza = 'novo_iz_tiketa'))
              AND (@naplata IS NULL OR n.Naplata = @naplata) AND (@ugovor IS NULL OR n.Ugovor = @ugovor)
              AND (@intervencija IS NULL OR n.Intervencija = @intervencija)
            ORDER BY n.DatumIzdavanja, n.Id",
            new
            {
                od = u.Od?.Date, doD = u.Do?.Date, u.KlijentId, u.JedinicaId, nosilac, u.SektorId,
                faza = string.IsNullOrWhiteSpace(u.Faza) ? null : u.Faza, naplata = DaNe(u.Naplata), ugovor = DaNe(u.Ugovor),
                intervencija = string.IsNullOrWhiteSpace(u.Intervencija) ? null : u.Intervencija
            })).ToList();

        foreach (var n in nalozi) n.Minuta = Minute(n);

        // Grupisanje
        var (nazivGrupe, kljuc) = (u.Grupa ?? "klijent") switch
        {
            "nosilac" => ("Nosilac", (Func<IzvjestajNalog, string>)(n => n.NosilacIme ?? "(bez nosioca)")),
            "jedinica" => ("Klijent / poslovna jedinica", n => n.KlijentNaziv + (string.IsNullOrWhiteSpace(n.Lokacija) ? "" : " – " + n.Lokacija)),
            "mjesec" => ("Mjesec", n => n.DatumIzdavanja.ToString("yyyy-MM")),
            _ => ("Klijent", n => n.KlijentNaziv)
        };
        var grupe = nalozi.GroupBy(kljuc).Select(gr => new IzvjestajGrupa
        {
            Naziv = gr.Key, Broj = gr.Count(), Zavrseno = gr.Count(n => n.Faza == "zavrsen"), ZaNaplatu = gr.Count(n => n.Naplata == "DA"),
            Minuta = gr.Sum(n => n.Minuta ?? 0), BezVremena = gr.Count(n => n.Minuta is null),
            Km = gr.Sum(n => n.Km), Putni = gr.Sum(n => n.PutniIznos ?? 0)
        }).OrderBy(x => u.Grupa == "mjesec" ? x.Naziv : null).ThenByDescending(x => u.Grupa == "mjesec" ? 0 : x.Broj).ThenBy(x => x.Naziv).ToList();

        // Materijal
        var ids = nalozi.Select(n => n.Id).ToArray();
        var materijal = ids.Length == 0 ? new List<IzvjestajMaterijal>() : (await c.QueryAsync<IzvjestajMaterijal>(@"
            SELECT nm.Naziv, MAX(nm.PN) AS PN, SUM(ISNULL(nm.Kolicina, 1)) AS Kolicina, COUNT(DISTINCT nm.NalogId) AS Naloga
            FROM dbo.NalogMaterijal nm WHERE nm.NalogId IN @ids
            GROUP BY nm.Naziv ORDER BY nm.Naziv", new { ids })).ToList();

        var zbir = new
        {
            brojNaloga = nalozi.Count, zavrseno = nalozi.Count(n => n.Faza == "zavrsen"), zaNaplatu = nalozi.Count(n => n.Naplata == "DA"),
            minuta = nalozi.Sum(n => n.Minuta ?? 0), bezVremena = nalozi.Count(n => n.Minuta is null),
            km = nalozi.Sum(n => n.Km), putni = nalozi.Sum(n => n.PutniIznos ?? 0), nazivGrupe
        };
        return new Rezultat(zbir, grupe, nalozi, materijal, samoSvoji, nazivGrupe);
    }

    /// <summary>Utrošeno vrijeme u minutama: iz početka/završetka, a ako je upisano ručno – iz teksta („2 h 30 min”, „1,5 h”, „2:30”).</summary>
    public static int? Minute(IzvjestajNalog n)
    {
        if (!n.UtrosenoRucno && n.PocetakRada is DateTime a && n.ZavrsetakRada is DateTime b && b > a)
            return (int)Math.Round((b - a).TotalMinutes);
        return IzTeksta(n.UtrosenoVrijeme);
    }

    public static int? IzTeksta(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return null;
        t = t.Trim().ToLowerInvariant();
        static decimal D(string s) => decimal.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);
        if (Regex.Match(t, @"^(\d{1,3}):(\d{2})$") is { Success: true } hm)
            return int.Parse(hm.Groups[1].Value) * 60 + int.Parse(hm.Groups[2].Value);
        if (Regex.Match(t, @"^(\d+(?:[.,]\d+)?)$") is { Success: true } broj)
            return (int)Math.Round(D(broj.Groups[1].Value) * 60);
        var h = Regex.Match(t, @"(\d+(?:[.,]\d+)?)\s*(?:h|sat|sata|sati|č|čas)");
        var m = Regex.Match(t, @"(\d+)\s*(?:min|m\b)");
        if (!h.Success && !m.Success) return null;
        return (int)Math.Round((h.Success ? D(h.Groups[1].Value) * 60 : 0) + (m.Success ? int.Parse(m.Groups[1].Value) : 0));
    }

    private static string Sati(int minuta) => $"{minuta / 60} h {minuta % 60:00} min";
    private static readonly Dictionary<string, string> Faze = new()
    { ["novo_iz_tiketa"] = "Novo iz tiketa", ["u_radu"] = "U radu", ["ceka_potpis"] = "Čeka potpis", ["zavrsen"] = "Završen" };

    private static byte[] Excel(Rezultat r, IzvjestajUpit u)
    {
        using var wb = new XLWorkbook();
        void Zaglavlje(IXLWorksheet ws, int red, params string[] kolone)
        {
            for (var i = 0; i < kolone.Length; i++) ws.Cell(red, i + 1).Value = kolone[i];
            var rng = ws.Range(red, 1, red, kolone.Length);
            rng.Style.Font.Bold = true; rng.Style.Fill.BackgroundColor = XLColor.FromHtml("#E4E8EE");
        }

        // Zbir
        var z = wb.AddWorksheet("Zbir");
        z.Cell(1, 1).Value = "Izvještaj radnih naloga"; z.Cell(1, 1).Style.Font.Bold = true; z.Cell(1, 1).Style.Font.FontSize = 14;
        z.Cell(2, 1).Value = $"Period: {(u.Od is null ? "od početka" : u.Od.Value.ToString("dd.MM.yyyy."))} – {(u.Do is null ? "danas" : u.Do.Value.ToString("dd.MM.yyyy."))}";
        var redZ = 4;
        void Red(string naziv, XLCellValue v, string? format = null)
        {
            z.Cell(redZ, 1).Value = naziv; z.Cell(redZ, 2).Value = v;
            if (format is not null) z.Cell(redZ, 2).Style.NumberFormat.Format = format;
            redZ++;
        }
        var minuta = r.Nalozi.Sum(n => n.Minuta ?? 0);
        Red("Broj naloga", r.Nalozi.Count);
        Red("Završeno", r.Nalozi.Count(n => n.Faza == "zavrsen"));
        Red("Za naplatu", r.Nalozi.Count(n => n.Naplata == "DA"));
        Red("Utrošeno vrijeme (sati)", Math.Round(minuta / 60m, 2), "0.00");
        Red("Naloga bez upisanog vremena", r.Nalozi.Count(n => n.Minuta is null));
        Red("Pređeno km", r.Nalozi.Sum(n => n.Km), "0.0");
        Red("Putni troškovi (KM)", r.Nalozi.Sum(n => n.PutniIznos ?? 0), "#,##0.00");
        z.Column(1).Width = 32; z.Column(2).Width = 16;

        // Grupisano
        var gs = wb.AddWorksheet("Grupisano");
        Zaglavlje(gs, 1, r.NazivGrupe, "Naloga", "Završeno", "Za naplatu", "Vrijeme (sati)", "Bez vremena", "Km", "Putni troškovi (KM)");
        var red = 2;
        foreach (var x in r.Grupe)
        {
            gs.Cell(red, 1).Value = x.Naziv; gs.Cell(red, 2).Value = x.Broj; gs.Cell(red, 3).Value = x.Zavrseno; gs.Cell(red, 4).Value = x.ZaNaplatu;
            gs.Cell(red, 5).Value = Math.Round(x.Minuta / 60m, 2); gs.Cell(red, 6).Value = x.BezVremena; gs.Cell(red, 7).Value = x.Km; gs.Cell(red, 8).Value = x.Putni;
            red++;
        }
        gs.Column(5).Style.NumberFormat.Format = "0.00"; gs.Column(7).Style.NumberFormat.Format = "0.0"; gs.Column(8).Style.NumberFormat.Format = "#,##0.00";
        gs.Columns().AdjustToContents();

        // Nalozi
        var ns = wb.AddWorksheet("Nalozi");
        Zaglavlje(ns, 1, "Broj", "Datum", "Klijent", "Poslovna jedinica", "Nosilac", "Zahtjev (interni)", "Tiket klijenta (eksterni)", "Uređaj", "Faza", "Status",
                  "Intervencija", "Naplata", "Ugovor", "Vrijeme (sati)", "Vrijeme (tekst)", "Km", "Putni troškovi (KM)", "Opis intervencije");
        red = 2;
        foreach (var n in r.Nalozi)
        {
            var v = new XLCellValue[]
            {
                n.Broj, n.DatumIzdavanja, n.KlijentNaziv, n.Lokacija ?? "", n.NosilacIme ?? "", n.PozivInterni ?? "", n.PozivEksterni ?? "",
                n.PrviUredjaj ?? "", Faze.GetValueOrDefault(n.Faza, n.Faza), n.Status ?? "", n.Intervencija ?? "", n.Naplata ?? "", n.Ugovor ?? "",
                n.Minuta is int mm ? (XLCellValue)Math.Round(mm / 60m, 2) : Blank.Value, n.UtrosenoVrijeme ?? "", n.Km, n.PutniIznos ?? 0, n.OpisIntervencije ?? ""
            };
            for (var i = 0; i < v.Length; i++) ns.Cell(red, i + 1).Value = v[i];
            red++;
        }
        ns.Column(2).Style.DateFormat.Format = "dd.MM.yyyy"; ns.Column(14).Style.NumberFormat.Format = "0.00";
        ns.Column(16).Style.NumberFormat.Format = "0.0"; ns.Column(17).Style.NumberFormat.Format = "#,##0.00";
        ns.Columns(1, 17).AdjustToContents(); ns.Column(18).Width = 60;
        ns.SheetView.FreezeRows(1);
        if (r.Nalozi.Count > 0) ns.Range(1, 1, red - 1, 18).SetAutoFilter();

        // Materijal
        var ms = wb.AddWorksheet("Materijal");
        Zaglavlje(ms, 1, "Materijal", "PN", "Količina", "Naloga");
        red = 2;
        foreach (var m in r.Materijal)
        {
            ms.Cell(red, 1).Value = m.Naziv; ms.Cell(red, 2).Value = m.PN ?? ""; ms.Cell(red, 3).Value = m.Kolicina; ms.Cell(red, 4).Value = m.Naloga;
            red++;
        }
        ms.Columns().AdjustToContents();

        using var izlaz = new MemoryStream();
        wb.SaveAs(izlaz);
        return izlaz.ToArray();
    }
}

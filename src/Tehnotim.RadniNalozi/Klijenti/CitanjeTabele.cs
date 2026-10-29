using System.Text;
using ClosedXML.Excel;
using Tehnotim.RadniNalozi.Prijava;

namespace Tehnotim.RadniNalozi.Klijenti;

/// <summary>
/// Čita prvi list .xlsx fajla ili .csv fajl i vraća redove kao tekst (kakav se vidi u Excelu).
/// Prepoznavanje kolona i pregled radi stranica za uvoz; ništa se ovdje ne upisuje u bazu.
/// </summary>
public static class CitanjeTabele
{
    private const long NajvecaVelicina = 5 * 1024 * 1024;

    public static void MapCitanjeTabele(this WebApplication app)
    {
        app.MapPost("/api/uvoz/procitaj", async (IFormFile fajl) =>
        {
            if (fajl.Length == 0) return Results.BadRequest(new { greska = "Fajl je prazan." });
            if (fajl.Length > NajvecaVelicina) return Results.BadRequest(new { greska = "Fajl je veći od 5 MB." });

            var ekstenzija = Path.GetExtension(fajl.FileName).ToLowerInvariant();
            await using var ulaz = new MemoryStream();
            await fajl.CopyToAsync(ulaz);
            ulaz.Position = 0;

            try
            {
                var redovi = ekstenzija switch
                {
                    ".xlsx" or ".xlsm" => ProcitajExcel(ulaz),
                    ".csv" or ".txt" => ProcitajCsv(ulaz),
                    _ => null
                };
                if (redovi is null)
                    return Results.BadRequest(new { greska = "Podržani su .xlsx i .csv fajlovi. Stari .xls otvorite u Excelu i sačuvajte kao .xlsx." });
                return Results.Ok(new { redovi });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { greska = "Fajl nije pročitan: " + ex.Message });
            }
        })
        .DisableAntiforgery()   // samo čita poslani fajl i vraća ga, ništa ne mijenja
        .RequireAuthorization(Politike.Kancelarija);
    }

    private static List<List<string>> ProcitajExcel(Stream ulaz)
    {
        using var wb = new XLWorkbook(ulaz);
        var list = wb.Worksheets.First();
        var opseg = list.RangeUsed();
        var redovi = new List<List<string>>();
        if (opseg is null) return redovi;

        var kolona = opseg.ColumnCount();
        foreach (var red in opseg.Rows())
        {
            var celije = new List<string>(kolona);
            for (var i = 1; i <= kolona; i++)
                celije.Add(red.Cell(i).GetFormattedString().Trim());
            if (celije.Any(c => c.Length > 0)) redovi.Add(celije);
        }
        return redovi;
    }

    private static List<List<string>> ProcitajCsv(Stream ulaz)
    {
        using var citac = new StreamReader(ulaz, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var tekst = citac.ReadToEnd();
        var prviRed = tekst.Split('\n').FirstOrDefault() ?? "";
        var separator = prviRed.Count(ch => ch == ';') > prviRed.Count(ch => ch == ',') ? ';'
                      : prviRed.Contains('\t') && !prviRed.Contains(',') ? '\t' : ',';

        var redovi = new List<List<string>>();
        var red = new List<string>();
        var celija = new StringBuilder();
        var uNavodnicima = false;

        for (var i = 0; i < tekst.Length; i++)
        {
            var ch = tekst[i];
            if (uNavodnicima)
            {
                if (ch == '"' && i + 1 < tekst.Length && tekst[i + 1] == '"') { celija.Append('"'); i++; }
                else if (ch == '"') uNavodnicima = false;
                else celija.Append(ch);
            }
            else if (ch == '"') uNavodnicima = true;
            else if (ch == separator) { red.Add(celija.ToString().Trim()); celija.Clear(); }
            else if (ch == '\n')
            {
                red.Add(celija.ToString().Trim()); celija.Clear();
                if (red.Any(c => c.Length > 0)) redovi.Add(red);
                red = [];
            }
            else if (ch != '\r') celija.Append(ch);
        }
        red.Add(celija.ToString().Trim());
        if (red.Any(c => c.Length > 0)) redovi.Add(red);
        return redovi;
    }
}

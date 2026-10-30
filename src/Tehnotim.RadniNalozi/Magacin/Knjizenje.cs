using System.Data.Common;
using Dapper;
using Tehnotim.RadniNalozi.Nalozi;

namespace Tehnotim.RadniNalozi.Magacin;

/// <summary>Stavka materijala naloga, povezana s magacinom koliko je moguće.</summary>
public class StavkaMaterijala
{
    public int? MaterijalId { get; set; }     // null = van magacina
    public bool VodiSeSN { get; set; }
    public int? KomadId { get; set; }         // komad iz magacina (artikal po SN)
    public string Naziv { get; set; } = "";
    public string? PN { get; set; }
    public string? SerijskiBroj { get; set; }
    public decimal? Kolicina { get; set; }
    public string? BrojOtpremnice { get; set; }
    public bool VanMagacinaRijeseno { get; set; }
    public string? VanMagacinaNapomena { get; set; }

    /// <summary>Šta stavka radi sa stanjem: komad (SN), količina, ili ništa (van magacina).</summary>
    public string Vrsta => KomadId is not null ? "komad" : MaterijalId is not null && !VodiSeSN ? "kolicina" : "van";
    public bool JeVanMagacina => Vrsta == "van";
}

/// <summary>
/// Knjiženje magacina iz naloga. Pri svakom čuvanju se poredi staro i novo stanje stavki naloga
/// i knjiži se samo razlika (ponovno čuvanje istog naloga ne skida ništa dvaput).
/// Minus je dozvoljen, ali se vraća upozorenje. Sve radi u transakciji naloga.
/// </summary>
public static class Knjizenje
{
    private class Artikal
    {
        public int Id { get; set; }
        public string Naziv { get; set; } = "";
        public string? PN { get; set; }
        public bool VodiSeSN { get; set; }
    }

    private class Komad
    {
        public int Id { get; set; }
        public string SerijskiBroj { get; set; } = "";
        public string Status { get; set; } = "";
        public int? NalogId { get; set; }
    }

    private static string? P(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Stavke koje nalog trenutno ima u bazi (prije izmjene).</summary>
    public static async Task<List<StavkaMaterijala>> StareStavkeAsync(DbConnection c, DbTransaction tx, int nalogId) =>
        (await c.QueryAsync<StavkaMaterijala>(@"
            SELECT nm.MaterijalId, ISNULL(m.VodiSeSN, 0) AS VodiSeSN, nm.KomadId, nm.Naziv, nm.PN, nm.SerijskiBroj, nm.Kolicina,
                   nm.BrojOtpremnice, nm.VanMagacinaRijeseno, nm.VanMagacinaNapomena
            FROM dbo.NalogMaterijal nm LEFT JOIN dbo.Materijal m ON m.Id = nm.MaterijalId
            WHERE nm.NalogId = @nalogId ORDER BY nm.Redoslijed", new { nalogId }, tx)).ToList();

    /// <summary>
    /// Povezuje unesene stavke s artiklima i komadima. Vraća grešku samo kad nalog ne može biti sačuvan
    /// (npr. isti SN dvaput ili SN koji je već utrošen u drugom nalogu).
    /// </summary>
    public static async Task<(List<StavkaMaterijala> Stavke, string? Greska)> PripremiAsync(
        DbConnection c, DbTransaction tx, IEnumerable<NalogMaterijalUnos> unos, int? nalogId, List<StavkaMaterijala> stare)
    {
        var stavke = new List<StavkaMaterijala>();
        foreach (var r in unos)
        {
            var naziv = P(r.Naziv);
            if (naziv is null && r.MaterijalId is null) continue;
            if (r.Kolicina < 0) return (new List<StavkaMaterijala>(), "Količina materijala ne može biti negativna.");

            var a = r.MaterijalId is int mid
                ? await c.QuerySingleOrDefaultAsync<Artikal>("SELECT Id, Naziv, PN, VodiSeSN FROM dbo.Materijal WHERE Id = @mid", new { mid }, tx)
                : await c.QueryFirstOrDefaultAsync<Artikal>("SELECT Id, Naziv, PN, VodiSeSN FROM dbo.Materijal WHERE Naziv = @naziv", new { naziv }, tx);

            var s = new StavkaMaterijala { BrojOtpremnice = P(r.BrojOtpremnice) };
            if (a is null)
            {
                // Van magacina: upisuje se kako je uneseno; razrješava administrator
                s.Naziv = naziv!;
                s.PN = P(r.PN);
                s.SerijskiBroj = P(r.SerijskiBroj);
                s.Kolicina = r.Kolicina;
            }
            else
            {
                s.MaterijalId = a.Id;
                s.VodiSeSN = a.VodiSeSN;
                s.Naziv = a.Naziv;
                s.PN = a.PN ?? P(r.PN);
                if (!a.VodiSeSN)
                {
                    s.Kolicina = r.Kolicina is > 0 ? r.Kolicina : 1;
                }
                else
                {
                    s.Kolicina = 1;
                    var sn = P(r.SerijskiBroj);
                    var k = r.KomadId is int kid
                        ? await c.QuerySingleOrDefaultAsync<Komad>("SELECT Id, SerijskiBroj, Status, NalogId FROM dbo.MagacinKomad WHERE Id = @kid AND MaterijalId = @m", new { kid, m = a.Id }, tx)
                        : sn is null ? null
                        : await c.QuerySingleOrDefaultAsync<Komad>("SELECT Id, SerijskiBroj, Status, NalogId FROM dbo.MagacinKomad WHERE MaterijalId = @m AND SerijskiBroj = @sn", new { m = a.Id, sn }, tx);
                    if (k is null && sn is null)
                        return (new List<StavkaMaterijala>(), $"Za artikal „{a.Naziv}” upišite ili izaberite serijski broj komada.");
                    if (k is not null)
                    {
                        var vecUOvomNalogu = k.Status == "utrosen" && nalogId is not null && k.NalogId == nalogId;
                        if (k.Status != "na_stanju" && !vecUOvomNalogu)
                            return (new List<StavkaMaterijala>(), $"Komad {k.SerijskiBroj} artikla „{a.Naziv}” je već utrošen u drugom nalogu ili otpisan.");
                        if (stavke.Any(x => x.KomadId == k.Id))
                            return (new List<StavkaMaterijala>(), $"Komad {k.SerijskiBroj} je u nalogu upisan dvaput.");
                        s.KomadId = k.Id;
                        s.SerijskiBroj = k.SerijskiBroj;
                    }
                    else s.SerijskiBroj = sn;   // SN kojeg nema u magacinu → evidencija van magacina
                }
            }

            // Stavka van magacina koja je već bila razriješena ostaje razriješena
            if (s.JeVanMagacina)
            {
                var stara = stare.FirstOrDefault(x => x.JeVanMagacina && x.VanMagacinaRijeseno && Isto(x.Naziv, s.Naziv) && Isto(x.PN, s.PN) && Isto(x.SerijskiBroj, s.SerijskiBroj));
                if (stara is not null) { s.VanMagacinaRijeseno = true; s.VanMagacinaNapomena = stara.VanMagacinaNapomena; }
            }
            stavke.Add(s);
        }
        return (stavke, null);
    }

    private static bool Isto(string? a, string? b) => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Knjiži razliku između starih i novih stavki naloga. Vraća upozorenja (minus, van magacina).</summary>
    public static async Task<List<string>> KnjiziAsync(DbConnection c, DbTransaction tx, int nalogId, string nalogBroj,
        List<StavkaMaterijala> stare, List<StavkaMaterijala> nove, string? korisnik)
    {
        var upozorenja = new List<string>();

        // Artikli po količini: razlika po artiklu
        var staro = stare.Where(s => s.Vrsta == "kolicina").GroupBy(s => s.MaterijalId!.Value).ToDictionary(g => g.Key, g => g.Sum(s => s.Kolicina ?? 0));
        var novo = nove.Where(s => s.Vrsta == "kolicina").GroupBy(s => s.MaterijalId!.Value).ToDictionary(g => g.Key, g => g.Sum(s => s.Kolicina ?? 0));
        foreach (var mid in staro.Keys.Union(novo.Keys))
        {
            var razlika = novo.GetValueOrDefault(mid) - staro.GetValueOrDefault(mid);
            if (razlika == 0) continue;
            var (stanje, naziv) = await PromijeniStanjeAsync(c, tx, mid, -razlika, razlika > 0 ? "utrosak" : "povrat", korisnik, nalogId, nalogBroj, null, null, null);
            if (stanje < 0) upozorenja.Add($"{naziv}: stanje u magacinu je sada {stanje:0.##} (u minusu). Javite administratoru da provjeri magacin.");
        }

        // Artikli po SN: komadi koji su izašli iz naloga se vraćaju, novi se troše
        var stariKomadi = stare.Where(s => s.Vrsta == "komad").Select(s => s.KomadId!.Value).ToHashSet();
        var noviKomadi = nove.Where(s => s.Vrsta == "komad").Select(s => s.KomadId!.Value).ToHashSet();
        foreach (var kid in stariKomadi.Except(noviKomadi))
        {
            var (mid, sn) = await c.QuerySingleAsync<(int, string)>(
                "UPDATE dbo.MagacinKomad SET Status = 'na_stanju', NalogId = NULL OUTPUT INSERTED.MaterijalId, INSERTED.SerijskiBroj WHERE Id = @kid", new { kid }, tx);
            await PromijeniStanjeAsync(c, tx, mid, 1, "povrat", korisnik, nalogId, nalogBroj, sn, null, null);
        }
        foreach (var kid in noviKomadi.Except(stariKomadi))
        {
            var (mid, sn) = await c.QuerySingleAsync<(int, string)>(
                "UPDATE dbo.MagacinKomad SET Status = 'utrosen', NalogId = @nalogId OUTPUT INSERTED.MaterijalId, INSERTED.SerijskiBroj WHERE Id = @kid", new { kid, nalogId }, tx);
            var (stanje, naziv) = await PromijeniStanjeAsync(c, tx, mid, -1, "utrosak", korisnik, nalogId, nalogBroj, sn, null, null);
            if (stanje < 0) upozorenja.Add($"{naziv}: stanje u magacinu je sada {stanje:0.##} (u minusu). Javite administratoru da provjeri magacin.");
        }

        foreach (var s in nove.Where(s => s.JeVanMagacina && !s.VanMagacinaRijeseno))
            upozorenja.Add(s.MaterijalId is null
                ? $"„{s.Naziv}” nije u magacinu – evidentirano u listi „Utrošeno van magacina”."
                : $"{s.Naziv}: serijski broj {s.SerijskiBroj} nije u magacinu – evidentirano u listi „Utrošeno van magacina”.");
        return upozorenja;
    }

    /// <summary>Mijenja stanje artikla za zadatu količinu (+ ulaz, − izlaz) i upisuje kretanje u karticu.</summary>
    public static async Task<(decimal Stanje, string Naziv)> PromijeniStanjeAsync(DbConnection c, DbTransaction tx, int materijalId, decimal kolicina,
        string vrsta, string? korisnik, int? nalogId, string? nalogBroj, string? serijskiBroj, string? dokument, string? napomena)
    {
        var (stanje, naziv) = await c.QuerySingleAsync<(decimal, string)>(
            "UPDATE dbo.Materijal SET Stanje = Stanje + @kolicina OUTPUT INSERTED.Stanje, INSERTED.Naziv WHERE Id = @materijalId",
            new { materijalId, kolicina }, tx);
        await c.ExecuteAsync(@"
            INSERT INTO dbo.MagacinPromjena (MaterijalId, Korisnik, Vrsta, Kolicina, StanjePoslije, NalogId, NalogBroj, SerijskiBroj, Dokument, Napomena)
            VALUES (@materijalId, @korisnik, @vrsta, @kolicina, @stanje, @nalogId, @nalogBroj, @serijskiBroj, @dokument, @napomena)",
            new { materijalId, korisnik, vrsta, kolicina, stanje, nalogId, nalogBroj, serijskiBroj, dokument, napomena }, tx);
        return (stanje, naziv);
    }
}

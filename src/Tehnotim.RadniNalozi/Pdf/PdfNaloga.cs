using System.Globalization;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Tehnotim.RadniNalozi.Nalozi;
using Tehnotim.RadniNalozi.Sifarnici;

namespace Tehnotim.RadniNalozi.Pdf;

/// <summary>
/// PDF radnog naloga u izgledu papirnog obrasca (raspored potvrđen u prototipu):
/// gornji dio tabele do Garancije ima svoju mrežu kolona (desna polovina podijeljena napola),
/// donji dio (od „Datum i vrijeme zahtjeva”) svoju (Kolor, C/B, Ukupno iste širine).
/// Tekst je pravi tekst (Arial iz Windowsa), polja za opis rastu s tekstom, a ako nalog ne stane, nastavlja se na novoj strani.
/// </summary>
public static class PdfNaloga
{
    // A4 u tačkama (1/72 inča)
    private const double StranaW = 595.28, StranaH = 841.89, Margina = 34;
    private const double Sirina = StranaW - 2 * Margina;
    private const double Pad = 3.5;

    // Mreže kolona – iste proporcije kao u prototipu (zbir 702)
    private static readonly double[] MrezaGore = Mreza([112, 92, 96, 81, 81, 120, 60, 60]);
    private static readonly double[] MrezaDole = Mreza([112, 92, 96, 81, 81, 80, 80, 80]);

    private static readonly XColor SivaLabel = XColor.FromArgb(230, 230, 230);
    private static readonly XColor SivaOznaceno = XColor.FromArgb(200, 200, 200);
    private static readonly CultureInfo Bs = CultureInfo.GetCultureInfo("bs-Latn-BA");

    private static XFont F(double vel, bool podebljano = false) =>
        new("Arial", vel, podebljano ? XFontStyleEx.Bold : XFontStyleEx.Regular);

    // Font resolver se smije postaviti samo jednom i prije prvog fonta
    static PdfNaloga()
    {
        try { GlobalFontSettings.FontResolver = new WindowsFontovi(); }
        catch (InvalidOperationException) { /* već postavljen */ }
    }

    private static double[] Mreza(double[] w)
    {
        var zbir = w.Sum();
        var x = new double[w.Length + 1];
        for (var i = 0; i < w.Length; i++) x[i + 1] = x[i] + w[i] / zbir * Sirina;
        return x;
    }

    // ---------- Ćelije i blokovi ----------

    private sealed record Celija(int Od, int Raspon, string? Tekst,
        bool Label = false, bool Oznaceno = false, bool Centar = false, bool Desno = false, bool Bold = false,
        double Vel = 8.5, double MinH = 0, byte[]? Slika = null);

    /// <summary>Lijeve ćelije zauzimaju visinu cijelog bloka; desno su redovi (podredovi) jedan ispod drugog.</summary>
    private sealed record Blok(double[] Mreza, List<Celija> Lijevo, List<List<Celija>> Redovi);

    private static Celija L(int od, int raspon, string tekst, bool centar = false, bool desno = false) =>
        new(od, raspon, tekst, Label: true, Bold: true, Centar: centar, Desno: desno);
    private static Celija T(int od, int raspon, string? tekst, double minH = 0, bool centar = false, bool bold = false) =>
        new(od, raspon, tekst, MinH: minH, Centar: centar, Bold: bold);

    private static Blok Red(double[] m, params Celija[] c) => new(m, [], [[.. c]]);

    // ---------- Glavna funkcija ----------

    public static byte[] Napravi(NalogDetalji n, Postavke firma, byte[]? logo)
    {
        var doc = new PdfDocument();
        doc.Info.Title = $"Radni nalog {n.Broj}";
        doc.Info.Author = firma.NazivFirme ?? "";

        var gore = MrezaGore;
        var dole = MrezaDole;
        var blokovi = new List<Blok>
        {
            new(gore, [], [[new Celija(0, 8, "RADNI NALOG", Label: true, Bold: true, Centar: true, Vel: 14)]]),

            // ID i poziv na zahtjev: Interni / Eksterni iste širine, linija između njih ide dolje do Garancije
            new(gore,
                [L(0, 1, "ID Radnog naloga:"), T(1, 2, n.Broj, bold: true), L(3, 2, "Poziv na zahtjev za podršku:")],
                [[L(5, 1, "Interni", centar: true), L(6, 2, "Eksterni", centar: true)],
                 [T(5, 1, n.PozivInterni, centar: true, bold: true), T(6, 2, n.PozivEksterni, centar: true, bold: true)]]),

            Red(gore, L(0, 1, "Datum izdavanja:"), T(1, 1, Dat(n.DatumIzdavanja)),
                      L(2, 1, "Datum i vrijeme početka rada:"), T(3, 2, DatV(n.PocetakRada)),
                      L(5, 1, "Datum i vrijeme završetka rada:"), T(6, 2, DatV(n.ZavrsetakRada))),
            Red(gore, L(0, 1, "Korisnik:\nLokacija:"),
                      T(1, 4, n.KlijentNaziv + "\n" + string.Join(", ", new[] { n.Lokacija, n.Adresa }.Where(s => !string.IsNullOrWhiteSpace(s)))),
                      L(5, 1, "Utrošeno vrijeme rada"), T(6, 2, n.UtrosenoVrijeme)),
            Red(gore, [L(0, 1, "Kontakt podaci:"), T(1, 4, n.Kontakt), L(5, 1, "Naplata:"), .. DaNe(n.Naplata)]),
            Red(gore, [L(0, 1, "Uređaj predao (datum i potpis):"), T(1, 4, n.UredjajPredao), L(5, 1, "Status ugovora:"), .. DaNe(n.Ugovor)]),
            Red(gore, [L(0, 1, "Nosilac naloga:"), T(1, 4, n.NosilacIme), L(5, 1, "Garancija:"), .. DaNe(n.Garancija)]),

            // ---- donji dio: druga mreža ----
            Red(dole, L(0, 1, "Datum i vrijeme zahtjeva:"), T(1, 4, DatV(n.ZahtjevVrijeme)), L(5, 3, "BROJ ODŠTAMPANIH STRANA:")),
            Red(dole, L(0, 2, "MODEL UREĐAJA:", centar: true), L(2, 2, "SERIJSKI BROJ:", centar: true), L(4, 1, "INV. BROJ:", centar: true),
                      L(5, 1, "Kolor", centar: true), L(6, 1, "C/B", centar: true), L(7, 1, "Ukupno", centar: true)),
        };

        var uredjaji = n.Uredjaji.ToList();
        while (uredjaji.Count < 2) uredjaji.Add(new NalogUredjajDetalji());
        foreach (var u in uredjaji)
        {
            var b = u.Kolor is not null || u.CB is not null || u.PratiBrojac;
            var ukupno = u.Kolor is null && u.CB is null ? "" : ((u.Kolor ?? 0) + (u.CB ?? 0)).ToString(Bs);
            blokovi.Add(Red(dole, T(0, 2, u.Model), T(2, 2, u.SerijskiBroj), T(4, 1, u.InventurniBroj),
                T(5, 1, b ? (u.SamoCrnoBijeli ? "—" : Broj(u.Kolor)) : "", centar: true),
                T(6, 1, b ? Broj(u.CB) : "", centar: true),
                T(7, 1, b ? ukupno : "", centar: true)));
        }
        var saBrojacem = n.Uredjaji.Where(u => u.Kolor is not null || u.CB is not null).ToList();
        if (saBrojacem.Count > 1)
        {
            int sk = saBrojacem.Sum(u => u.Kolor ?? 0), sc = saBrojacem.Sum(u => u.CB ?? 0);
            blokovi.Add(Red(dole, L(0, 5, "UKUPNO SVI UREĐAJI:", desno: true),
                T(5, 1, sk.ToString(Bs), centar: true, bold: true), T(6, 1, sc.ToString(Bs), centar: true, bold: true),
                T(7, 1, (sk + sc).ToString(Bs), centar: true, bold: true)));
        }

        blokovi.Add(Red(dole, L(0, 1, "Opis zahtjeva:"), T(1, 7, n.OpisZahtjeva, minH: 52)));
        blokovi.Add(Red(dole, L(0, 1, "Opis izvršene intervencije:"), T(1, 7, n.OpisIntervencije, minH: 104)));

        // Utrošeni materijal
        var mat = n.Materijal.Select(m => new List<Celija> { T(1, 4, m.Naziv), T(5, 1, Kol(m.Kolicina), centar: true), T(6, 2, m.BrojOtpremnice) }).ToList();
        while (mat.Count < 5) mat.Add(new List<Celija> { T(1, 4, ""), T(5, 1, ""), T(6, 2, "") });
        blokovi.Add(new(dole, [L(0, 1, "Utrošeno:")],
            [[L(1, 4, "Utrošeni materijal", centar: true), L(5, 1, "Količina", centar: true), L(6, 2, "Broj otpremnice", centar: true)], .. mat]));

        // Putni troškovi
        var kmUk = (n.KmTamo ?? 0) + (n.KmNazad ?? 0);
        blokovi.Add(new(dole, [L(0, 1, "Putni troškovi:")],
        [
            [T(1, 7, $"Od BL do  {n.PutDestinacija}   km  {Kol(n.KmTamo)}   do BL km  {Kol(n.KmNazad)}")],
            [T(1, 7, $"UKUPNO:  {Kol(kmUk)} km  x  TARIFA  {Kol(n.TarifaKm)}  =  {(n.PutniIznos is decimal iznos ? iznos.ToString("N2", Bs) + " KM" : "")}")]
        ]));

        string[] statusi = ["OBAVLJENA", "DJELIMIČNO OBAVLJENA", "NIJE OBAVLJENA", "NIJE POTREBNA"];
        blokovi.Add(Red(dole, L(0, 1, "Status:"),
            Izbor(1, 1, statusi[0], n.Status), Izbor(2, 2, statusi[1], n.Status), Izbor(4, 2, statusi[2], n.Status), Izbor(6, 2, statusi[3], n.Status)));
        blokovi.Add(Red(dole, L(0, 1, "Intervencija:"),
            Izbor(1, 4, "NA TERENU", n.Intervencija), Izbor(5, 3, "SERVIS/KANCELARIJA", n.Intervencija)));

        blokovi.Add(new(dole, [L(0, 1, "OVJERA RADNOG NALOGA (potpis)")],
        [
            [T(1, 4, "IZVOĐAČ (SERVISER): " + n.NosilacIme, minH: 40), new Celija(5, 3, null, MinH: 40, Slika: n.PotpisServiser)],
            [T(1, 4, "KORISNIK USLUGE:", minH: 40), new Celija(5, 3, null, MinH: 40, Slika: n.PotpisKorisnik)]
        ]));

        Crtaj(doc, blokovi, n, firma, logo);
        using var izlaz = new MemoryStream();
        doc.Save(izlaz);
        return izlaz.ToArray();
    }

    private static Celija[] DaNe(string? v) =>
        [new(6, 1, "DA", Centar: true, Oznaceno: v == "DA", Bold: v == "DA"), new(7, 1, "NE", Centar: true, Oznaceno: v == "NE", Bold: v == "NE")];
    private static Celija Izbor(int od, int raspon, string tekst, string? izabrano) =>
        new(od, raspon, tekst, Centar: true, Oznaceno: izabrano == tekst, Bold: izabrano == tekst, Vel: 8);

    private static string Dat(DateTime? d) => d is null ? "" : d.Value.ToString("dd.MM.yyyy.", Bs);
    private static string DatV(DateTime? d) => d is null ? "" : d.Value.ToString("dd.MM.yyyy. HH:mm", Bs);
    private static string Broj(int? b) => b?.ToString(Bs) ?? "";
    private static string Kol(decimal? k) => k is null ? "" : k.Value.ToString("0.##", Bs);

    // ---------- Crtanje ----------

    private static void Crtaj(PdfDocument doc, List<Blok> blokovi, NalogDetalji n, Postavke firma, byte[]? logo)
    {
        var strana = doc.AddPage();
        strana.Size = PageSize.A4;
        var g = XGraphics.FromPdfPage(strana);
        var y = Zaglavlje(g, firma, logo);
        var vrhTabele = y;
        var dnoStrane = StranaH - Margina - 22;
        var broj = 1;

        foreach (var b in blokovi)
        {
            var visine = VisineBloka(g, b);
            var h = visine.Sum();
            if (y + h > dnoStrane && y > vrhTabele)
            {
                Okvir(g, vrhTabele, y);
                Podnozje(g, firma, broj, n.Broj);
                g.Dispose();
                strana = doc.AddPage();
                strana.Size = PageSize.A4;
                g = XGraphics.FromPdfPage(strana);
                broj++;
                y = Margina;
                g.DrawString($"Radni nalog {n.Broj} – nastavak", F(9, true), XBrushes.Black, Margina, y, XStringFormats.TopLeft);
                y += 16;
                vrhTabele = y;
            }
            CrtajBlok(g, b, y, visine);
            y += h;
        }
        Okvir(g, vrhTabele, y);
        Podnozje(g, firma, broj, n.Broj);
        g.Dispose();
    }

    private static double Zaglavlje(XGraphics g, Postavke firma, byte[]? logo)
    {
        var y = Margina;
        if (logo is not null)
        {
            var img = XImage.FromStream(Tok(logo));
            var h = 34.0;
            var w = h * img.PixelWidth / img.PixelHeight;
            g.DrawImage(img, Margina, y, w, h);
        }
        var redovi = new[] { firma.NazivFirme, firma.Adresa, string.Join(" | ", new[] { firma.Email, firma.Web }.Where(s => !string.IsNullOrWhiteSpace(s))) }
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var f = F(7.5, true);
        var ry = y + 2;
        foreach (var r in redovi)
        {
            g.DrawString(r!, f, XBrushes.Black, StranaW - Margina - g.MeasureString(r, f).Width, ry, XStringFormats.TopLeft);
            ry += 10;
        }
        return y + 44;
    }

    private static void Podnozje(XGraphics g, Postavke firma, int strana, string brojNaloga)
    {
        var dijelovi = new List<string>();
        if (!string.IsNullOrWhiteSpace(firma.Jib)) dijelovi.Add("JIB: " + firma.Jib);
        if (!string.IsNullOrWhiteSpace(firma.Mb)) dijelovi.Add("MB: " + firma.Mb);
        if (!string.IsNullOrWhiteSpace(firma.ZiroRacun)) dijelovi.Add("Žiro-račun: " + firma.ZiroRacun);
        var tekst = string.Join("  I  ", dijelovi);
        var f = F(6.5, true);
        var y = StranaH - Margina - 10;
        if (tekst.Length > 0)
            g.DrawString(tekst, f, XBrushes.Black, (StranaW - g.MeasureString(tekst, f).Width) / 2, y, XStringFormats.TopLeft);
        if (strana > 1 || brojNaloga.Length > 0)
        {
            var oznaka = $"{brojNaloga} · str. {strana}";
            var fs = F(6.5);
            g.DrawString(oznaka, fs, XBrushes.Gray, StranaW - Margina - g.MeasureString(oznaka, fs).Width, y + 10, XStringFormats.TopLeft);
        }
    }

    private static void Okvir(XGraphics g, double y1, double y2) =>
        g.DrawRectangle(new XPen(XColors.Black, 1.5), Margina, y1, Sirina, y2 - y1);

    /// <summary>Visina svakog podreda u bloku; lijeve ćelije koje traže više mjesta produže zadnji podred.</summary>
    private static List<double> VisineBloka(XGraphics g, Blok b)
    {
        var visine = b.Redovi.Select(red => red.Max(c => PotrebnaVisina(g, b.Mreza, c))).ToList();
        var lijevo = b.Lijevo.Count == 0 ? 0 : b.Lijevo.Max(c => PotrebnaVisina(g, b.Mreza, c));
        var ukupno = visine.Sum();
        if (lijevo > ukupno) visine[^1] += lijevo - ukupno;
        return visine;
    }

    private static double PotrebnaVisina(XGraphics g, double[] m, Celija c)
    {
        var w = m[c.Od + c.Raspon] - m[c.Od] - 2 * Pad;
        var redova = Prelomi(g, c.Tekst ?? "", F(c.Vel, c.Bold), w).Count;
        var h = Math.Max(1, redova) * VisinaReda(c.Vel) + 2 * Pad;
        return Math.Max(Math.Max(h, 16), c.MinH);
    }

    private static double VisinaReda(double vel) => vel * 1.22;

    private static void CrtajBlok(XGraphics g, Blok b, double y, List<double> visine)
    {
        var ukupno = visine.Sum();
        foreach (var c in b.Lijevo) CrtajCeliju(g, b.Mreza, c, y, ukupno);
        var ry = y;
        for (var i = 0; i < b.Redovi.Count; i++)
        {
            foreach (var c in b.Redovi[i]) CrtajCeliju(g, b.Mreza, c, ry, visine[i]);
            ry += visine[i];
        }
    }

    private static readonly XPen Linija = new(XColors.Black, 0.6);

    // PDFsharp čita sliku direktno iz bafera toka, pa bafer mora biti javno dostupan (publiclyVisible: true)
    private static MemoryStream Tok(byte[] b) => new(b, 0, b.Length, writable: false, publiclyVisible: true);

    private static void CrtajCeliju(XGraphics g, double[] m, Celija c, double y, double h)
    {
        var x = Margina + m[c.Od];
        var w = m[c.Od + c.Raspon] - m[c.Od];
        if (c.Label || c.Oznaceno)
            g.DrawRectangle(new XSolidBrush(c.Oznaceno ? SivaOznaceno : SivaLabel), x, y, w, h);
        g.DrawRectangle(Linija, x, y, w, h);

        if (c.Slika is { Length: > 0 })
        {
            var img = XImage.FromStream(Tok(c.Slika));
            var maxW = w - 2 * Pad; var maxH = h - 2 * Pad;
            var k = Math.Min(maxW / img.PixelWidth, maxH / img.PixelHeight);
            var iw = img.PixelWidth * k; var ih = img.PixelHeight * k;
            g.DrawImage(img, x + (w - iw) / 2, y + (h - ih) / 2, iw, ih);
            return;
        }
        if (string.IsNullOrEmpty(c.Tekst)) return;

        var f = F(c.Vel, c.Bold);
        var redovi = Prelomi(g, c.Tekst, f, w - 2 * Pad);
        var vr = VisinaReda(c.Vel);
        // Kratki tekst (labele, vrijednosti) vertikalno u sredini; dugi opisi od vrha
        var ty = c.MinH > 30 ? y + Pad : y + (h - redovi.Count * vr) / 2;
        foreach (var r in redovi)
        {
            var tw = g.MeasureString(r, f).Width;
            var tx = c.Centar ? x + (w - tw) / 2 : c.Desno ? x + w - Pad - tw : x + Pad;
            g.DrawString(r, f, XBrushes.Black, tx, ty + (vr - c.Vel) / 2, XStringFormats.TopLeft);
            ty += vr;
        }
    }

    /// <summary>Prelom teksta u redove zadate širine (poštuje novi red; predugačke riječi lomi po slovima).</summary>
    private static List<string> Prelomi(XGraphics g, string tekst, XFont f, double sirina)
    {
        var redovi = new List<string>();
        foreach (var pasus in tekst.Replace("\r", "").Split('\n'))
        {
            var red = "";
            foreach (var rijec in pasus.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var probni = red.Length == 0 ? rijec : red + " " + rijec;
                if (g.MeasureString(probni, f).Width <= sirina) { red = probni; continue; }
                if (red.Length > 0) redovi.Add(red);
                red = rijec;
                while (g.MeasureString(red, f).Width > sirina && red.Length > 1)
                {
                    var i = red.Length - 1;
                    while (i > 1 && g.MeasureString(red[..i], f).Width > sirina) i--;
                    redovi.Add(red[..i]);
                    red = red[i..];
                }
            }
            redovi.Add(red);
        }
        while (redovi.Count > 1 && redovi[^1].Length == 0) redovi.RemoveAt(redovi.Count - 1);
        return redovi;
    }
}

/// <summary>Arial i Arial Bold iz Windows foldera s fontovima (server i računari su Windows).</summary>
internal sealed class WindowsFontovi : IFontResolver
{
    private static readonly string Folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? "arialbd" : "arial", false, false);

    public byte[]? GetFont(string faceName) => File.ReadAllBytes(Path.Combine(Folder, faceName + ".ttf"));
}

using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Tehnotim.RadniNalozi.Sdp;

/// <summary>Pretvaranje opisa tiketa (HTML maila) u tekst, ćirilica → latinica, prepoznavanje podataka iz teksta.</summary>
public static partial class TekstTiketa
{
    private static readonly Dictionary<char, string> Cir = new()
    {
        ['А']="A",['Б']="B",['В']="V",['Г']="G",['Д']="D",['Ђ']="Đ",['Е']="E",['Ж']="Ž",['З']="Z",['И']="I",['Ј']="J",['К']="K",
        ['Л']="L",['Љ']="Lj",['М']="M",['Н']="N",['Њ']="Nj",['О']="O",['П']="P",['Р']="R",['С']="S",['Т']="T",['Ћ']="Ć",['У']="U",
        ['Ф']="F",['Х']="H",['Ц']="C",['Ч']="Č",['Џ']="Dž",['Ш']="Š",
        ['а']="a",['б']="b",['в']="v",['г']="g",['д']="d",['ђ']="đ",['е']="e",['ж']="ž",['з']="z",['и']="i",['ј']="j",['к']="k",
        ['л']="l",['љ']="lj",['м']="m",['н']="n",['њ']="nj",['о']="o",['п']="p",['р']="r",['с']="s",['т']="t",['ћ']="ć",['у']="u",
        ['ф']="f",['х']="h",['ц']="c",['ч']="č",['џ']="dž",['ш']="š"
    };

    public static string Latinica(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) sb.Append(Cir.TryGetValue(ch, out var l) ? l : ch.ToString());
        return sb.ToString();
    }

    /// <summary>HTML → običan tekst (redovi sačuvani, višestruki prazni redovi spojeni).</summary>
    public static string IzHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var s = Regex.Replace(html, @"<(script|style|head)[\s\S]*?</\1>", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<li[^>]*>", "\n• ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"</(p|div|li|tr|h\d|ul|ol|table)>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = WebUtility.HtmlDecode(s).Replace(' ', ' ');
        var redovi = s.Replace("\r", "").Split('\n').Select(r => Regex.Replace(r, @"[ \t]+", " ").Trim());
        var sb = new StringBuilder();
        var prazan = true;
        foreach (var r in redovi)
        {
            if (r == "") { if (!prazan) sb.Append('\n'); prazan = true; continue; }
            sb.Append(r).Append('\n');
            prazan = false;
        }
        return sb.ToString().Trim();
    }

    /// <summary>Mail iz Redmine-a (RGU): ima „Аутор:” u listi detalja.</summary>
    public static bool JeRedmine(string? html) =>
        !string.IsNullOrEmpty(html) && html.Contains("redmine", StringComparison.OrdinalIgnoreCase) && AutorRegex().IsMatch(html);

    /// <summary>Iz Redmine maila: autor (poslovna jedinica, npr. „Kalinovik PJ”) i tekst zahtjeva (bez zaglavlja i podnožja).</summary>
    public static (string? Autor, string Opis) Redmine(string html)
    {
        var autor = AutorRegex().Match(html) is { Success: true } m ? Latinica(WebUtility.HtmlDecode(m.Groups[1].Value).Trim()) : null;
        string? opis = null;
        foreach (Match p in Regex.Matches(html, @"<p[^>]*>([\s\S]*?)</p>", RegexOptions.IgnoreCase))
        {
            var t = IzHtml(p.Groups[1].Value);
            if (t == "" || t.Contains("информативног", StringComparison.OrdinalIgnoreCase) || t.Contains("informativnog", StringComparison.OrdinalIgnoreCase))
                continue;
            opis = t;
            break;
        }
        return (autor, Latinica(opis ?? IzHtml(html)));
    }

    /// <summary>Eksterni broj iz naslova: broj iza # (npr. „[TEHNOTIM - Хардвер #39422] …”).</summary>
    public static string? EksterniBroj(string? naslov) =>
        naslov is not null && Regex.Match(naslov, @"#(\d{2,})") is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Inventurni broj iz teksta („inv.broj: 15252”, „inventurni broj 15252”).</summary>
    public static string? InventurniBroj(string tekst) =>
        Regex.Match(tekst, @"\binv(?:entarski|enturni)?\.?\s*(?:br(?:oj)?)?\.?\s*[:\-]?\s*(\d{3,})", RegexOptions.IgnoreCase) is { Success: true } m
            ? m.Groups[1].Value : null;

    /// <summary>Ime ispod pozdrava („S poštovanjem! Nikola Čorlija, šef PJ…” → „Nikola Čorlija”).</summary>
    public static string? Potpisnik(string tekst)
    {
        var m = Regex.Match(tekst, @"(?:s\s+po[šs]tovanjem|pozdrav|lp\.?)\s*[!,.]?\s*\n+\s*([^\n]{3,80})", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var ime = m.Groups[1].Value.Split(',')[0].Trim().TrimEnd('.');
        return ime.Contains('@') || ime.Contains("http", StringComparison.OrdinalIgnoreCase) || ime.Length < 3 ? null : ime;
    }

    private static readonly HashSet<string> Zanemari = new(StringComparer.OrdinalIgnoreCase)
    {
        "pj", "op", "filijala", "poslovna", "jedinica", "ispostava", "podrucje", "područje", "operativno", "centar",
        "ad", "doo", "d", "o", "a", "i", "u", "za", "rs", "bih", "republike", "srpske", "republika", "srpska"
    };

    /// <summary>Riječi za poređenje naziva: latinica, mala slova, bez kratkih i opštih riječi.</summary>
    public static HashSet<string> Rijeci(string? s) =>
        Regex.Split(Latinica(s).ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(w => w.Length >= 3 && !Zanemari.Contains(w)).ToHashSet();

    [GeneratedRegex(@"(?:Аутор|Autor):\s*</strong>\s*([^<]+)", RegexOptions.IgnoreCase)]
    private static partial Regex AutorRegex();
}

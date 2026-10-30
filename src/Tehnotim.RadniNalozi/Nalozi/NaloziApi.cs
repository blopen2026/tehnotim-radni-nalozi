using System.Data.Common;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Prijava;

namespace Tehnotim.RadniNalozi.Nalozi;

/// <summary>
/// Radni nalozi: čitanje, dodavanje i izmjena. Svaki upisani korisnik smije praviti i mijenjati naloge.
/// Čuvanje radi sve u jednoj transakciji: jedinica, uređaji, materijal, broj naloga, stavke, istorija.
/// </summary>
public static class NaloziApi
{
    private static readonly string[] Statusi = ["OBAVLJENA", "DJELIMIČNO OBAVLJENA", "NIJE OBAVLJENA", "NIJE POTREBNA"];
    private static readonly string[] Intervencije = ["NA TERENU", "SERVIS/KANCELARIJA"];

    public static void MapNalozi(this WebApplication app, string connectionString)
    {
        var nalozi = app.MapGroup("/api/nalozi").RequireAuthorization(Politike.Korisnik);

        // Lista naloga s pretragom i filterima; najnoviji prvi, po stranicama
        nalozi.MapGet("", async (string? q, string? faza, int? klijentId, int? nosilacId, DateTime? od, DateTime? @do,
                                 int? stranica, int? velicina) =>
        {
            var vel = Math.Clamp(velicina ?? 50, 1, 200);
            var str = Math.Max(stranica ?? 1, 1);
            var trazi = string.IsNullOrWhiteSpace(q) ? null : "%" + q.Trim() + "%";
            var p = new { trazi, klijentId, nosilacId, od = od?.Date, doDatum = @do?.Date, faza, preskoci = (str - 1) * vel, vel };

            const string uslovBezFaze = @"
                WHERE (@klijentId IS NULL OR n.KlijentId = @klijentId)
                  AND (@nosilacId IS NULL OR n.NosilacKorisnikId = @nosilacId)
                  AND (@od IS NULL OR n.DatumIzdavanja >= @od)
                  AND (@doDatum IS NULL OR n.DatumIzdavanja <= @doDatum)
                  AND (@trazi IS NULL OR n.Broj LIKE @trazi OR n.PozivInterni LIKE @trazi OR n.PozivEksterni LIKE @trazi
                       OR n.KlijentNaziv LIKE @trazi OR n.Lokacija LIKE @trazi OR n.NosilacIme LIKE @trazi
                       OR n.OpisZahtjeva LIKE @trazi OR n.OpisIntervencije LIKE @trazi
                       OR EXISTS (SELECT 1 FROM dbo.NalogUredjaj nu WHERE nu.NalogId = n.Id
                                  AND (nu.SerijskiBroj LIKE @trazi OR nu.InventurniBroj LIKE @trazi OR nu.Model LIKE @trazi)))";
            const string uslov = uslovBezFaze + " AND (@faza IS NULL OR n.Faza = @faza)";

            await using var c = new SqlConnection(connectionString);
            var lista = await c.QueryAsync<NalogUListi>(@"
                SELECT n.Id, n.Broj, n.Faza, n.DatumIzdavanja, n.KlijentNaziv, n.Lokacija, n.NosilacIme, n.PozivInterni, n.PozivEksterni,
                       (SELECT TOP 1 CONCAT(nu.Model, CASE WHEN nu.Model IS NOT NULL AND nu.SerijskiBroj IS NOT NULL THEN ' · ' END, nu.SerijskiBroj)
                        FROM dbo.NalogUredjaj nu WHERE nu.NalogId = n.Id ORDER BY nu.Redoslijed) AS PrviUredjaj,
                       (SELECT COUNT(*) FROM dbo.NalogUredjaj nu WHERE nu.NalogId = n.Id) AS BrojUredjaja
                FROM dbo.Nalog n" + uslov + @"
                ORDER BY n.DatumIzdavanja DESC, n.Id DESC
                OFFSET @preskoci ROWS FETCH NEXT @vel ROWS ONLY", p);
            var ukupno = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Nalog n" + uslov, p);
            var poFazama = (await c.QueryAsync<(string Faza, int Broj)>(
                "SELECT n.Faza, COUNT(*) FROM dbo.Nalog n" + uslovBezFaze + " GROUP BY n.Faza", p))
                .ToDictionary(x => x.Faza, x => x.Broj);
            return Results.Ok(new { nalozi = lista, ukupno, stranica = str, velicina = vel, poFazama });
        });

        // Ko je i kad mijenjao nalog
        nalozi.MapGet("/{id:int}/istorija", async (int id) =>
        {
            await using var c = new SqlConnection(connectionString);
            return Results.Ok(await c.QueryAsync<IzmjenaNaloga>(
                "SELECT Vrijeme, Korisnik, Opis FROM dbo.NalogIzmjena WHERE NalogId = @id ORDER BY Vrijeme DESC, Id DESC", new { id }));
        });

        // Brisanje (kancelarija, administrator). Broj se ne koristi ponovo; brojač uređaja se vraća na zadnji preostali nalog.
        nalozi.MapDelete("/{id:int}", async (int id) =>
        {
            await using var c = new SqlConnection(connectionString);
            await c.OpenAsync();
            await using DbTransaction tx = await c.BeginTransactionAsync();
            var uredjaji = (await c.QueryAsync<int>(
                "SELECT DISTINCT UredjajId FROM dbo.NalogUredjaj WHERE NalogId = @id AND UredjajId IS NOT NULL AND (Kolor IS NOT NULL OR CB IS NOT NULL)",
                new { id }, tx)).ToList();
            var n = await c.ExecuteAsync("DELETE FROM dbo.Nalog WHERE Id = @id", new { id }, tx);
            if (n == 0) return Results.NotFound(new { greska = "Nalog ne postoji." });
            foreach (var uid in uredjaji)
                await c.ExecuteAsync(@"
                    UPDATE u SET ZadnjiKolor = z.Kolor, ZadnjiCB = z.CB, BrojacDatum = z.DatumIzdavanja
                    FROM dbo.Uredjaj u
                    CROSS APPLY (SELECT TOP 1 nu.Kolor, nu.CB, nn.DatumIzdavanja
                                 FROM dbo.NalogUredjaj nu JOIN dbo.Nalog nn ON nn.Id = nu.NalogId
                                 WHERE nu.UredjajId = u.Id AND (nu.Kolor IS NOT NULL OR nu.CB IS NOT NULL)
                                 ORDER BY nn.DatumIzdavanja DESC, nn.Id DESC) z
                    WHERE u.Id = @uid", new { uid }, tx);
            await tx.CommitAsync();
            return Results.Ok();
        }).RequireAuthorization(Politike.Kancelarija);

        nalozi.MapGet("/{id:int}", async (int id) =>
        {
            await using var c = new SqlConnection(connectionString);
            var n = await c.QuerySingleOrDefaultAsync<NalogDetalji>(@"
                SELECT Id, Broj, Faza, PozivInterni, PozivEksterni, SdpZahtjevId, DatumIzdavanja, ZahtjevVrijeme, PocetakRada, ZavrsetakRada,
                       UtrosenoVrijeme, UtrosenoRucno, KlijentId, PoslovnaJedinicaId, KlijentNaziv, Lokacija, Adresa, Kontakt,
                       Naplata, Ugovor, Garancija, UredjajPredao, NosilacKorisnikId, NosilacIme, OpisZahtjeva, OpisIntervencije,
                       PutDestinacija, KmTamo, KmNazad, TarifaKm, PutniIznos, Status, Intervencija, PotpisKasnije,
                       PotpisServiser, PotpisKorisnik, Kreirano, Kreirao, Izmijenjeno, Izmijenio
                FROM dbo.Nalog WHERE Id = @id", new { id });
            if (n is null) return Results.NotFound(new { greska = "Nalog ne postoji." });

            n.Uredjaji = (await c.QueryAsync<NalogUredjajDetalji>(@"
                SELECT nu.UredjajId, nu.SerijskiBroj, nu.Model, nu.InventurniBroj, nu.TipUredjajaId,
                       ISNULL(t.PratiBrojac, 0) AS PratiBrojac, nu.SamoCrnoBijeli, nu.Kolor, nu.CB
                FROM dbo.NalogUredjaj nu LEFT JOIN dbo.TipUredjaja t ON t.Id = nu.TipUredjajaId
                WHERE nu.NalogId = @id ORDER BY nu.Redoslijed", new { id })).ToList();
            n.Materijal = (await c.QueryAsync<NalogMaterijalDetalji>(
                "SELECT Naziv, Kolicina, BrojOtpremnice FROM dbo.NalogMaterijal WHERE NalogId = @id ORDER BY Redoslijed", new { id })).ToList();
            return Results.Ok(n);
        });

        // Prethodno stanje brojača uređaja: zadnji drugi nalog s brojačem; za novi nalog i ručno upisano stanje
        nalozi.MapGet("/prethodni-brojac", async (int uredjajId, int? nalogId) =>
        {
            await using var c = new SqlConnection(connectionString);
            var izNaloga = await c.QueryFirstOrDefaultAsync<PrethodniBrojac>(@"
                SELECT TOP 1 nu.Kolor, nu.CB, n.DatumIzdavanja AS Datum, n.Broj AS IzNaloga
                FROM dbo.NalogUredjaj nu JOIN dbo.Nalog n ON n.Id = nu.NalogId
                WHERE nu.UredjajId = @uredjajId AND (@nalogId IS NULL OR n.Id <> @nalogId)
                  AND (nu.Kolor IS NOT NULL OR nu.CB IS NOT NULL)
                ORDER BY n.DatumIzdavanja DESC, n.Id DESC", new { uredjajId, nalogId });
            if (izNaloga is not null) return Results.Ok(izNaloga);
            if (nalogId is not null) return Results.Ok(new PrethodniBrojac());
            var rucno = await c.QueryFirstOrDefaultAsync<PrethodniBrojac>(
                "SELECT ZadnjiKolor AS Kolor, ZadnjiCB AS CB, BrojacDatum AS Datum FROM dbo.Uredjaj WHERE Id = @uredjajId", new { uredjajId });
            return Results.Ok(rucno ?? new PrethodniBrojac());
        });

        nalozi.MapPost("", (NalogUnos u, ClaimsPrincipal ko) => Sacuvaj(connectionString, null, u, ko));
        nalozi.MapPut("/{id:int}", (int id, NalogUnos u, ClaimsPrincipal ko) => Sacuvaj(connectionString, id, u, ko));
    }

    public class PrethodniBrojac
    {
        public int? Kolor { get; set; }
        public int? CB { get; set; }
        public DateTime? Datum { get; set; }
        public string? IzNaloga { get; set; }
    }

    private class UredjajStanje
    {
        public int Id { get; set; }
        public int? KlijentId { get; set; }
    }

    private static string? P(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static readonly byte[] PngPotpis = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>„data:image/png;base64,…” ili čist base64 → bajtovi PNG slike; prazno → null.</summary>
    private static byte[]? PotpisIzTeksta(string? tekst)
    {
        if (string.IsNullOrWhiteSpace(tekst)) return null;
        var b64 = tekst.Trim();
        var zarez = b64.IndexOf(',');
        if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && zarez > 0) b64 = b64[(zarez + 1)..];
        byte[] bajtovi;
        try { bajtovi = Convert.FromBase64String(b64); }
        catch (FormatException) { throw new FormatException("Potpis nije ispravna slika."); }
        if (bajtovi.Length > 500_000) throw new FormatException("Slika potpisa je prevelika.");
        if (bajtovi.Length < 8 || !bajtovi.AsSpan(0, 8).SequenceEqual(PngPotpis)) throw new FormatException("Potpis mora biti PNG slika.");
        return bajtovi;
    }

    private static readonly Regex TelefonRx = new(@"\+?\d[\d\s/\-]{4,}\d", RegexOptions.Compiled);
    private static readonly Regex EmailRx = new(@"[^\s,;]+@[^\s,;]+\.[^\s,;]+", RegexOptions.Compiled);

    /// <summary>„Marko Marković, 065 123 456, marko@firma.ba” → (Marko Marković, 065 123 456, marko@firma.ba)</summary>
    public static (string? Osoba, string? Telefon, string? Email) RazdvojiKontakt(string? kontakt)
    {
        if (string.IsNullOrWhiteSpace(kontakt)) return (null, null, null);
        var emailovi = EmailRx.Matches(kontakt).Select(m => m.Value).ToList();
        var bezEmaila = EmailRx.Replace(kontakt, " ");
        var telefoni = TelefonRx.Matches(bezEmaila).Select(m => Regex.Replace(m.Value.Trim(), @"\s+", " ")).ToList();
        var ostatak = TelefonRx.Replace(bezEmaila, " ");
        ostatak = Regex.Replace(ostatak, @"(?i)\b(tel|telefon|mob|mobitel|e-?mail|kontakt)\b\.?:?", " ");
        ostatak = Regex.Replace(ostatak, @"[\s,;/]+", " ").Trim(' ', ',', ';', '-', ':');
        return (P(ostatak), telefoni.Count > 0 ? string.Join(", ", telefoni) : null, emailovi.Count > 0 ? string.Join(", ", emailovi) : null);
    }
    private static string? DaNe(string? s) => s is "DA" or "NE" ? s : null;
    private static IResult Greska(string poruka) => Results.BadRequest(new { greska = poruka });

    private static async Task<IResult> Sacuvaj(string connectionString, int? id, NalogUnos u, ClaimsPrincipal ko)
    {
        if (u.KlijentId <= 0) return Greska("Izaberite korisnika (klijenta) iz liste.");
        if (u.DatumIzdavanja == default) return Greska("Upišite datum izdavanja.");
        var status = P(u.Status);
        if (status is not null && !Statusi.Contains(status)) return Greska("Nepoznat status.");
        var intervencija = P(u.Intervencija);
        if (intervencija is not null && !Intervencije.Contains(intervencija)) return Greska("Nepoznata vrsta intervencije.");
        if (u.KmTamo < 0 || u.KmNazad < 0 || u.TarifaKm < 0) return Greska("Kilometri i tarifa ne mogu biti negativni.");

        byte[]? potpisServiser, potpisKorisnik;
        try
        {
            potpisServiser = PotpisIzTeksta(u.PotpisServiser);
            potpisKorisnik = PotpisIzTeksta(u.PotpisKorisnik);
        }
        catch (FormatException ex) { return Greska(ex.Message); }

        var datum = u.DatumIzdavanja.Date;
        var korisnik = ko.FindFirstValue(Tvrdnje.Ime) ?? ko.Identity?.Name;

        await using var c = new SqlConnection(connectionString);
        await c.OpenAsync();
        await using DbTransaction tx = await c.BeginTransactionAsync();

        // Postojeći nalog
        string? postojeciBroj = null;
        var imaPotpisKorisnika = false;
        if (id is not null)
        {
            var post = await c.QuerySingleOrDefaultAsync<(string Broj, bool ImaPotpis)>(
                "SELECT Broj, CAST(CASE WHEN PotpisKorisnik IS NULL THEN 0 ELSE 1 END AS bit) FROM dbo.Nalog WITH (UPDLOCK) WHERE Id = @id",
                new { id }, tx);
            if (post.Broj is null) return Results.NotFound(new { greska = "Nalog ne postoji." });
            postojeciBroj = post.Broj;
            imaPotpisKorisnika = post.ImaPotpis;
        }

        // Klijent
        var klijentNaziv = await c.ExecuteScalarAsync<string?>("SELECT Naziv FROM dbo.Klijent WHERE Id = @k", new { k = u.KlijentId }, tx);
        if (klijentNaziv is null) return Greska("Izabrani klijent ne postoji.");

        // Poslovna jedinica: izabrana iz baze, ili nova po upisanom nazivu
        int? jedinicaId = null;
        if (u.PoslovnaJedinicaId is int pj)
        {
            var kid = await c.ExecuteScalarAsync<int?>("SELECT KlijentId FROM dbo.PoslovnaJedinica WHERE Id = @pj", new { pj }, tx);
            if (kid != u.KlijentId) return Greska("Poslovna jedinica ne pripada izabranom klijentu.");
            jedinicaId = pj;
        }
        else if (P(u.Lokacija) is string lok)
        {
            jedinicaId = await c.ExecuteScalarAsync<int?>(
                "SELECT Id FROM dbo.PoslovnaJedinica WHERE KlijentId = @k AND Naziv = @lok", new { k = u.KlijentId, lok }, tx);
            if (jedinicaId is null)
            {
                // Nova jedinica: iz polja „Kontakt podaci” razdvoji telefone, e-mail i ime kontakt osobe
                var (osoba, telefon, email) = RazdvojiKontakt(u.Kontakt);
                jedinicaId = await c.ExecuteScalarAsync<int>(@"
                    INSERT INTO dbo.PoslovnaJedinica (KlijentId, Naziv, Mjesto, Adresa, KontaktOsoba, Telefon, Email, KmOdBL)
                    OUTPUT INSERTED.Id VALUES (@k, @lok, @mjesto, @adresa, @osoba, @telefon, @email, @km)",
                    new { k = u.KlijentId, lok, mjesto = P(u.PutDestinacija), adresa = P(u.Adresa), osoba, telefon, email, km = u.KmTamo ?? 0 }, tx);
            }
        }

        // Nosilac
        string? nosilacIme = null;
        if (u.NosilacKorisnikId is int nid)
        {
            nosilacIme = await c.ExecuteScalarAsync<string?>(
                "SELECT COALESCE(ImePrezime, KorisnickiNalog) FROM dbo.Korisnik WHERE Id = @nid", new { nid }, tx);
            if (nosilacIme is null) return Greska("Izabrani nosilac naloga ne postoji.");
        }

        // Uređaji: postojeći se dopunjavaju, novi se dodaju; brojač se ažurira samo iz novijeg naloga
        var stavkeUredjaja = new List<(int? UredjajId, NalogUredjajUnos R, int? Kolor, int? CB)>();
        foreach (var r in u.Uredjaji ?? [])
        {
            r.SerijskiBroj = P(r.SerijskiBroj);
            r.Model = P(r.Model);
            r.InventurniBroj = P(r.InventurniBroj);
            if (r.SerijskiBroj is null && r.Model is null && r.InventurniBroj is null) continue;
            if (r.Kolor < 0 || r.CB < 0) return Greska("Stanje brojača ne može biti negativno.");

            var prati = r.TipUredjajaId is int tip
                && await c.ExecuteScalarAsync<bool>("SELECT PratiBrojac FROM dbo.TipUredjaja WHERE Id = @tip", new { tip }, tx);
            int? kolor = prati && !r.SamoCrnoBijeli ? r.Kolor : null;
            int? cb = prati ? r.CB : null;
            var samoCb = prati && r.SamoCrnoBijeli;

            int? uredjajId = null;
            if (r.SerijskiBroj is not null)
            {
                var post = await c.QuerySingleOrDefaultAsync<UredjajStanje>(
                    "SELECT Id, KlijentId FROM dbo.Uredjaj WITH (UPDLOCK) WHERE SerijskiBroj = @s", new { s = r.SerijskiBroj }, tx);
                if (post is null)
                {
                    uredjajId = await c.ExecuteScalarAsync<int>(@"
                        INSERT INTO dbo.Uredjaj (SerijskiBroj, InventurniBroj, Model, TipUredjajaId, SamoCrnoBijeli, KlijentId, PoslovnaJedinicaId)
                        OUTPUT INSERTED.Id VALUES (@ser, @inv, @model, @tip, @samoCb, @k, @jed)",
                        new { ser = r.SerijskiBroj, inv = r.InventurniBroj, model = r.Model, tip = r.TipUredjajaId, samoCb, k = u.KlijentId, jed = jedinicaId }, tx);
                }
                else
                {
                    uredjajId = post.Id;
                    await c.ExecuteAsync(@"
                        UPDATE dbo.Uredjaj SET
                            Model = COALESCE(@model, Model),
                            InventurniBroj = COALESCE(@inv, InventurniBroj),
                            TipUredjajaId = COALESCE(@tip, TipUredjajaId),
                            SamoCrnoBijeli = CASE WHEN @tip IS NULL THEN SamoCrnoBijeli ELSE @samoCb END,
                            PoslovnaJedinicaId = CASE WHEN ISNULL(KlijentId, @k) = @k AND @jed IS NOT NULL THEN @jed ELSE PoslovnaJedinicaId END,
                            KlijentId = ISNULL(KlijentId, @k)
                        WHERE Id = @id",
                        new { id = post.Id, model = r.Model, inv = r.InventurniBroj, tip = r.TipUredjajaId, samoCb, k = u.KlijentId, jed = jedinicaId }, tx);
                }

                if (kolor is not null || cb is not null)
                    await c.ExecuteAsync(@"
                        UPDATE dbo.Uredjaj SET ZadnjiKolor = COALESCE(@kolor, ZadnjiKolor), ZadnjiCB = COALESCE(@cb, ZadnjiCB), BrojacDatum = @datum
                        WHERE Id = @id AND (BrojacDatum IS NULL OR BrojacDatum <= @datum)",
                        new { id = uredjajId, kolor, cb, datum }, tx);
            }
            stavkeUredjaja.Add((uredjajId, r, kolor, cb));
        }

        // Materijal: novi naziv se dodaje u šifarnik
        var stavkeMaterijala = new List<(int MaterijalId, string Naziv, decimal? Kolicina, string? Otpremnica)>();
        foreach (var m in u.Materijal ?? [])
        {
            var naziv = P(m.Naziv);
            if (naziv is null) continue;
            var mid = await c.ExecuteScalarAsync<int?>("SELECT Id FROM dbo.Materijal WHERE Naziv = @naziv", new { naziv }, tx)
                      ?? await c.ExecuteScalarAsync<int>("INSERT INTO dbo.Materijal (Naziv) OUTPUT INSERTED.Id VALUES (@naziv)", new { naziv }, tx);
            stavkeMaterijala.Add((mid, naziv, m.Kolicina, P(m.BrojOtpremnice)));
        }

        // Putni troškovi i faza
        decimal? putniIznos = u.TarifaKm is decimal t && (u.KmTamo is not null || u.KmNazad is not null)
            ? Math.Round(((u.KmTamo ?? 0) + (u.KmNazad ?? 0)) * t, 2) : null;
        var faza = potpisKorisnik is not null ? "zavrsen" : u.PotpisKasnije ? "ceka_potpis" : "u_radu";
        var potpisKasnije = potpisKorisnik is null && u.PotpisKasnije;

        var p = new
        {
            u.PozivInterni, u.PozivEksterni, datum, u.ZahtjevVrijeme, u.PocetakRada, u.ZavrsetakRada,
            UtrosenoVrijeme = P(u.UtrosenoVrijeme), u.UtrosenoRucno,
            u.KlijentId, jedinicaId, klijentNaziv, Lokacija = P(u.Lokacija), Adresa = P(u.Adresa), Kontakt = P(u.Kontakt),
            Naplata = DaNe(u.Naplata), Ugovor = DaNe(u.Ugovor), Garancija = DaNe(u.Garancija), UredjajPredao = P(u.UredjajPredao),
            u.NosilacKorisnikId, nosilacIme, OpisZahtjeva = P(u.OpisZahtjeva), OpisIntervencije = P(u.OpisIntervencije),
            PutDestinacija = P(u.PutDestinacija), u.KmTamo, u.KmNazad, u.TarifaKm, putniIznos,
            status, intervencija, PotpisKasnije = potpisKasnije, potpisServiser, potpisKorisnik, faza, korisnik, id
        };

        int nalogId;
        if (id is null)
        {
            // Novi broj u godini datuma izdavanja; brojač zaključan do kraja transakcije
            var godina = datum.Year;
            var redniBroj = await c.ExecuteScalarAsync<int>(@"
                DECLARE @z int = (SELECT Zadnji FROM dbo.BrojacNaloga WITH (UPDLOCK, HOLDLOCK) WHERE Godina = @godina);
                DECLARE @m int = (SELECT ISNULL(MAX(RedniBroj), 0) FROM dbo.Nalog WITH (UPDLOCK, HOLDLOCK) WHERE Godina = @godina);
                DECLARE @novi int = (CASE WHEN ISNULL(@z, 0) > @m THEN ISNULL(@z, 0) ELSE @m END) + 1;
                IF @z IS NULL INSERT INTO dbo.BrojacNaloga (Godina, Zadnji) VALUES (@godina, @novi);
                ELSE UPDATE dbo.BrojacNaloga SET Zadnji = @novi WHERE Godina = @godina;
                SELECT @novi;", new { godina }, tx);

            nalogId = await c.ExecuteScalarAsync<int>(@"
                INSERT INTO dbo.Nalog (Godina, RedniBroj, Faza, PozivInterni, PozivEksterni, DatumIzdavanja, ZahtjevVrijeme, PocetakRada, ZavrsetakRada,
                    UtrosenoVrijeme, UtrosenoRucno, KlijentId, PoslovnaJedinicaId, KlijentNaziv, Lokacija, Adresa, Kontakt,
                    Naplata, Ugovor, Garancija, UredjajPredao, NosilacKorisnikId, NosilacIme, OpisZahtjeva, OpisIntervencije,
                    PutDestinacija, KmTamo, KmNazad, TarifaKm, PutniIznos, Status, Intervencija, PotpisKasnije, PotpisServiser, PotpisKorisnik, Kreirao)
                OUTPUT INSERTED.Id
                VALUES (@godina, @redniBroj, @faza, @PozivInterni, @PozivEksterni, @datum, @ZahtjevVrijeme, @PocetakRada, @ZavrsetakRada,
                    @UtrosenoVrijeme, @UtrosenoRucno, @KlijentId, @jedinicaId, @klijentNaziv, @Lokacija, @Adresa, @Kontakt,
                    @Naplata, @Ugovor, @Garancija, @UredjajPredao, @NosilacKorisnikId, @nosilacIme, @OpisZahtjeva, @OpisIntervencije,
                    @PutDestinacija, @KmTamo, @KmNazad, @TarifaKm, @putniIznos, @status, @intervencija, @PotpisKasnije, @potpisServiser, @potpisKorisnik, @korisnik)",
                new DynamicParameters(p).Also(d => { d.Add("godina", godina); d.Add("redniBroj", redniBroj); }), tx);
        }
        else
        {
            nalogId = id.Value;
            await c.ExecuteAsync(@"
                UPDATE dbo.Nalog SET Faza = @faza, PozivInterni = @PozivInterni, PozivEksterni = @PozivEksterni, DatumIzdavanja = @datum,
                    ZahtjevVrijeme = @ZahtjevVrijeme, PocetakRada = @PocetakRada, ZavrsetakRada = @ZavrsetakRada,
                    UtrosenoVrijeme = @UtrosenoVrijeme, UtrosenoRucno = @UtrosenoRucno, KlijentId = @KlijentId, PoslovnaJedinicaId = @jedinicaId,
                    KlijentNaziv = @klijentNaziv, Lokacija = @Lokacija, Adresa = @Adresa, Kontakt = @Kontakt,
                    Naplata = @Naplata, Ugovor = @Ugovor, Garancija = @Garancija, UredjajPredao = @UredjajPredao,
                    NosilacKorisnikId = @NosilacKorisnikId, NosilacIme = @nosilacIme, OpisZahtjeva = @OpisZahtjeva, OpisIntervencije = @OpisIntervencije,
                    PutDestinacija = @PutDestinacija, KmTamo = @KmTamo, KmNazad = @KmNazad, TarifaKm = @TarifaKm, PutniIznos = @putniIznos,
                    Status = @status, Intervencija = @intervencija, PotpisKasnije = @PotpisKasnije,
                    PotpisServiser = @potpisServiser, PotpisKorisnik = @potpisKorisnik,
                    Izmijenjeno = SYSUTCDATETIME(), Izmijenio = @korisnik
                WHERE Id = @id", p, tx);
            await c.ExecuteAsync("DELETE FROM dbo.NalogUredjaj WHERE NalogId = @id; DELETE FROM dbo.NalogMaterijal WHERE NalogId = @id;", new { id }, tx);
        }

        // Stavke
        var red = 0;
        foreach (var (uredjajId, r, kolor, cb) in stavkeUredjaja)
            await c.ExecuteAsync(@"
                INSERT INTO dbo.NalogUredjaj (NalogId, Redoslijed, UredjajId, SerijskiBroj, Model, InventurniBroj, TipUredjajaId, SamoCrnoBijeli, Kolor, CB)
                VALUES (@nalogId, @red, @uredjajId, @ser, @model, @inv, @tip, @samoCb, @kolor, @cb)",
                new { nalogId, red = ++red, uredjajId, ser = r.SerijskiBroj, model = r.Model, inv = r.InventurniBroj, tip = r.TipUredjajaId,
                      samoCb = r.SamoCrnoBijeli, kolor, cb }, tx);
        red = 0;
        foreach (var (mid, naziv, kolicina, otpremnica) in stavkeMaterijala)
            await c.ExecuteAsync(@"
                INSERT INTO dbo.NalogMaterijal (NalogId, Redoslijed, MaterijalId, Naziv, Kolicina, BrojOtpremnice)
                VALUES (@nalogId, @red, @mid, @naziv, @kolicina, @otpremnica)",
                new { nalogId, red = ++red, mid, naziv, kolicina, otpremnica }, tx);

        // Istorija
        await c.ExecuteAsync("INSERT INTO dbo.NalogIzmjena (NalogId, Korisnik, Opis) VALUES (@nalogId, @korisnik, @opis)",
            new { nalogId, korisnik, opis = id is null ? "Nalog kreiran" : "Nalog izmijenjen" }, tx);
        var potpisPromjena = !imaPotpisKorisnika && potpisKorisnik is not null ? "Korisnik usluge potpisao nalog"
                           : imaPotpisKorisnika && potpisKorisnik is null ? "Potpis korisnika uklonjen" : null;
        if (potpisPromjena is not null)
            await c.ExecuteAsync("INSERT INTO dbo.NalogIzmjena (NalogId, Korisnik, Opis) VALUES (@nalogId, @korisnik, @opis)",
                new { nalogId, korisnik, opis = potpisPromjena }, tx);

        var broj = postojeciBroj ?? await c.ExecuteScalarAsync<string>("SELECT Broj FROM dbo.Nalog WHERE Id = @nalogId", new { nalogId }, tx);
        await tx.CommitAsync();
        return Results.Ok(new { id = nalogId, broj, faza });
    }

    /// <summary>Mala pomoć: izvrši akciju nad objektom i vrati ga (za DynamicParameters).</summary>
    private static T Also<T>(this T obj, Action<T> akcija) { akcija(obj); return obj; }
}

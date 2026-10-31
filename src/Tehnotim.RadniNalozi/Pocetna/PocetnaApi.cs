using System.Security.Claims;
using Dapper;
using Microsoft.Data.SqlClient;
using Tehnotim.RadniNalozi.Nalozi;
using Tehnotim.RadniNalozi.Prijava;

namespace Tehnotim.RadniNalozi.Pocetna;

/// <summary>Podaci za početnu stranicu, prema ulozi: serviser vidi samo svoje naloge.</summary>
public static class PocetnaApi
{
    private const string Kolone = @"
        n.Id, n.Broj, n.Faza, n.DatumIzdavanja, n.KlijentNaziv, n.Lokacija, n.NosilacIme, n.PozivInterni, n.PozivEksterni,
        (SELECT TOP 1 CONCAT(nu.Model, CASE WHEN nu.Model IS NOT NULL AND nu.SerijskiBroj IS NOT NULL THEN ' · ' END, nu.SerijskiBroj)
         FROM dbo.NalogUredjaj nu WHERE nu.NalogId = n.Id ORDER BY nu.Redoslijed) AS PrviUredjaj,
        (SELECT COUNT(*) FROM dbo.NalogUredjaj nu WHERE nu.NalogId = n.Id) AS BrojUredjaja";

    public static void MapPocetna(this WebApplication app, string cs)
    {
        app.MapGet("/api/pocetna", async (ClaimsPrincipal ko) =>
        {
            await using var c = new SqlConnection(cs);
            var ja = await c.QuerySingleAsync<(int Id, string Uloga)>(
                "SELECT Id, Uloga FROM dbo.Korisnik WHERE KorisnickiNalog = @n", new { n = ko.Identity?.Name });

            if (ja.Uloga == Uloge.Serviser)
            {
                var aktivni = await c.QueryAsync<NalogUListi>($@"
                    SELECT {Kolone} FROM dbo.Nalog n
                    WHERE n.NosilacKorisnikId = @id AND n.Faza IN ('novo_iz_tiketa', 'u_radu', 'ceka_potpis')
                    ORDER BY CASE n.Faza WHEN 'ceka_potpis' THEN 1 ELSE 0 END, n.DatumIzdavanja DESC, n.Id DESC", new { id = ja.Id });
                var posljednji = await c.QueryAsync<NalogUListi>($@"
                    SELECT TOP 10 {Kolone} FROM dbo.Nalog n WHERE n.NosilacKorisnikId = @id
                    ORDER BY n.DatumIzdavanja DESC, n.Id DESC", new { id = ja.Id });
                return Results.Ok(new { uloga = ja.Uloga, korisnikId = ja.Id, aktivni, posljednji });
            }

            var odMjeseca = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var f = await c.QuerySingleAsync<(int URadu, int CekaPotpis, int ZavrsenoMjesec)>(@"
                SELECT (SELECT COUNT(*) FROM dbo.Nalog WHERE Faza IN ('novo_iz_tiketa', 'u_radu')),
                       (SELECT COUNT(*) FROM dbo.Nalog WHERE Faza = 'ceka_potpis'),
                       (SELECT COUNT(*) FROM dbo.Nalog WHERE Faza = 'zavrsen' AND DatumIzdavanja >= @odMjeseca)", new { odMjeseca });
            var sviPosljednji = await c.QueryAsync<NalogUListi>($@"
                SELECT TOP 10 {Kolone} FROM dbo.Nalog n ORDER BY n.DatumIzdavanja DESC, n.Id DESC");
            return Results.Ok(new
            {
                uloga = ja.Uloga, korisnikId = ja.Id,
                poFazama = new { uRadu = f.URadu, cekaPotpis = f.CekaPotpis, zavrsenoMjesec = f.ZavrsenoMjesec },
                posljednji = sviPosljednji
            });
        }).RequireAuthorization(Politike.Korisnik);
    }
}

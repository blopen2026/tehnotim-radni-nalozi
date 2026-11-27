using System.Net;
using Tehnotim.RadniNalozi.Nalozi;
using Tehnotim.RadniNalozi.Sifarnici;

namespace Tehnotim.RadniNalozi.Potvrda;

/// <summary>Stranica koju korisnik otvara iz e-maila: pregled naloga, „Potvrđujem” ili primjedba. Sve je u jednoj stranici (bez prijave).</summary>
public static class StranicaPotvrde
{
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string Hbr(string? s) => H(s).Replace("\r", "").Replace("\n", "<br>");
    private static string Vrijeme(DateTime? d) => d is null ? "" : d.Value.ToString("dd.MM.yyyy.") + " u " + d.Value.ToString("HH:mm");

    public static string Napravi(NalogDetalji? n, Postavke firma, string? logoBase64)
    {
        string sadrzaj;
        if (n is null)
            sadrzaj = """
                <div class="poruka"><h1>Link više ne važi</h1>
                <p>Moguće je da vam je u međuvremenu poslan noviji link za ovaj nalog – provjerite e-mail.
                Ako imate pitanja, javite nam se.</p></div>
                """;
        else
        {
            string stanje;
            if (n.Potvrdjeno is not null)
                stanje = $"""
                    <div class="stanje ok"><b>Nalog je potvrđen</b> – {H(n.PotvrdaIme)}, {H(Vrijeme(n.Potvrdjeno))}. Kod potvrde: <span class="mono">{H(n.PotvrdaKod)}</span>.
                    <br>Na vaš e-mail smo poslali nalog u PDF-u sa upisanom potvrdom. Hvala!</div>
                    """;
            else if (n.PotvrdaPrimjedbaVrijeme is not null)
                stanje = $"""
                    <div class="stanje warn"><b>Primjedba je poslana</b> ({H(Vrijeme(n.PotvrdaPrimjedbaVrijeme))}):<br>{Hbr(n.PotvrdaPrimjedba)}
                    <br><br>Kad nalog bude ispravljen, dobićete novi link za potvrdu.</div>
                    """;
            else
                stanje = $"""
                    <div class="odluka">
                      <p>Molimo vas da pregledate nalog. Potvrdom izjavljujete da ste upoznati sa sadržajem radnog naloga.</p>
                      <button id="potvrdi" class="glavno">Potvrđujem nalog</button>
                      <details id="prim"><summary>Nešto nije tačno – pošalji primjedbu</summary>
                        <textarea id="tekst" rows="4" maxlength="2000" placeholder="Šta treba ispraviti?"></textarea>
                        <button id="posalji">Pošalji primjedbu</button>
                      </details>
                      <p id="greska" class="greska" role="alert"></p>
                    </div>
                    """;

            var uredjaji = n.Uredjaji.Where(u => !string.IsNullOrWhiteSpace(u.Model) || !string.IsNullOrWhiteSpace(u.SerijskiBroj) || !string.IsNullOrWhiteSpace(u.InventurniBroj))
                .Select(u => H(string.Join(" · ", new[] { u.Model, u.SerijskiBroj is null ? null : "SN " + u.SerijskiBroj, u.InventurniBroj is null ? null : "inv. " + u.InventurniBroj }
                    .Where(s => !string.IsNullOrWhiteSpace(s)))));
            var materijal = n.Materijal.Where(m => !string.IsNullOrWhiteSpace(m.Naziv))
                .Select(m => H(m.Naziv + (m.Kolicina is decimal k ? $" × {k:0.##}" : "")));
            var redovi = new List<(string, string?)>
            {
                ("Korisnik", H(n.KlijentNaziv) + (string.IsNullOrWhiteSpace(n.Lokacija) ? "" : "<br>" + H(n.Lokacija))),
                ("Datum izdavanja", H(n.DatumIzdavanja.ToString("dd.MM.yyyy."))),
                ("Rad", n.PocetakRada is null && n.ZavrsetakRada is null ? null
                        : H(string.Join(" – ", new[] { n.PocetakRada?.ToString("dd.MM.yyyy. HH:mm"), n.ZavrsetakRada?.ToString("dd.MM.yyyy. HH:mm") }.Where(s => s is not null)))
                          + (string.IsNullOrWhiteSpace(n.UtrosenoVrijeme) ? "" : $" ({H(n.UtrosenoVrijeme)})")),
                ("Vaš broj zahtjeva", string.IsNullOrWhiteSpace(n.PozivEksterni) ? null : H(n.PozivEksterni)),
                ("Uređaj", uredjaji.Any() ? string.Join("<br>", uredjaji) : null),
                ("Opis zahtjeva", string.IsNullOrWhiteSpace(n.OpisZahtjeva) ? null : Hbr(n.OpisZahtjeva)),
                ("Opis izvršene intervencije", string.IsNullOrWhiteSpace(n.OpisIntervencije) ? null : Hbr(n.OpisIntervencije)),
                ("Utrošeni materijal", materijal.Any() ? string.Join("<br>", materijal) : null),
                ("Status", string.IsNullOrWhiteSpace(n.Status) ? null : H(n.Status) + (string.IsNullOrWhiteSpace(n.Intervencija) ? "" : " · " + H(n.Intervencija))),
                ("Nalog izvršio", string.IsNullOrWhiteSpace(n.NosilacIme) ? null : H(n.NosilacIme)),
            };
            var tabela = string.Concat(redovi.Where(r => r.Item2 is not null).Select(r => $"<tr><th>{H(r.Item1)}</th><td>{r.Item2}</td></tr>"));

            sadrzaj = $"""
                <h1>Radni nalog <span class="mono">{H(n.Broj)}</span></h1>
                <p class="za">Za: <b>{H(n.PotvrdaIme)}</b> ({H(n.PotvrdaEmail)})</p>
                <table>{tabela}</table>
                <p><a class="pdf" href="#" id="pdf" target="_blank" rel="noopener">Otvori nalog (PDF)</a></p>
                {stanje}
                """;
        }

        var logo = logoBase64 is null ? $"<b>{H(firma.NazivFirme)}</b>" : $"<img src=\"data:image/png;base64,{logoBase64}\" alt=\"{H(firma.NazivFirme)}\">";
        var podaci = H(string.Join(" | ", new[] { firma.Adresa, firma.Email, firma.Web }.Where(s => !string.IsNullOrWhiteSpace(s))));

        return $$$"""
            <!doctype html>
            <html lang="sr-Latn"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="robots" content="noindex"><title>Potvrda radnog naloga – {{{H(firma.NazivFirme)}}}</title>
            <style>
              :root{--t:#208293;--line:#d6e2e4;--muted:#5b6b6e;--ok:#1d7a46;--okbg:#e6f4ec;--warn:#8a5a00;--warnbg:#fff4dc}
              *{box-sizing:border-box} body{margin:0;background:#f4f7f8;color:#1a1a1a;font:16px/1.5 Arial,Helvetica,sans-serif}
              header{background:#fff;border-bottom:3px solid var(--t);padding:14px 16px}
              header .u{max-width:760px;margin:0 auto;display:flex;justify-content:space-between;align-items:center;gap:12px;flex-wrap:wrap}
              header img{height:34px} header .d{color:var(--t);font-size:13px;font-weight:bold;text-align:right}
              main{max-width:760px;margin:20px auto;padding:0 16px}
              .k{background:#fff;border:1px solid var(--line);border-radius:10px;padding:20px}
              h1{font-size:22px;margin:0 0 4px} .mono{font-family:Consolas,monospace} .za{color:var(--muted);margin:0 0 14px}
              table{width:100%;border-collapse:collapse} th,td{text-align:left;vertical-align:top;padding:8px 0;border-top:1px solid var(--line)}
              th{width:34%;color:var(--muted);font-weight:normal;padding-right:12px}
              @media(max-width:560px){th,td{display:block;width:auto;border:0;padding:2px 0} th{padding-top:10px;border-top:1px solid var(--line)}}
              a.pdf{color:var(--t);font-weight:bold}
              .odluka{margin-top:18px;padding-top:16px;border-top:2px solid var(--t)}
              button{font:inherit;border:1px solid var(--t);background:#fff;color:var(--t);border-radius:8px;padding:10px 18px;cursor:pointer;font-weight:bold}
              button.glavno{background:var(--t);color:#fff;padding:13px 24px;font-size:17px;width:100%;max-width:340px}
              button:disabled{opacity:.6;cursor:wait}
              details{margin-top:18px} summary{cursor:pointer;color:var(--muted)}
              textarea{display:block;width:100%;margin:10px 0;font:inherit;padding:8px;border:1px solid var(--line);border-radius:8px}
              .stanje{margin-top:18px;padding:14px 16px;border-radius:8px} .ok{background:var(--okbg);color:var(--ok)} .warn{background:var(--warnbg);color:var(--warn)}
              .greska{color:#b3261e} .poruka h1{margin-bottom:10px}
              footer{max-width:760px;margin:0 auto 30px;padding:0 16px;color:var(--muted);font-size:12px;text-align:center}
            </style></head>
            <body>
            <header><div class="u">{{{logo}}}<div class="d">{{{H(firma.NazivFirme)}}}</div></div></header>
            <main><div class="k">{{{sadrzaj}}}</div></main>
            <footer>{{{podaci}}}</footer>
            <script>
              const pdf = document.getElementById('pdf');
              if (pdf) pdf.href = location.pathname.replace(/\/$/, '') + '/pdf';
              async function posalji(akcija, tekst, dugme) {
                const g = document.getElementById('greska');
                g.textContent = ''; dugme.disabled = true;
                try {
                  const r = await fetch(location.pathname, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ akcija, tekst }) });
                  const d = await r.json().catch(() => ({}));
                  if (!r.ok) { g.textContent = d.greska || 'Nije uspjelo, pokušajte ponovo.'; dugme.disabled = false; return; }
                  location.reload();
                } catch { g.textContent = 'Veza nije uspjela, pokušajte ponovo.'; dugme.disabled = false; }
              }
              const p = document.getElementById('potvrdi');
              if (p) p.onclick = () => posalji('potvrdi', null, p);
              const s = document.getElementById('posalji');
              if (s) s.onclick = () => {
                const t = document.getElementById('tekst').value.trim();
                if (t.length < 3) { document.getElementById('greska').textContent = 'Upišite primjedbu.'; return; }
                posalji('primjedba', t, s);
              };
            </script>
            </body></html>
            """;
    }
}

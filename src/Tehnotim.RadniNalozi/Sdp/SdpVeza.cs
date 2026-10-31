using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Tehnotim.RadniNalozi.Sdp;

public class SdpPostavke
{
    public string? SdpUrl { get; set; }
    public string? SdpApiKljuc { get; set; }
    public bool SdpPreskociCertifikat { get; set; }
}

/// <summary>Greška u radu sa SDP-om, s porukom razumljivom korisniku.</summary>
public class SdpGreska(string poruka) : Exception(poruka);

/// <summary>Stavka iz SDP-a (tehničar, grupa…): id i naziv.</summary>
public record SdpStavka(string Id, string Naziv);

/// <summary>
/// Pozivi prema ServiceDesk Plus REST API v3. Adresa i ključ su u dbo.Postavke.
/// SDP 15200 traži TLS 1.3 – HttpClient koristi postavke Windowsa (Server 2022 / Windows 11 to podržavaju).
/// </summary>
public static class SdpVeza
{
    public static async Task<SdpPostavke> PostavkeAsync(string cs)
    {
        await using var c = new SqlConnection(cs);
        return await c.QuerySingleAsync<SdpPostavke>("SELECT SdpUrl, SdpApiKljuc, SdpPreskociCertifikat FROM dbo.Postavke WHERE Id = 1");
    }

    /// <summary>GET prema SDP-u; vraća korijen JSON odgovora. inputData ide kao ?input_data=… (JSON).</summary>
    public static Task<JsonElement> GetAsync(string cs, string putanja, object? inputData = null) =>
        PosaljiAsync(cs, url => new HttpRequestMessage(HttpMethod.Get,
            url + putanja + (inputData is null ? "" : "?input_data=" + Uri.EscapeDataString(JsonSerializer.Serialize(inputData)))));

    /// <summary>POST s poljem input_data (JSON) kao obrazac – tako SDP v3 prima npr. napomene.</summary>
    public static Task<JsonElement> PostAsync(string cs, string putanja, object inputData) =>
        PosaljiAsync(cs, url => new HttpRequestMessage(HttpMethod.Post, url + putanja)
        {
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("input_data", JsonSerializer.Serialize(inputData))])
        });

    /// <summary>POST fajla (multipart) – prilog tiketu.</summary>
    public static Task<JsonElement> PostFajlAsync(string cs, string putanja, byte[] sadrzaj, string imeFajla, string polje)
    {
        return PosaljiAsync(cs, url =>
        {
            var dio = new ByteArrayContent(sadrzaj);
            dio.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            var forma = new MultipartFormDataContent { { dio, polje, imeFajla } };
            return new HttpRequestMessage(HttpMethod.Post, url + putanja) { Content = forma };
        });
    }

    private static async Task<JsonElement> PosaljiAsync(string cs, Func<string, HttpRequestMessage> napravi)
    {
        var p = await PostavkeAsync(cs);
        if (string.IsNullOrWhiteSpace(p.SdpUrl) || string.IsNullOrWhiteSpace(p.SdpApiKljuc))
            throw new SdpGreska("Veza sa ServiceDesk Plus-om nije podešena (Šifarnici → Veza sa ServiceDesk Plus).");

        using var handler = new HttpClientHandler();
        if (p.SdpPreskociCertifikat)
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };

        using var zahtjev = napravi(p.SdpUrl.TrimEnd('/'));
        zahtjev.Headers.TryAddWithoutValidation("authtoken", p.SdpApiKljuc);
        zahtjev.Headers.TryAddWithoutValidation("Accept", "application/vnd.manageengine.sdp.v3+json");

        HttpResponseMessage odgovor;
        try
        {
            odgovor = await http.SendAsync(zahtjev);
        }
        catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
        {
            throw new SdpGreska("Certifikat SDP servera nije prihvaćen. Za testni server uključite „Ne provjeravaj certifikat”, a za produkciju instalirajte certifikat SDP-a među pouzdane na serveru aplikacije.");
        }
        catch (HttpRequestException ex)
        {
            throw new SdpGreska($"ServiceDesk Plus nije dostupan na {p.SdpUrl}: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            throw new SdpGreska($"ServiceDesk Plus ne odgovara (isteklo 60 s) na {p.SdpUrl}.");
        }

        using (odgovor)
        {
            var tekst = await odgovor.Content.ReadAsStringAsync();
            if (odgovor.StatusCode is HttpStatusCode.Unauthorized)
                throw new SdpGreska("ServiceDesk Plus ne prihvata API ključ (provjerite ključ i da tehničar ima prijavu).");
            if (odgovor.StatusCode is HttpStatusCode.Forbidden)
                throw new SdpGreska("Tehničar s ovim API ključem nema pravo na ovu radnju u ServiceDesk Plus-u.");

            JsonElement korijen;
            try
            {
                using var doc = JsonDocument.Parse(tekst);
                korijen = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                throw new SdpGreska($"ServiceDesk Plus je vratio neočekivan odgovor ({(int)odgovor.StatusCode}). Provjerite adresu.");
            }

            if (!odgovor.IsSuccessStatusCode || Status(korijen) == "failed")
                throw new SdpGreska($"ServiceDesk Plus je odbio zahtjev ({(int)odgovor.StatusCode}): {Poruke(korijen)}");
            return korijen;
        }
    }

    /// <summary>response_status je kod jednog zapisa objekat, a kod liste niz objekata.</summary>
    private static JsonElement? StatusObjekat(JsonElement k)
    {
        if (k.ValueKind != JsonValueKind.Object || !k.TryGetProperty("response_status", out var rs)) return null;
        if (rs.ValueKind == JsonValueKind.Object) return rs;
        if (rs.ValueKind == JsonValueKind.Array && rs.GetArrayLength() > 0 && rs[0].ValueKind == JsonValueKind.Object) return rs[0];
        return null;
    }

    private static string? Status(JsonElement k) =>
        StatusObjekat(k) is { } rs && rs.TryGetProperty("status", out var s) ? s.GetString() : null;

    private static string Poruke(JsonElement k)
    {
        if (StatusObjekat(k) is not { } rs) return "bez opisa";
        if (rs.TryGetProperty("messages", out var m) && m.ValueKind == JsonValueKind.Array)
            return string.Join("; ", m.EnumerateArray().Select(x => x.TryGetProperty("message", out var t) ? t.GetString() : x.ToString()));
        return rs.ToString();
    }

    /// <summary>Tekst ili broj iz JSON-a kao string (SDP id-jeve vraća kao string, ali za svaki slučaj).</summary>
    public static string? Tekst(JsonElement e, string ime) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(ime, out var v)
            ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), _ => null }
            : null;

    /// <summary>Prvi niz u korijenu odgovora (npr. "technicians", "groups", "requests").</summary>
    /// Kod liste SDP vraća i "response_status" kao niz – njega preskačemo.
    private static List<JsonElement> PrviNiz(JsonElement k)
    {
        if (k.ValueKind != JsonValueKind.Object) return new List<JsonElement>();
        foreach (var p in k.EnumerateObject())
            if (p.Value.ValueKind == JsonValueKind.Array && p.Name is not ("response_status" or "list_info"))
                return p.Value.EnumerateArray().ToList();
        return new List<JsonElement>();
    }

    /// <summary>
    /// Lista tehničara ili grupa iz SDP-a. Prvo probaju namjenski API pozivi; ako ih ova verzija nema,
    /// lista se sastavi iz zadnjih 500 zahtjeva (tehničari i grupe kojima su dodijeljeni).
    /// </summary>
    public static async Task<List<SdpStavka>> ListaAsync(string cs, string vrsta)
    {
        var putanje = vrsta == "tehnicari" ? new[] { "/api/v3/technicians" } : new[] { "/api/v3/groups", "/api/v3/support_groups" };
        foreach (var putanja in putanje)
        {
            try
            {
                var k = await GetAsync(cs, putanja, new { list_info = new { row_count = 100, start_index = 1 } });
                var lista = PrviNiz(k).Select(x => new SdpStavka(Tekst(x, "id") ?? "", Tekst(x, "name") ?? ""))
                    .Where(x => x.Id != "" && x.Naziv != "").ToList();
                if (lista.Count > 0) return lista.OrderBy(x => x.Naziv).ToList();
            }
            catch (SdpGreska ex) when (ex.Message.Contains("odbio zahtjev") || ex.Message.Contains("neočekivan"))
            {
                // ovaj poziv ne postoji u ovoj verziji – probaj sljedeći / rezervni način
            }
        }

        var polje = vrsta == "tehnicari" ? "technician" : "group";
        var nadjeno = new Dictionary<string, string>();
        for (var start = 1; start <= 401; start += 100)
        {
            var k = await GetAsync(cs, "/api/v3/requests", new { list_info = new { row_count = 100, start_index = start, sort_field = "id", sort_order = "desc" } });
            var zahtjevi = PrviNiz(k);
            foreach (var z in zahtjevi)
                if (z.TryGetProperty(polje, out var t) && t.ValueKind == JsonValueKind.Object && Tekst(t, "id") is { } id)
                    nadjeno[id] = Tekst(t, "name") ?? id;
            if (zahtjevi.Count < 100) break;
        }
        return nadjeno.Select(x => new SdpStavka(x.Key, x.Value)).OrderBy(x => x.Naziv).ToList();
    }
}

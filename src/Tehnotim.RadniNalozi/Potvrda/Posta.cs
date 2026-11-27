using System.Net.Sockets;
using Dapper;
using MailKit.Security;
using Microsoft.Data.SqlClient;
using MimeKit;

namespace Tehnotim.RadniNalozi.Potvrda;

/// <summary>Podešavanje slanja e-maila (Šifarnici → Slanje e-maila) i javna adresa aplikacije za link u e-mailu.</summary>
public class PostaPostavke
{
    public string? SmtpServer { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpSigurnost { get; set; }
    public string? SmtpKorisnik { get; set; }
    public string? SmtpLozinka { get; set; }
    public string? SmtpPosiljalac { get; set; }
    public string? SmtpPosiljalacIme { get; set; }
    public string? JavnaAdresa { get; set; }
    public string? NazivFirme { get; set; }
}

public class PostaGreska(string poruka) : Exception(poruka);

public record PrilogPoste(string Ime, byte[] Sadrzaj);

/// <summary>Slanje e-maila preko SMTP servera firme (MailKit).</summary>
public static class Posta
{
    public static async Task<PostaPostavke> PostavkeAsync(string cs)
    {
        await using var c = new SqlConnection(cs);
        return await c.QuerySingleAsync<PostaPostavke>(@"
            SELECT SmtpServer, SmtpPort, SmtpSigurnost, SmtpKorisnik, SmtpLozinka, SmtpPosiljalac, SmtpPosiljalacIme, JavnaAdresa, NazivFirme
            FROM dbo.Postavke WHERE Id = 1");
    }

    /// <summary>Šta nedostaje da bi se e-mail mogao poslati (null = sve je podešeno).</summary>
    public static string? StaNedostaje(PostaPostavke p) =>
        string.IsNullOrWhiteSpace(p.SmtpServer) || string.IsNullOrWhiteSpace(p.SmtpPosiljalac)
            ? "Slanje e-maila nije podešeno (Šifarnici → Slanje e-maila i potvrda naloga)."
            : null;

    public static async Task PosaljiAsync(PostaPostavke p, string primalacIme, string primalacEmail, string naslov,
                                          string html, string tekst, PrilogPoste? prilog = null)
    {
        if (StaNedostaje(p) is string nedostaje) throw new PostaGreska(nedostaje);

        var poruka = new MimeMessage();
        poruka.From.Add(new MailboxAddress(string.IsNullOrWhiteSpace(p.SmtpPosiljalacIme) ? (p.NazivFirme ?? "") : p.SmtpPosiljalacIme, p.SmtpPosiljalac!.Trim()));
        try { poruka.To.Add(new MailboxAddress(primalacIme ?? "", primalacEmail.Trim())); }
        catch (ParseException) { throw new PostaGreska($"E-mail adresa „{primalacEmail}” nije ispravna."); }
        poruka.Subject = naslov;
        var tijelo = new BodyBuilder { HtmlBody = html, TextBody = tekst };
        if (prilog is not null)
            tijelo.Attachments.Add(prilog.Ime, prilog.Sadrzaj, new MimeKit.ContentType("application", "pdf"));
        poruka.Body = tijelo.ToMessageBody();

        var (opcija, podrazumijevaniPort) = (p.SmtpSigurnost ?? "auto") switch
        {
            "ssl" => (SecureSocketOptions.SslOnConnect, 465),
            "starttls" => (SecureSocketOptions.StartTls, 587),
            "bez" => (SecureSocketOptions.None, 25),
            _ => (SecureSocketOptions.Auto, 587)
        };
        var port = p.SmtpPort is > 0 ? p.SmtpPort.Value : podrazumijevaniPort;

        using var smtp = new MailKit.Net.Smtp.SmtpClient { Timeout = 30_000 };
        try
        {
            await smtp.ConnectAsync(p.SmtpServer!.Trim(), port, opcija);
            if (!string.IsNullOrWhiteSpace(p.SmtpKorisnik))
                await smtp.AuthenticateAsync(p.SmtpKorisnik.Trim(), p.SmtpLozinka ?? "");
            await smtp.SendAsync(poruka);
            await smtp.DisconnectAsync(true);
        }
        catch (AuthenticationException)
        {
            throw new PostaGreska("Mail server je odbio korisničko ime ili lozinku (Šifarnici → Slanje e-maila).");
        }
        catch (SslHandshakeException ex)
        {
            throw new PostaGreska($"Šifrovana veza sa mail serverom nije uspostavljena (provjerite port i vrstu zaštite): {ex.Message}");
        }
        catch (SocketException ex)
        {
            throw new PostaGreska($"Mail server {p.SmtpServer}:{port} nije dostupan: {ex.Message}");
        }
        catch (MailKit.Net.Smtp.SmtpCommandException ex)
        {
            throw new PostaGreska($"Mail server je odbio poruku: {ex.Message}");
        }
        catch (Exception ex) when (ex is not PostaGreska)
        {
            throw new PostaGreska($"E-mail nije poslan: {ex.Message}");
        }
    }
}

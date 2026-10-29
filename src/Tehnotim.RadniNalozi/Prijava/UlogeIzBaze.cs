using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Tehnotim.RadniNalozi.Prijava;

/// <summary>
/// Nakon što Windows prijava utvrdi ko je korisnik (npr. TEHNOTIM\marko), ovdje se iz baze
/// dodaju njegova uloga i ime. Korisnik koji nije upisan ili nije aktivan ne dobija ulogu,
/// pa ga API rute odbijaju.
/// </summary>
public class UlogeIzBaze(Korisnici korisnici, ILogger<UlogeIzBaze> logger) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true, Name: { Length: > 0 } nalog })
            return principal;
        if (principal.HasClaim(c => c.Type == Tvrdnje.Ucitano))
            return principal;

        Korisnik? k = null;
        try
        {
            k = await korisnici.PrijavaAsync(nalog);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Uloga za {Nalog} nije učitana iz baze", nalog);
        }

        var dodatno = new ClaimsIdentity();
        dodatno.AddClaim(new Claim(Tvrdnje.Ucitano, "1"));
        if (k is { Aktivan: true })
        {
            dodatno.AddClaim(new Claim(ClaimTypes.Role, k.Uloga));
            dodatno.AddClaim(new Claim(Tvrdnje.KorisnikId, k.Id.ToString()));
            if (!string.IsNullOrWhiteSpace(k.ImePrezime))
                dodatno.AddClaim(new Claim(Tvrdnje.Ime, k.ImePrezime));
        }
        principal.AddIdentity(dodatno);
        return principal;
    }
}

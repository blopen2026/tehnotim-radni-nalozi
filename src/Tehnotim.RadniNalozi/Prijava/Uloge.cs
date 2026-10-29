namespace Tehnotim.RadniNalozi.Prijava;

public static class Uloge
{
    public const string Serviser = "serviser";
    public const string Kancelarija = "kancelarija";
    public const string Administrator = "administrator";

    public static readonly string[] Sve = [Serviser, Kancelarija, Administrator];
}

/// <summary>Pravila pristupa koja se kače na API rute.</summary>
public static class Politike
{
    /// <summary>Bilo koja uloga (upisan i aktivan korisnik).</summary>
    public const string Korisnik = "Korisnik";
    /// <summary>Kancelarija ili administrator.</summary>
    public const string Kancelarija = "Kancelarija";
    /// <summary>Samo administrator.</summary>
    public const string Administrator = "Administrator";
}

/// <summary>Dodatne tvrdnje (claims) koje aplikacija dodaje prijavljenom korisniku.</summary>
public static class Tvrdnje
{
    public const string Ucitano = "rn:ucitano";
    public const string KorisnikId = "rn:korisnik_id";
    public const string Ime = "rn:ime";
}

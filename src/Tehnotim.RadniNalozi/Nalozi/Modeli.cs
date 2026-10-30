namespace Tehnotim.RadniNalozi.Nalozi;

/// <summary>Ono što forma naloga šalje pri čuvanju.</summary>
public class NalogUnos
{
    public string? PozivInterni { get; set; }
    public string? PozivEksterni { get; set; }
    public DateTime DatumIzdavanja { get; set; }
    public DateTime? ZahtjevVrijeme { get; set; }
    public DateTime? PocetakRada { get; set; }
    public DateTime? ZavrsetakRada { get; set; }
    public string? UtrosenoVrijeme { get; set; }
    public bool UtrosenoRucno { get; set; }

    public int KlijentId { get; set; }
    public int? PoslovnaJedinicaId { get; set; }
    public string? Lokacija { get; set; }       // naziv jedinice; nova se dodaje klijentu
    public string? Adresa { get; set; }
    public string? Kontakt { get; set; }

    public string? Naplata { get; set; }
    public string? Ugovor { get; set; }
    public string? Garancija { get; set; }
    public string? UredjajPredao { get; set; }
    public int? NosilacKorisnikId { get; set; }

    public string? OpisZahtjeva { get; set; }
    public string? OpisIntervencije { get; set; }

    public string? PutDestinacija { get; set; }
    public decimal? KmTamo { get; set; }
    public decimal? KmNazad { get; set; }
    public decimal? TarifaKm { get; set; }

    public string? Status { get; set; }
    public string? Intervencija { get; set; }
    public bool PotpisKasnije { get; set; }

    public List<NalogUredjajUnos> Uredjaji { get; set; } = [];
    public List<NalogMaterijalUnos> Materijal { get; set; } = [];
}

public class NalogUredjajUnos
{
    public string? SerijskiBroj { get; set; }
    public string? Model { get; set; }
    public string? InventurniBroj { get; set; }
    public int? TipUredjajaId { get; set; }
    public bool SamoCrnoBijeli { get; set; }
    public int? Kolor { get; set; }
    public int? CB { get; set; }
}

public class NalogMaterijalUnos
{
    public string? Naziv { get; set; }
    public decimal? Kolicina { get; set; }
    public string? BrojOtpremnice { get; set; }
}

/// <summary>Nalog kako ga čita forma (bez potpisa – korak 9).</summary>
public class NalogDetalji
{
    public int Id { get; set; }
    public string Broj { get; set; } = "";
    public string Faza { get; set; } = "";
    public string? PozivInterni { get; set; }
    public string? PozivEksterni { get; set; }
    public long? SdpZahtjevId { get; set; }
    public DateTime DatumIzdavanja { get; set; }
    public DateTime? ZahtjevVrijeme { get; set; }
    public DateTime? PocetakRada { get; set; }
    public DateTime? ZavrsetakRada { get; set; }
    public string? UtrosenoVrijeme { get; set; }
    public bool UtrosenoRucno { get; set; }
    public int KlijentId { get; set; }
    public int? PoslovnaJedinicaId { get; set; }
    public string KlijentNaziv { get; set; } = "";
    public string? Lokacija { get; set; }
    public string? Adresa { get; set; }
    public string? Kontakt { get; set; }
    public string? Naplata { get; set; }
    public string? Ugovor { get; set; }
    public string? Garancija { get; set; }
    public string? UredjajPredao { get; set; }
    public int? NosilacKorisnikId { get; set; }
    public string? NosilacIme { get; set; }
    public string? OpisZahtjeva { get; set; }
    public string? OpisIntervencije { get; set; }
    public string? PutDestinacija { get; set; }
    public decimal? KmTamo { get; set; }
    public decimal? KmNazad { get; set; }
    public decimal? TarifaKm { get; set; }
    public decimal? PutniIznos { get; set; }
    public string? Status { get; set; }
    public string? Intervencija { get; set; }
    public bool PotpisKasnije { get; set; }
    public DateTime Kreirano { get; set; }
    public string? Kreirao { get; set; }
    public DateTime? Izmijenjeno { get; set; }
    public string? Izmijenio { get; set; }
    public List<NalogUredjajDetalji> Uredjaji { get; set; } = [];
    public List<NalogMaterijalDetalji> Materijal { get; set; } = [];
}

public class NalogUredjajDetalji
{
    public int? UredjajId { get; set; }
    public string? SerijskiBroj { get; set; }
    public string? Model { get; set; }
    public string? InventurniBroj { get; set; }
    public int? TipUredjajaId { get; set; }
    public bool PratiBrojac { get; set; }
    public bool SamoCrnoBijeli { get; set; }
    public int? Kolor { get; set; }
    public int? CB { get; set; }
}

public class NalogMaterijalDetalji
{
    public string Naziv { get; set; } = "";
    public decimal? Kolicina { get; set; }
    public string? BrojOtpremnice { get; set; }
}

public class NalogUListi
{
    public int Id { get; set; }
    public string Broj { get; set; } = "";
    public string Faza { get; set; } = "";
    public DateTime DatumIzdavanja { get; set; }
    public string KlijentNaziv { get; set; } = "";
    public string? Lokacija { get; set; }
    public string? NosilacIme { get; set; }
    public string? PozivInterni { get; set; }
    public string? PozivEksterni { get; set; }
    public string? PrviUredjaj { get; set; }
    public int BrojUredjaja { get; set; }
}

public class IzmjenaNaloga
{
    public DateTime Vrijeme { get; set; }
    public string? Korisnik { get; set; }
    public string? Opis { get; set; }
}

/// <summary>Jedan nalog u istoriji uređaja.</summary>
public class NalogUredjajaStavka
{
    public int Id { get; set; }
    public string Broj { get; set; } = "";
    public string Faza { get; set; } = "";
    public DateTime DatumIzdavanja { get; set; }
    public string? PozivInterni { get; set; }
    public string? NosilacIme { get; set; }
    public string? Status { get; set; }
    public string? Opis { get; set; }
    public int? Kolor { get; set; }
    public int? CB { get; set; }
}

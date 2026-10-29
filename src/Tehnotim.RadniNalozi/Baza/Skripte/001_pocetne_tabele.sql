-- 001 – Početne tabele (prema docs/specifikacija.md, poglavlje „Model podataka”)

CREATE TABLE dbo.Klijent (
    Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Klijent PRIMARY KEY,
    Naziv         nvarchar(200) NOT NULL CONSTRAINT UQ_Klijent_Naziv UNIQUE,
    Skracenica    nvarchar(30)  NULL,
    Ugovor        bit           NOT NULL CONSTRAINT DF_Klijent_Ugovor DEFAULT (0),
    Kreirano      datetime2(0)  NOT NULL CONSTRAINT DF_Klijent_Kreirano DEFAULT (SYSUTCDATETIME())
);

CREATE TABLE dbo.PoslovnaJedinica (
    Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PoslovnaJedinica PRIMARY KEY,
    KlijentId     int           NOT NULL CONSTRAINT FK_PoslovnaJedinica_Klijent REFERENCES dbo.Klijent(Id),
    Naziv         nvarchar(200) NOT NULL,
    Mjesto        nvarchar(100) NULL,
    Adresa        nvarchar(300) NULL,
    PostanskiBroj nvarchar(10)  NULL,
    Telefon       nvarchar(200) NULL,
    RadnoVrijeme  nvarchar(50)  NULL,
    KontaktOsoba  nvarchar(150) NULL,
    Email         nvarchar(200) NULL,
    KmOdBL        decimal(7,1)  NOT NULL CONSTRAINT DF_PoslovnaJedinica_Km DEFAULT (0),
    SdpLokacija   nvarchar(200) NULL,   -- naziv site/department u ServiceDesk Plus (faza 2)
    CONSTRAINT UQ_PoslovnaJedinica_Naziv UNIQUE (KlijentId, Naziv)
);

CREATE TABLE dbo.TipUredjaja (
    Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TipUredjaja PRIMARY KEY,
    Naziv         nvarchar(100) NOT NULL CONSTRAINT UQ_TipUredjaja_Naziv UNIQUE,
    PratiBrojac   bit           NOT NULL CONSTRAINT DF_TipUredjaja_PratiBrojac DEFAULT (0)
);

CREATE TABLE dbo.Uredjaj (
    Id                 int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Uredjaj PRIMARY KEY,
    SerijskiBroj       nvarchar(100) NOT NULL CONSTRAINT UQ_Uredjaj_SerijskiBroj UNIQUE,
    InventurniBroj     nvarchar(100) NULL,
    Model              nvarchar(200) NULL,
    TipUredjajaId      int           NULL CONSTRAINT FK_Uredjaj_Tip REFERENCES dbo.TipUredjaja(Id),
    SamoCrnoBijeli     bit           NOT NULL CONSTRAINT DF_Uredjaj_SamoCB DEFAULT (0),
    KlijentId          int           NULL CONSTRAINT FK_Uredjaj_Klijent REFERENCES dbo.Klijent(Id),
    PoslovnaJedinicaId int           NULL CONSTRAINT FK_Uredjaj_PoslovnaJedinica REFERENCES dbo.PoslovnaJedinica(Id),
    GarancijaDo        date          NULL,
    ZadnjiKolor        int           NULL,
    ZadnjiCB           int           NULL,
    BrojacDatum        date          NULL,
    Kreirano           datetime2(0)  NOT NULL CONSTRAINT DF_Uredjaj_Kreirano DEFAULT (SYSUTCDATETIME())
);
CREATE INDEX IX_Uredjaj_InventurniBroj ON dbo.Uredjaj(InventurniBroj);

CREATE TABLE dbo.Materijal (
    Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Materijal PRIMARY KEY,
    Naziv         nvarchar(200) NOT NULL CONSTRAINT UQ_Materijal_Naziv UNIQUE,
    Sifra         nvarchar(50)  NULL
);

CREATE TABLE dbo.Serviser (
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Serviser PRIMARY KEY,
    ImePrezime      nvarchar(150) NOT NULL,
    KorisnickiNalog nvarchar(150) NULL,   -- Windows nalog, npr. TEHNOTIM\marko
    Email           nvarchar(200) NULL,   -- veza s tehničarom u ServiceDesk Plus
    Aktivan         bit           NOT NULL CONSTRAINT DF_Serviser_Aktivan DEFAULT (1)
);

CREATE TABLE dbo.Postavke (
    Id            int           NOT NULL CONSTRAINT PK_Postavke PRIMARY KEY CONSTRAINT CK_Postavke_JedanRed CHECK (Id = 1),
    TarifaKm      decimal(9,2)  NOT NULL CONSTRAINT DF_Postavke_Tarifa DEFAULT (0),
    NazivFirme    nvarchar(200) NULL,
    Adresa        nvarchar(200) NULL,
    Email         nvarchar(200) NULL,
    Web           nvarchar(200) NULL,
    Jib           nvarchar(30)  NULL,
    Mb            nvarchar(30)  NULL,
    ZiroRacun     nvarchar(200) NULL
);

-- Zadnji dodijeljeni broj naloga po godini (0001/2026, 0002/2026 …)
CREATE TABLE dbo.BrojacNaloga (
    Godina        int NOT NULL CONSTRAINT PK_BrojacNaloga PRIMARY KEY,
    Zadnji        int NOT NULL
);

CREATE TABLE dbo.Nalog (
    Id                  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Nalog PRIMARY KEY,
    Godina              int           NOT NULL,
    RedniBroj           int           NOT NULL,
    Broj                AS (RIGHT('000' + CAST(RedniBroj AS varchar(10)), 4) + '/' + CAST(Godina AS varchar(4))) PERSISTED,
    Faza                nvarchar(20)  NOT NULL CONSTRAINT DF_Nalog_Faza DEFAULT ('u_radu'),

    PozivInterni        nvarchar(50)  NULL,
    PozivEksterni       nvarchar(50)  NULL,
    SdpZahtjevId        bigint        NULL,

    DatumIzdavanja      date          NOT NULL,
    ZahtjevVrijeme      datetime2(0)  NULL,
    PocetakRada         datetime2(0)  NULL,
    ZavrsetakRada       datetime2(0)  NULL,
    UtrosenoVrijeme     nvarchar(50)  NULL,
    UtrosenoRucno       bit           NOT NULL CONSTRAINT DF_Nalog_UtrosenoRucno DEFAULT (0),

    -- Veza na bazu + podaci kakvi su bili u trenutku naloga (za PDF i istoriju)
    KlijentId           int           NOT NULL CONSTRAINT FK_Nalog_Klijent REFERENCES dbo.Klijent(Id),
    PoslovnaJedinicaId  int           NULL CONSTRAINT FK_Nalog_PoslovnaJedinica REFERENCES dbo.PoslovnaJedinica(Id),
    KlijentNaziv        nvarchar(200) NOT NULL,
    Lokacija            nvarchar(200) NULL,
    Adresa              nvarchar(300) NULL,
    Kontakt             nvarchar(300) NULL,

    Naplata             char(2)       NULL,   -- 'DA' / 'NE'
    Ugovor              char(2)       NULL,
    Garancija           char(2)       NULL,
    UredjajPredao       nvarchar(200) NULL,
    NosilacServiserId   int           NULL CONSTRAINT FK_Nalog_Serviser REFERENCES dbo.Serviser(Id),
    NosilacIme          nvarchar(150) NULL,

    OpisZahtjeva        nvarchar(max) NULL,
    OpisIntervencije    nvarchar(max) NULL,

    PutDestinacija      nvarchar(100) NULL,
    KmTamo              decimal(7,1)  NULL,
    KmNazad             decimal(7,1)  NULL,
    TarifaKm            decimal(9,2)  NULL,
    PutniIznos          decimal(10,2) NULL,

    Status              nvarchar(30)  NULL,   -- OBAVLJENA, DJELIMIČNO OBAVLJENA, NIJE OBAVLJENA, NIJE POTREBNA
    Intervencija        nvarchar(30)  NULL,   -- NA TERENU, SERVIS/KANCELARIJA

    PotpisServiser      varbinary(max) NULL,  -- PNG
    PotpisKorisnik      varbinary(max) NULL,  -- PNG
    PotpisKasnije       bit           NOT NULL CONSTRAINT DF_Nalog_PotpisKasnije DEFAULT (0),

    Kreirano            datetime2(0)  NOT NULL CONSTRAINT DF_Nalog_Kreirano DEFAULT (SYSUTCDATETIME()),
    Kreirao             nvarchar(150) NULL,
    Izmijenjeno         datetime2(0)  NULL,
    Izmijenio           nvarchar(150) NULL,

    CONSTRAINT UQ_Nalog_Broj UNIQUE (Godina, RedniBroj),
    CONSTRAINT CK_Nalog_Faza CHECK (Faza IN ('novo_iz_tiketa', 'u_radu', 'ceka_potpis', 'zavrsen')),
    CONSTRAINT CK_Nalog_DaNe CHECK ((Naplata IS NULL OR Naplata IN ('DA','NE'))
                                AND (Ugovor IS NULL OR Ugovor IN ('DA','NE'))
                                AND (Garancija IS NULL OR Garancija IN ('DA','NE')))
);
CREATE INDEX IX_Nalog_Klijent ON dbo.Nalog(KlijentId);
CREATE INDEX IX_Nalog_SdpZahtjev ON dbo.Nalog(SdpZahtjevId);

CREATE TABLE dbo.NalogUredjaj (
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_NalogUredjaj PRIMARY KEY,
    NalogId         int           NOT NULL CONSTRAINT FK_NalogUredjaj_Nalog REFERENCES dbo.Nalog(Id) ON DELETE CASCADE,
    Redoslijed      int           NOT NULL,
    UredjajId       int           NULL CONSTRAINT FK_NalogUredjaj_Uredjaj REFERENCES dbo.Uredjaj(Id),
    SerijskiBroj    nvarchar(100) NULL,
    Model           nvarchar(200) NULL,
    InventurniBroj  nvarchar(100) NULL,
    TipUredjajaId   int           NULL CONSTRAINT FK_NalogUredjaj_Tip REFERENCES dbo.TipUredjaja(Id),
    SamoCrnoBijeli  bit           NOT NULL CONSTRAINT DF_NalogUredjaj_SamoCB DEFAULT (0),
    Kolor           int           NULL,
    CB              int           NULL
);

CREATE TABLE dbo.NalogMaterijal (
    Id              int IDENTITY(1,1) NOT NULL CONSTRAINT PK_NalogMaterijal PRIMARY KEY,
    NalogId         int           NOT NULL CONSTRAINT FK_NalogMaterijal_Nalog REFERENCES dbo.Nalog(Id) ON DELETE CASCADE,
    Redoslijed      int           NOT NULL,
    MaterijalId     int           NULL CONSTRAINT FK_NalogMaterijal_Materijal REFERENCES dbo.Materijal(Id),
    Naziv           nvarchar(200) NOT NULL,
    Kolicina        decimal(10,2) NULL,
    BrojOtpremnice  nvarchar(50)  NULL
);

-- Ko je i kad mijenjao nalog
CREATE TABLE dbo.NalogIzmjena (
    Id              bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NalogIzmjena PRIMARY KEY,
    NalogId         int           NOT NULL CONSTRAINT FK_NalogIzmjena_Nalog REFERENCES dbo.Nalog(Id) ON DELETE CASCADE,
    Vrijeme         datetime2(0)  NOT NULL CONSTRAINT DF_NalogIzmjena_Vrijeme DEFAULT (SYSUTCDATETIME()),
    Korisnik        nvarchar(150) NULL,
    Opis            nvarchar(500) NULL
);

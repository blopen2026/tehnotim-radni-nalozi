-- 003 – Korisnici aplikacije i njihove uloge
-- Prijava ide preko domenskog (Windows) naloga; ovdje se samo određuje ko smije ući i s kojom ulogom.

CREATE TABLE dbo.Korisnik (
    Id               int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Korisnik PRIMARY KEY,
    KorisnickiNalog  nvarchar(150) NOT NULL CONSTRAINT UQ_Korisnik_Nalog UNIQUE,   -- npr. TEHNOTIM\marko
    ImePrezime       nvarchar(150) NULL,
    Email            nvarchar(200) NULL,
    Uloga            nvarchar(20)  NOT NULL,
    Aktivan          bit           NOT NULL CONSTRAINT DF_Korisnik_Aktivan DEFAULT (1),
    ServiserId       int           NULL CONSTRAINT FK_Korisnik_Serviser REFERENCES dbo.Serviser(Id),
    Kreirano         datetime2(0)  NOT NULL CONSTRAINT DF_Korisnik_Kreirano DEFAULT (SYSUTCDATETIME()),
    ZadnjaPrijava    datetime2(0)  NULL,
    CONSTRAINT CK_Korisnik_Uloga CHECK (Uloga IN ('serviser', 'kancelarija', 'administrator'))
);

-- 006 – Veza sa ServiceDesk Plus-om (adresa, API ključ) i sektori servisera

ALTER TABLE dbo.Postavke ADD
    SdpUrl                nvarchar(300) NULL,     -- npr. https://TEST-NALOZI
    SdpApiKljuc           nvarchar(300) NULL,     -- API ključ tehničara „Radni nalozi (API)”; nikad se ne šalje pregledniku
    SdpPreskociCertifikat bit NOT NULL CONSTRAINT DF_Postavke_SdpCert DEFAULT (0);  -- samo za test (samopotpisan certifikat)

-- Sektor: grupa servisera koja u SDP-u radi preko jednog zajedničkog naloga (tehničara) i/ili grupe
CREATE TABLE dbo.Sektor (
    Id                int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sektor PRIMARY KEY,
    Naziv             nvarchar(100) NOT NULL CONSTRAINT UQ_Sektor_Naziv UNIQUE,
    SdpTehnicarId     nvarchar(30)  NULL,
    SdpTehnicarNaziv  nvarchar(150) NULL,
    SdpGrupaId        nvarchar(30)  NULL,
    SdpGrupaNaziv     nvarchar(150) NULL,
    Aktivan           bit NOT NULL CONSTRAINT DF_Sektor_Aktivan DEFAULT (1)
);

-- Jedan korisnik može biti u više sektora
CREATE TABLE dbo.KorisnikSektor (
    KorisnikId int NOT NULL CONSTRAINT FK_KorisnikSektor_Korisnik REFERENCES dbo.Korisnik(Id) ON DELETE CASCADE,
    SektorId   int NOT NULL CONSTRAINT FK_KorisnikSektor_Sektor   REFERENCES dbo.Sektor(Id)   ON DELETE CASCADE,
    CONSTRAINT PK_KorisnikSektor PRIMARY KEY (KorisnikId, SektorId)
);

ALTER TABLE dbo.Nalog ADD SektorId int NULL CONSTRAINT FK_Nalog_Sektor REFERENCES dbo.Sektor(Id);
GO

INSERT INTO dbo.Sektor (Naziv) VALUES (N'Tehnička'), (N'Printing'), (N'Sistem'), (N'Mreže');

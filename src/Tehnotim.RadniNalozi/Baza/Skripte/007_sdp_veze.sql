-- 007 – Zapamćene veze sa ServiceDesk Plus-om: SDP site → klijent, SDP requester → poslovna jedinica
-- Pune se same kad se nalog iz tiketa sačuva (korisnik potvrdi klijenta i jedinicu u formi).

CREATE TABLE dbo.SdpSite (
    SdpSiteId    nvarchar(30)  NOT NULL CONSTRAINT PK_SdpSite PRIMARY KEY,
    Naziv        nvarchar(200) NULL,
    KlijentId    int           NOT NULL CONSTRAINT FK_SdpSite_Klijent REFERENCES dbo.Klijent(Id) ON DELETE CASCADE,
    Izmijenjeno  datetime2(0)  NOT NULL CONSTRAINT DF_SdpSite_Izm DEFAULT (SYSUTCDATETIME()),
    Izmijenio    nvarchar(150) NULL
);

CREATE TABLE dbo.SdpRequester (
    SdpRequesterId     nvarchar(30)  NOT NULL CONSTRAINT PK_SdpRequester PRIMARY KEY,
    Naziv              nvarchar(200) NULL,
    Email              nvarchar(200) NULL,
    KlijentId          int           NOT NULL CONSTRAINT FK_SdpRequester_Klijent REFERENCES dbo.Klijent(Id) ON DELETE CASCADE,
    PoslovnaJedinicaId int           NOT NULL CONSTRAINT FK_SdpRequester_Jedinica REFERENCES dbo.PoslovnaJedinica(Id),
    Izmijenjeno        datetime2(0)  NOT NULL CONSTRAINT DF_SdpRequester_Izm DEFAULT (SYSUTCDATETIME()),
    Izmijenio          nvarchar(150) NULL
);

CREATE INDEX IX_Nalog_SdpZahtjevId ON dbo.Nalog (SdpZahtjevId) WHERE SdpZahtjevId IS NOT NULL;

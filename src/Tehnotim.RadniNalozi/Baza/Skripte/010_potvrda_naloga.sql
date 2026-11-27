-- 010 – Elektronska potvrda naloga: ovlaštena lica klijenta, potvrda preko linka iz e-maila, slanje e-maila (SMTP)

-- Osobe klijenta koje smiju potvrditi nalog (npr. šef poslovne jedinice)
CREATE TABLE dbo.OvlastenoLice (
    Id           int IDENTITY(1,1) NOT NULL CONSTRAINT PK_OvlastenoLice PRIMARY KEY,
    KlijentId    int           NOT NULL CONSTRAINT FK_OvlastenoLice_Klijent REFERENCES dbo.Klijent(Id) ON DELETE CASCADE,
    ImePrezime   nvarchar(150) NOT NULL,
    Email        nvarchar(200) NOT NULL,
    Funkcija     nvarchar(150) NULL,
    SveJedinice  bit           NOT NULL CONSTRAINT DF_OvlastenoLice_Sve DEFAULT (1),   -- važi za sve jedinice klijenta
    Aktivan      bit           NOT NULL CONSTRAINT DF_OvlastenoLice_Aktivan DEFAULT (1),
    Kreirano     datetime2(0)  NOT NULL CONSTRAINT DF_OvlastenoLice_Kreirano DEFAULT (SYSUTCDATETIME())
);
CREATE INDEX IX_OvlastenoLice_Klijent ON dbo.OvlastenoLice (KlijentId);

-- Za koje jedinice važi lice (kad SveJedinice = 0)
CREATE TABLE dbo.OvlastenoLiceJedinica (
    OvlastenoLiceId    int NOT NULL CONSTRAINT FK_OLJ_Lice REFERENCES dbo.OvlastenoLice(Id) ON DELETE CASCADE,
    PoslovnaJedinicaId int NOT NULL CONSTRAINT FK_OLJ_Jedinica REFERENCES dbo.PoslovnaJedinica(Id) ON DELETE CASCADE,
    CONSTRAINT PK_OvlastenoLiceJedinica PRIMARY KEY (OvlastenoLiceId, PoslovnaJedinicaId)
);

-- Potvrda na nalogu: kome je poslano (ime i e-mail se pamte kakvi su bili), kad, i kad je potvrđeno.
-- Link nosi slučajan ključ; u bazi je samo njegov SHA-256 otisak.
ALTER TABLE dbo.Nalog ADD
    PotvrdaLiceId           int            NULL CONSTRAINT FK_Nalog_PotvrdaLice REFERENCES dbo.OvlastenoLice(Id),
    PotvrdaIme              nvarchar(150)  NULL,
    PotvrdaEmail            nvarchar(200)  NULL,
    PotvrdaKljucHash        varbinary(32)  NULL,
    PotvrdaKod              nvarchar(12)   NULL,     -- kratki kod potvrde, štampa se na nalogu
    PotvrdaPoslano          datetime2(0)   NULL,     -- lokalno vrijeme servera
    PotvrdaPoslao           nvarchar(150)  NULL,
    PotvrdaPoslaoKorisnikId int            NULL,
    Potvrdjeno              datetime2(0)   NULL,
    PotvrdaAdresa           nvarchar(60)   NULL,     -- IP adresa s koje je potvrđeno
    PotvrdaPrimjedba        nvarchar(2000) NULL,
    PotvrdaPrimjedbaVrijeme datetime2(0)   NULL;
GO

CREATE UNIQUE INDEX UX_Nalog_PotvrdaKljuc ON dbo.Nalog (PotvrdaKljucHash) WHERE PotvrdaKljucHash IS NOT NULL;

-- Slanje e-maila i javna adresa aplikacije (za link u e-mailu)
ALTER TABLE dbo.Postavke ADD
    SmtpServer        nvarchar(200) NULL,
    SmtpPort          int           NULL,
    SmtpSigurnost     nvarchar(10)  NULL,      -- auto, ssl, starttls, bez
    SmtpKorisnik      nvarchar(200) NULL,
    SmtpLozinka       nvarchar(300) NULL,      -- nikad se ne šalje pregledniku
    SmtpPosiljalac    nvarchar(200) NULL,      -- adresa s koje ide e-mail
    SmtpPosiljalacIme nvarchar(150) NULL,
    JavnaAdresa       nvarchar(300) NULL;      -- npr. https://nalozi.tehnotim.ba (adresa koju klijent može otvoriti)

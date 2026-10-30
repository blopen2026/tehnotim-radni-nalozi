-- 005 – Magacin rezervnih dijelova (samo količinski, jedan magacin)
-- Šifarnik Materijal postaje lista artikala magacina. Artikal se vodi po količini ili po serijskom broju (svaki komad posebno).
-- Svako kretanje se bilježi u MagacinPromjena (kartica artikla).

ALTER TABLE dbo.Materijal ADD
    PN           nvarchar(100) NULL,                                                     -- product number
    VodiSeSN     bit           NOT NULL CONSTRAINT DF_Materijal_VodiSeSN DEFAULT (0),     -- svaki komad ima svoj SN
    Stanje       decimal(10,2) NOT NULL CONSTRAINT DF_Materijal_Stanje DEFAULT (0),       -- može biti i u minusu
    MinKolicina  decimal(10,2) NULL,                                                     -- upozorenje kad stanje padne ispod
    Aktivan      bit           NOT NULL CONSTRAINT DF_Materijal_Aktivan DEFAULT (1);
GO

CREATE INDEX IX_Materijal_PN ON dbo.Materijal(PN);
GO

-- Komadi artikala koji se vode po serijskom broju
CREATE TABLE dbo.MagacinKomad (
    Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MagacinKomad PRIMARY KEY,
    MaterijalId   int           NOT NULL CONSTRAINT FK_MagacinKomad_Materijal REFERENCES dbo.Materijal(Id),
    SerijskiBroj  nvarchar(100) NOT NULL,
    Status        nvarchar(20)  NOT NULL CONSTRAINT DF_MagacinKomad_Status DEFAULT ('na_stanju'),
    NalogId       int           NULL CONSTRAINT FK_MagacinKomad_Nalog REFERENCES dbo.Nalog(Id),   -- u kojem nalogu je utrošen
    Primljeno     datetime2(0)  NOT NULL CONSTRAINT DF_MagacinKomad_Primljeno DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_MagacinKomad UNIQUE (MaterijalId, SerijskiBroj),
    CONSTRAINT CK_MagacinKomad_Status CHECK (Status IN ('na_stanju', 'utrosen', 'otpisan'))
);
GO

-- Kartica artikla: svako kretanje (prijem, utrošak u nalogu, povrat iz naloga, korekcija)
CREATE TABLE dbo.MagacinPromjena (
    Id            bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MagacinPromjena PRIMARY KEY,
    MaterijalId   int           NOT NULL CONSTRAINT FK_MagacinPromjena_Materijal REFERENCES dbo.Materijal(Id),
    Vrijeme       datetime2(0)  NOT NULL CONSTRAINT DF_MagacinPromjena_Vrijeme DEFAULT (SYSUTCDATETIME()),
    Korisnik      nvarchar(150) NULL,
    Vrsta         nvarchar(20)  NOT NULL,
    Kolicina      decimal(10,2) NOT NULL,      -- + ulaz, - izlaz
    StanjePoslije decimal(10,2) NOT NULL,
    NalogId       int           NULL,          -- bez FK: zapis ostaje i kad se nalog obriše
    NalogBroj     nvarchar(20)  NULL,
    SerijskiBroj  nvarchar(100) NULL,
    Dokument      nvarchar(100) NULL,          -- otpremnica / dobavljač kod prijema
    Napomena      nvarchar(300) NULL,
    CONSTRAINT CK_MagacinPromjena_Vrsta CHECK (Vrsta IN ('prijem', 'utrosak', 'povrat', 'korekcija', 'otpis'))
);
CREATE INDEX IX_MagacinPromjena_Materijal ON dbo.MagacinPromjena(MaterijalId, Vrijeme);
GO

-- Stavke materijala u nalogu: PN, SN, komad iz magacina; stavke van magacina se razrješavaju posebno
ALTER TABLE dbo.NalogMaterijal ADD
    PN                   nvarchar(100) NULL,
    SerijskiBroj         nvarchar(100) NULL,
    KomadId              int           NULL CONSTRAINT FK_NalogMaterijal_Komad REFERENCES dbo.MagacinKomad(Id),
    VanMagacinaRijeseno  bit           NOT NULL CONSTRAINT DF_NalogMaterijal_Rijeseno DEFAULT (0),
    VanMagacinaNapomena  nvarchar(300) NULL;

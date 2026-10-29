-- 004 – Nosilac naloga je korisnik aplikacije; posebna tabela Serviser se ukida.
-- Svaki aktivni korisnik može biti nosilac naloga, osim ako mu se isključi NosilacNaloga.

ALTER TABLE dbo.Korisnik ADD NosilacNaloga bit NOT NULL CONSTRAINT DF_Korisnik_NosilacNaloga DEFAULT (1);
GO

ALTER TABLE dbo.Nalog DROP CONSTRAINT FK_Nalog_Serviser;
GO

EXEC sp_rename 'dbo.Nalog.NosilacServiserId', 'NosilacKorisnikId', 'COLUMN';
GO

ALTER TABLE dbo.Nalog ADD CONSTRAINT FK_Nalog_NosilacKorisnik
    FOREIGN KEY (NosilacKorisnikId) REFERENCES dbo.Korisnik(Id);
GO

ALTER TABLE dbo.Korisnik DROP CONSTRAINT FK_Korisnik_Serviser;
GO

ALTER TABLE dbo.Korisnik DROP COLUMN ServiserId;
GO

DROP TABLE dbo.Serviser;

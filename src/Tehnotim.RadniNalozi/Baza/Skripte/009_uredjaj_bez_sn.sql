-- 009 – Uređaj može biti bez serijskog broja (uvoz po inventurnom broju, npr. popis osnovnih sredstava klijenta)
--        Uređaj se prepoznaje po serijskom broju ili po inventurnom broju kod istog klijenta. Novi tip: UPS.

ALTER TABLE dbo.Uredjaj DROP CONSTRAINT UQ_Uredjaj_SerijskiBroj;
ALTER TABLE dbo.Uredjaj ALTER COLUMN SerijskiBroj nvarchar(100) NULL;
GO

CREATE UNIQUE INDEX UQ_Uredjaj_SerijskiBroj ON dbo.Uredjaj (SerijskiBroj) WHERE SerijskiBroj IS NOT NULL;
CREATE INDEX IX_Uredjaj_KlijentInventurni ON dbo.Uredjaj (KlijentId, InventurniBroj) WHERE InventurniBroj IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM dbo.TipUredjaja WHERE Naziv = N'UPS')
    INSERT INTO dbo.TipUredjaja (Naziv, PratiBrojac) VALUES (N'UPS', 0);

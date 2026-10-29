-- 002 – Početni podaci: tipovi uređaja i podaci firme za PDF

INSERT INTO dbo.TipUredjaja (Naziv, PratiBrojac) VALUES
    (N'Štampač', 1),
    (N'Multifunkcijski uređaj', 1),
    (N'Fotokopir aparat', 1),
    (N'Računar', 0),
    (N'Laptop', 0),
    (N'Skener', 0),
    (N'Monitor', 0);

INSERT INTO dbo.Postavke (Id, TarifaKm, NazivFirme, Adresa, Email, Web, Jib, Mb, ZiroRacun) VALUES
    (1, 0,
     N'Tehnotim d.o.o.',
     N'Miše Stupara 4, 78000 Banja Luka',
     N'info@tehnotim.ba',
     N'www.tehnotim.ba',
     N'4405312380000',
     N'11667694',
     N'572-106-00020639-67 MF Banka a.d. Banja Luka');

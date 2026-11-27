-- 011 – Zatvaranje naloga dugmetom (umjesto potvrde e-mailom, dok nema pristupa izvana) i zatvaranje tiketa u SDP-u

ALTER TABLE dbo.Nalog ADD
    Zatvoreno        datetime2(0)  NULL,     -- lokalno vrijeme servera
    Zatvorio         nvarchar(150) NULL,
    SdpTiketZatvoren datetime2(0)  NULL;

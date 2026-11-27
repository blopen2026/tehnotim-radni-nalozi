-- 012 – Ime korisnika kako je u SDP listi „Inženjer / Tehničar” (udf_pick_602), ako se razlikuje od imena u aplikaciji

ALTER TABLE dbo.Korisnik ADD SdpIme nvarchar(150) NULL;

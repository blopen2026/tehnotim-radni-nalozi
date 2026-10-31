-- 008 – Povratak u tiket: kad je u tiket upisana napomena o otvaranju i o završetku naloga (s PDF-om)
ALTER TABLE dbo.Nalog ADD
    SdpNapomenaOtvoren  datetime2(0) NULL,
    SdpNapomenaZavrsen  datetime2(0) NULL;

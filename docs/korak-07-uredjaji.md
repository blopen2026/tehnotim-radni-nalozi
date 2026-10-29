# Korak 7 – uređaji

Meni **Uređaji** (kancelarija i administrator):

- lista s pretragom (serijski i inventurni broj, model, klijent, jedinica) i filterom po klijentu; prvih 500 rezultata;
- uređaj: serijski broj (jedinstven), inventurni broj, model, tip, klijent, poslovna jedinica, garancija do;
- za tipove s brojačem: „samo crno-bijeli” i početno stanje brojača (kolor, C/B, datum očitanja);
- novi uređaj istog modela kao postojeći dobija prijedlog tipa;
- poslovna jedinica određuje klijenta (ne može se izabrati jedinica drugog klijenta);
- uređaj koji se pojavljuje u nalozima ne može se obrisati;
- sa stranice klijenta: link „Uređaji ovog klijenta”.

Za formu naloga (korak 8) već postoje: traženje po serijskom ili inventurnom broju (`/api/uredjaji/po-broju`)
i lista poznatih modela s tipom (`/api/uredjaji/modeli`).

## Provjera

```
git pull
pokreni.cmd   (ili: cd src\Tehnotim.RadniNalozi  →  dotnet run)
```

1. Meni **Uređaji** → **+ Novi uređaj**: serijski, inventurni, model, tip Multifunkcijski, klijent Republička Geodetska Uprava,
   jedinica npr. PJ Prijedor; pojave se polja brojača → upisati stanje; garancija do nekog datuma → Sačuvaj.
2. Drugi uređaj **istog modela** → tip se sam predloži.
3. Uređaj tipa Računar → polja brojača se ne pojavljuju.
4. Isti serijski broj drugi put → aplikacija odbija.
5. Pretraga po inventurnom broju i po nazivu jedinice; filter po klijentu.
6. Stranica klijenta → „Uređaji ovog klijenta”.

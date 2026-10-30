# Korak 8b – lista naloga, istorija, brisanje

- Meni **Nalozi** (sve uloge): pretraga po broju naloga, internom i eksternom broju, klijentu, jedinici, nosiocu,
  opisu i serijskom/inventurnom broju i modelu uređaja; filteri po klijentu, nosiocu, datumu od–do i fazi (s brojem naloga po fazi);
  najnoviji prvi, po 50 na strani. Filteri ostaju u adresi stranice, pa povratak iz naloga vraća istu listu.
- Početna: posljednjih 15 naloga i link „Svi nalozi”.
- U nalogu: **Istorija izmjena** (ko je i kad kreirao/mijenjao).
- U nalogu, kod uređaja iz baze: link **istorija naloga** (otvara listu naloga za taj serijski broj) – i za servisere.
- Na stranici Uređaji, u izmjeni uređaja: tabela **Istorija naloga** s brojačem iz svakog naloga.
- **Brisanje naloga**: samo kancelarija i administrator, uz potvrdu. Broj obrisanog naloga se ne dodjeljuje ponovo.
  Brojač uređaja se vraća na stanje iz zadnjeg preostalog naloga.

## Provjera

```
git pull
pokreni.cmd
```

1. Meni **Nalozi** → vide se svi probni nalozi; brojevi po fazama.
2. Pretraga po serijskom broju uređaja, po internom broju, po dijelu opisa.
3. Filter po klijentu, nosiocu, datumu; faza „Čeka potpis”.
4. Otvoriti nalog → „← Nalozi” vraća na istu filtriranu listu.
5. U nalogu: dno stranice – Istorija izmjena; kod uređaja – link „istorija naloga”.
6. Uređaji → klik na uređaj → Istorija naloga.
7. Obrisati probni nalog (npr. onaj s `PK Test`) → nestaje iz liste; poslije se može obrisati i jedinica `PK Test` u Klijentima.

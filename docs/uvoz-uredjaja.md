# Uvoz uređaja iz Excela

**Uređaji → Uvoz iz Excela** (kancelarija i administrator).

1. Izaberite **klijenta** i **fajl** (.xlsx / .csv). Kolone se prepoznaju po zaglavlju (inventurni broj, serijski broj,
   naziv/model, organizaciona jedinica…), mogu se prepraviti.
2. **Tip uređaja** se prepoznaje po prvoj riječi naziva: računar, laptop, monitor, štampač, multifunkcijski (i „štampač/kopir/…”),
   kopir, skener, UPS. Sve ostalo (softver, licence, telefoni, telefaks, ruteri, switchevi, serveri, video nadzor, čitači…) se **preskače**
   – spisak preskočenih naziva je u pregledu. Model = naziv bez prve riječi (npr. „Racunar LENOVO M720T” → tip Računar, model „LENOVO M720T”).
3. **Poslovne jedinice:** za svaki naziv iz tabele bira se jedinica klijenta. Tačno prepoznate su izabrane, djelimično (žuto) treba provjeriti,
   neprepoznate (crveno) su „bez poslovne jedinice” – može se izabrati jedinica ili „ne uvozi”.
4. Pregled: broj za uvoz po tipu, preskočeni, bez jedinice → **Uvezi**.

## Pravila

- Uređaj se prepoznaje po **serijskom broju**, a ako ga nema – po **inventurnom broju kod istog klijenta**. Postojeći se ažurira
  (model, tip, jedinica), ništa se ne duplira. Ponovni uvoz novije tabele samo ažurira.
- Serijski broj više nije obavezan (skripta `009_uredjaj_bez_sn.sql`); uređaj bez serijskog broja mora imati klijenta.
- U nalogu: upis inventurnog broja pronalazi uređaj tog klijenta (popuni model, tip, jedinicu); pri čuvanju naloga uređaj se veže po
  inventurnom broju, a ako serviser upiše i serijski broj, on se dopiše uređaju.
- Novi tip uređaja: **UPS**.

## RGU – tabela „IKT raspored” (02.07.2026.)

6.751 red → oko 4.950 uređaja za uvoz (monitori 1.857, računari 1.763, štampači 573, skeneri 240, UPS 215, laptopi 160,
multifunkcijski 97, kopir 47); oko 1.800 preskočeno. Organizacione jedinice koje nisu među uvezenim jedinicama RGU-a
(npr. Foča, Teslić, Han Pijesak, Šipovo, Čelinac, BACKUP DATA CENTAR, BIJELJINA AOP / UPRAVA) treba izabrati ručno ili prvo dodati jedinice.
„Uprava” (objekti Uprava sjedište i Uprava Borik) ide na „Sjedište uprave” – da li je Borik posebna jedinica, još nije odlučeno.

// Zaglavlje svih stranica: ko je prijavljen, meni prema ulozi.
// Stranica zove: const ja = await ucitajZaglavlje('pocetna');
const esc = t => String(t ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const NAZIVI_ULOGA = { serviser: 'Serviser', kancelarija: 'Kancelarija', administrator: 'Administrator' };

async function ucitajZaglavlje(aktivna) {
  const ja = await fetch('/api/ja').then(r => r.json());
  const meni = [['pocetna', '/', 'Početna'], ['nalozi', '/nalozi.html', 'Nalozi'], ['novi', '/nalog.html', '+ Novi nalog'], ['tiketi', '/tiketi.html', 'Iz tiketa'], ['izvjestaji', '/izvjestaji.html', 'Izvještaji']];
  if (ja.uloga === 'kancelarija' || ja.uloga === 'administrator') {
    meni.push(['klijenti', '/klijenti.html', 'Klijenti']);
    meni.push(['uredjaji', '/uredjaji.html', 'Uređaji']);
    meni.push(['sifarnici', '/sifarnici.html', 'Šifarnici']);
  }
  if (ja.uloga === 'administrator') {
    meni.push(['magacin', '/magacin.html', 'Magacin']);
    meni.push(['korisnici', '/korisnici.html', 'Korisnici']);
  }

  const h = document.createElement('header');
  h.innerHTML = `<a class="brand" href="/" title="Početna"><img class="logo" src="/img/logo-svijetli.png" alt="Tehnotim"></a>
    <nav class="${meni.length >= 6 ? 'rasiri' : ''}">${ja.registrovan ? meni.map(([k, url, t]) => `<a href="${url}" class="${k === aktivna ? 'akt' : ''}">${t}</a>`).join('') : ''}</nav>
    <span class="ja"><b>${esc(ja.ime || ja.nalog)}</b>${ja.registrovan ? NAZIVI_ULOGA[ja.uloga] : 'nema pristup'}</span>`;
  document.body.prepend(h);
  // Logo uz lijevu ivicu ekrana, korisnik uz desnu, meni u sredini – na svim stranicama iste širine (900 px),
  // da meni izgleda jednako bez obzira na širinu sadržaja stranice.
  // Na uskom ekranu (nema mjesta lijevo i desno od menija) sve ide u jedan red jedno do drugog.
  const sirina = 900;
  h.style.setProperty('--sirina', sirina + 'px');
  const prilagodi = () => h.classList.toggle('usko', window.innerWidth < sirina + 2 * 200);
  prilagodi(); window.addEventListener('resize', prilagodi);

  if (!ja.registrovan) {
    document.querySelector('main').innerHTML = `<div class="card">
      <h1>Nemate pristup aplikaciji</h1>
      <p>Prijavljeni ste kao <b class="mono">${esc(ja.nalog)}</b>, ali taj nalog nije upisan među korisnike ili nije aktivan.</p>
      <p class="muted">Pošaljite ovaj naziv naloga administratoru da vas doda.</p></div>`;
  }
  return ja;
}

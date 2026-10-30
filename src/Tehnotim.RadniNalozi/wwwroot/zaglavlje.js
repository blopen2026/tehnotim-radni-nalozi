// Zaglavlje svih stranica: ko je prijavljen, meni prema ulozi.
// Stranica zove: const ja = await ucitajZaglavlje('pocetna');
const esc = t => String(t ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const NAZIVI_ULOGA = { serviser: 'Serviser', kancelarija: 'Kancelarija', administrator: 'Administrator' };

async function ucitajZaglavlje(aktivna) {
  const ja = await fetch('/api/ja').then(r => r.json());
  const meni = [['pocetna', '/', 'Početna'], ['nalozi', '/nalozi.html', 'Nalozi'], ['novi', '/nalog.html', '+ Novi nalog']];
  if (ja.uloga === 'kancelarija' || ja.uloga === 'administrator') {
    meni.push(['klijenti', '/klijenti.html', 'Klijenti']);
    meni.push(['uredjaji', '/uredjaji.html', 'Uređaji']);
    meni.push(['sifarnici', '/sifarnici.html', 'Šifarnici']);
  }
  if (ja.uloga === 'administrator') meni.push(['korisnici', '/korisnici.html', 'Korisnici']);

  const h = document.createElement('header');
  h.innerHTML = `<a class="brand" href="/"><span class="logochip"><img src="/img/logo.png" alt="Tehnotim"></span><small>Radni nalozi</small></a>
    <nav>${ja.registrovan ? meni.map(([k, url, t]) => `<a href="${url}" class="${k === aktivna ? 'akt' : ''}">${t}</a>`).join('') : ''}</nav>
    <span class="ja"><b>${esc(ja.ime || ja.nalog)}</b>${ja.registrovan ? NAZIVI_ULOGA[ja.uloga] : 'nema pristup'}</span>`;
  document.body.prepend(h);

  if (!ja.registrovan) {
    document.querySelector('main').innerHTML = `<div class="card">
      <h1>Nemate pristup aplikaciji</h1>
      <p>Prijavljeni ste kao <b class="mono">${esc(ja.nalog)}</b>, ali taj nalog nije upisan među korisnike ili nije aktivan.</p>
      <p class="muted">Pošaljite ovaj naziv naloga administratoru da vas doda.</p></div>`;
  }
  return ja;
}

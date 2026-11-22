// PRAFF LEATHER — Barkodla Stok İşlemleri
// NETUM gibi okuyucular klavye gibi çalışır: kodu yazar ve Enter'a basar. Enter = işlem tetikleyici.
(function () {
  'use strict';
  const root = document.querySelector('[data-scan]');
  if (!root) return;
  const $ = (s, r) => (r || root).querySelector(s);
  const $$ = (s, r) => Array.from((r || root).querySelectorAll(s));
  const csrf = () => (document.querySelector('meta[name="csrf"]') || {}).content || '';
  const num = v => Number(v || 0).toLocaleString('tr-TR');
  const esc = s => String(s == null ? '' : s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const camLib = (document.currentScript && document.currentScript.dataset.cameraLib) || '/lib/html5-qrcode/html5-qrcode.min.js';

  const input = $('[data-scan-input]');
  const qtyInput = $('[data-scan-qty]');
  const result = $('[data-result]');
  const variantsBox = $('[data-variants]');
  const undoBtn = $('[data-undo]');
  const hint = $('[data-mode-hint]');
  const countPanel = $('[data-count-panel]');
  const countBody = $('[data-count-body]');
  const logBody = $('[data-log-body]');

  let mode = 'giris';
  let busy = false;
  const queue = [];
  const undoStack = [];
  const counts = new Map(); // "pid:vid" -> {pid, vid, name, variantName, system, counted}
  let last = { code: '', at: 0 };
  let logCount = 0;

  // ---------------- Ses ----------------
  let audio;
  function beep(ok) {
    try {
      audio = audio || new (window.AudioContext || window.webkitAudioContext)();
      const o = audio.createOscillator(), g = audio.createGain();
      o.type = ok ? 'sine' : 'square';
      o.frequency.value = ok ? 1250 : 220;
      g.gain.value = 0.08;
      o.connect(g); g.connect(audio.destination);
      o.start();
      o.stop(audio.currentTime + (ok ? 0.09 : 0.35));
    } catch { /* ses desteklenmiyor */ }
  }

  const focusInput = () => setTimeout(() => { input.value = ''; input.focus(); }, 0);

  async function post(url, data, json) {
    const opt = { method: 'POST', credentials: 'same-origin', headers: { 'RequestVerificationToken': csrf(), 'Accept': 'application/json' } };
    if (json) { opt.headers['Content-Type'] = 'application/json'; opt.body = JSON.stringify(data); }
    else { const fd = new FormData(); Object.keys(data).forEach(k => { if (data[k] != null) fd.append(k, data[k]); }); opt.body = fd; }
    const res = await fetch(url, opt);
    if (res.status === 429) throw new Error('Çok hızlı okutma. Birkaç saniye bekleyin.');
    if (!res.ok) throw new Error('Sunucu hatası (' + res.status + '). Oturumunuz kapanmış olabilir; sayfayı yenileyin.');
    return res.json();
  }

  // ---------------- Sonuç kartı ----------------
  function show(kind, html) {
    result.hidden = false;
    result.className = 'result ' + kind;
    result.innerHTML = html;
  }
  function productCard(p, line) {
    const img = p.image ? '<img src="' + esc(p.image) + '" alt="" />' : '<span class="noimg">PL</span>';
    return img + '<div><b>' + esc(p.name) + (p.variantName ? ' — ' + esc(p.variantName) : '') + '</b><small>' + esc(p.sku) + '</small>' + (line || '') + '</div>';
  }

  // ---------------- Mod ----------------
  const hints = {
    giris: 'Her okutma stoğa 1 adet ekler (adet kutusundan değiştirebilirsiniz).',
    cikis: 'Her okutma stoktan 1 adet düşer (adet kutusundan değiştirebilirsiniz).',
    sayim: 'Okutulan ürünler sayılır; bitince “Sayımı Kaydet” ile stok sayılan miktara eşitlenir.'
  };
  $$('[data-mode]').forEach(b => b.addEventListener('click', () => {
    mode = b.dataset.mode;
    $$('[data-mode]').forEach(x => { const on = x === b; x.classList.toggle('on', on); x.setAttribute('aria-selected', on); });
    root.dataset.current = mode;
    hint.textContent = hints[mode];
    countPanel.hidden = mode !== 'sayim';
    variantsBox.hidden = true;
    result.hidden = true;
    focusInput();
  }));

  // ---------------- Okutma kuyruğu ----------------
  function enqueue(code, variantId) {
    code = (code || '').trim();
    if (!code) return;
    const now = Date.now();
    // Çift okutma koruması: aynı kod 400 ms içinde tekrar gelirse yok sayılır.
    if (!variantId && code === last.code && now - last.at < 400) return;
    last = { code, at: now };
    queue.push({ code, variantId, qty: Math.max(1, parseInt(qtyInput.value, 10) || 1) });
    pump();
  }
  async function pump() {
    if (busy) return;
    const job = queue.shift();
    if (!job) return;
    busy = true;
    root.classList.add('busy');
    try { await (mode === 'sayim' ? count(job) : move(job)); }
    catch (err) { beep(false); show('err', '<div><b>İşlem yapılamadı</b><small>' + esc(err.message) + '</small></div>'); }
    finally { busy = false; root.classList.remove('busy'); pump(); }
  }

  function askVariant(job, product) {
    variantsBox.hidden = false;
    variantsBox.innerHTML = '<p><b>' + esc(product.name) + '</b> renkli bir ürün. Hangi renk?</p><div class="vbtns">' +
      product.variants.map(v => '<button type="button" class="vbtn" data-v="' + v.id + '"><i style="background:' + esc(v.hex) + '"></i>' + esc(v.name) + '<small>Stok: ' + num(v.stock) + '</small></button>').join('') + '</div>';
    $$('[data-v]', variantsBox).forEach(b => b.addEventListener('click', () => {
      variantsBox.hidden = true;
      queue.unshift({ code: job.code, variantId: parseInt(b.dataset.v, 10), qty: job.qty });
      pump();
      focusInput();
    }));
    beep(false);
  }

  function notFound(code) {
    beep(false);
    show('err', '<div><b>Bu barkod kayıtlı değil — yeni ürüne ata?</b><small>Okutulan kod: <code>' + esc(code) + '</code></small>' +
      '<span class="acts"><a class="sbtn" href="/admin/urunler/yeni?barkod=' + encodeURIComponent(code) + '">Yeni ürün oluştur</a>' +
      '<a class="sbtn" href="/admin/barkod?filtre=eksik">Mevcut ürüne ata</a></span></div>');
  }

  // Giriş / çıkış
  async function move(job) {
    const data = await post('/admin/barkod-stok/okut', { code: job.code, mode, quantity: job.qty, variantId: job.variantId });
    if (data.notFound) return notFound(job.code);
    if (data.needVariant) return askVariant(job, data.product);
    if (!data.ok) { beep(false); show('err', '<div><b>' + esc(data.message) + '</b></div>'); return; }
    beep(true);
    variantsBox.hidden = true;
    const sign = mode === 'giris' ? '+' : '−';
    show('ok', productCard(data.product, '<span class="chg">' + num(data.before) + ' → <b>' + num(data.after) + '</b> <em>(' + sign + num(job.qty) + ')</em></span>'));
    if (data.movementId) { undoStack.push({ id: data.movementId, name: data.product.name }); undoBtn.disabled = false; }
    addLog(mode === 'giris' ? 'Giriş' : 'Çıkış', data.product, sign + num(job.qty), num(data.before) + ' → ' + num(data.after));
  }

  // Sayım
  async function count(job) {
    const data = await post('/admin/barkod-stok/bul', { code: job.code });
    if (data.notFound) return notFound(job.code);
    const p = data.product;
    let vid = p.variantId, vname = p.variantName, system = p.stock;
    if (!vid && p.variants) {
      if (!job.variantId) return askVariant(job, p);
      const v = p.variants.find(x => x.id === job.variantId);
      vid = v.id; vname = v.name; system = v.stock;
    }
    const key = p.id + ':' + (vid || '');
    const row = counts.get(key) || { pid: p.id, vid: vid || null, name: p.name, variantName: vname, sku: p.sku, system, counted: 0 };
    row.counted += job.qty;
    counts.set(key, row);
    beep(true);
    variantsBox.hidden = true;
    show('ok', productCard({ name: p.name, variantName: vname, sku: p.sku, image: p.image }, '<span class="chg">Sayılan: <b>' + num(row.counted) + '</b> · Sistem: ' + num(system) + '</span>'));
    renderCounts();
    addLog('Sayım', { name: p.name, variantName: vname }, '+' + num(job.qty), 'sayılan ' + num(row.counted));
  }

  function renderCounts() {
    const rows = Array.from(counts.entries());
    $('[data-count-total]').textContent = rows.length + ' kalem · ' + num(rows.reduce((s, [, r]) => s + r.counted, 0)) + ' adet';
    countBody.innerHTML = rows.length === 0 ? '<tr><td colspan="5" class="mut">Henüz sayım yok.</td></tr>' : rows.map(([key, r]) => {
      const diff = r.counted - r.system;
      return '<tr><td>' + esc(r.name) + (r.variantName ? ' <span class="mut">— ' + esc(r.variantName) + '</span>' : '') + '</td>' +
        '<td class="r">' + num(r.system) + '</td>' +
        '<td class="r"><input class="inp cnt-in" type="number" min="0" value="' + r.counted + '" data-key="' + esc(key) + '" aria-label="Sayılan miktar" /></td>' +
        '<td class="r ' + (diff === 0 ? '' : diff > 0 ? 'pos' : 'neg') + '">' + (diff > 0 ? '+' : '') + num(diff) + '</td>' +
        '<td><button class="sbtn dl" type="button" data-del="' + esc(key) + '" aria-label="Satırı sil">×</button></td></tr>';
    }).join('');
  }
  countBody.addEventListener('change', e => {
    const inp = e.target.closest('[data-key]');
    if (!inp) return;
    const r = counts.get(inp.dataset.key);
    if (r) { r.counted = Math.max(0, parseInt(inp.value, 10) || 0); renderCounts(); }
  });
  countBody.addEventListener('click', e => {
    const b = e.target.closest('[data-del]');
    if (b) { counts.delete(b.dataset.del); renderCounts(); focusInput(); }
  });
  $('[data-count-clear]').addEventListener('click', () => {
    if (counts.size && !confirm('Sayım listesi temizlensin mi? (Stoklar değişmez)')) return;
    counts.clear(); renderCounts(); focusInput();
  });
  $('[data-count-save]').addEventListener('click', async () => {
    if (counts.size === 0) return alert('Sayılan ürün yok.');
    if (!confirm(counts.size + ' kalemin stoğu sayılan miktara eşitlenecek. Devam edilsin mi?')) return;
    const lines = Array.from(counts.values()).map(r => ({ productId: r.pid, variantId: r.vid, counted: r.counted }));
    const note = $('[data-count-note]').value.trim();
    try {
      const data = await post('/admin/barkod-stok/sayim-kaydet' + (note ? '?note=' + encodeURIComponent(note) : ''), lines, true);
      if (!data.ok) throw new Error(data.message);
      const failed = data.results.filter(r => !r.ok);
      const changed = data.results.filter(r => r.ok && r.diff !== 0);
      show(failed.length ? 'err' : 'ok', '<div><b>Sayım kaydedildi.</b><small>' + changed.length + ' kalemde fark düzeltildi' + (failed.length ? ', ' + failed.length + ' kalem kaydedilemedi' : '') + '.</small>' +
        '<ul class="diffs">' + data.results.map(r => '<li>' + esc(r.name || ('#' + r.productId)) + (r.variantName ? ' — ' + esc(r.variantName) : '') + ': ' +
          (r.ok ? num(r.before) + ' → <b>' + num(r.after) + '</b> (' + (r.diff > 0 ? '+' : '') + num(r.diff) + ')' : '<span class="neg">' + esc(r.message) + '</span>') + '</li>').join('') + '</ul></div>');
      beep(!failed.length);
      if (!failed.length) { counts.clear(); renderCounts(); }
      addLog('Sayım kaydı', { name: changed.length + ' kalem düzeltildi' }, '', '');
    } catch (err) { beep(false); show('err', '<div><b>Sayım kaydedilemedi</b><small>' + esc(err.message) + '</small></div>'); }
    focusInput();
  });

  // ---------------- Geri al ----------------
  undoBtn.addEventListener('click', async () => {
    const lastOp = undoStack.pop();
    if (!lastOp) return;
    undoBtn.disabled = true;
    try {
      const data = await post('/admin/barkod-stok/geri-al', { movementId: lastOp.id });
      beep(data.ok);
      show(data.ok ? 'ok' : 'err', '<div><b>' + esc(lastOp.name) + '</b><small>' + esc(data.message) + '</small></div>');
      if (data.ok) addLog('Geri alındı', { name: lastOp.name }, '', num(data.after));
      else undoStack.push(lastOp);
    } catch (err) { undoStack.push(lastOp); show('err', '<div><b>Geri alınamadı</b><small>' + esc(err.message) + '</small></div>'); }
    undoBtn.disabled = undoStack.length === 0;
    focusInput();
  });

  // ---------------- Oturum günlüğü ----------------
  function addLog(op, p, qty, stock) {
    const empty = $('.emptyrow', logBody);
    if (empty) empty.remove();
    const tr = document.createElement('tr');
    tr.innerHTML = '<td>' + new Date().toLocaleTimeString('tr-TR') + '</td><td>' + esc(op) + '</td><td>' + esc(p.name) + (p.variantName ? ' — ' + esc(p.variantName) : '') + '</td><td class="r">' + esc(qty) + '</td><td class="r">' + esc(stock) + '</td>';
    logBody.prepend(tr);
    logCount++;
    $('[data-log-count]').textContent = logCount + ' işlem';
  }

  // ---------------- Klavye (okuyucu) ----------------
  $('[data-scan-form]').addEventListener('submit', e => { e.preventDefault(); const v = input.value; focusInput(); enqueue(v); });
  input.addEventListener('keydown', e => {
    if (e.key === 'Enter') { e.preventDefault(); const v = input.value; input.value = ''; enqueue(v); }
  });
  // Sayfada başka bir yere tıklanınca imleç tekrar barkod kutusuna dönsün (form alanları hariç).
  document.addEventListener('click', e => {
    if (!e.target.closest('input,select,textarea,button,a,[data-cam-box]')) input.focus();
  });
  qtyInput.addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); focusInput(); } });

  // ---------------- Kamera (telefon) ----------------
  const camBox = $('[data-cam-box]');
  let scanner = null;
  function loadLib() {
    return new Promise((resolve, reject) => {
      if (window.Html5Qrcode || window.__Html5QrcodeLibrary__) return resolve();
      const s = document.createElement('script');
      s.src = camLib; s.onload = resolve; s.onerror = () => reject(new Error('Kamera kütüphanesi yüklenemedi.'));
      document.head.appendChild(s);
    });
  }
  async function stopCamera() {
    if (scanner) { try { await scanner.stop(); scanner.clear(); } catch { } scanner = null; }
    camBox.hidden = true;
  }
  $('[data-camera]').addEventListener('click', async () => {
    if (!window.isSecureContext) { show('err', '<div><b>Kamera kullanılamıyor</b><small>Kamera yalnızca HTTPS (güvenli) bağlantıda çalışır.</small></div>'); return; }
    try {
      await loadLib();
      const Lib = window.Html5Qrcode || (window.__Html5QrcodeLibrary__ && window.__Html5QrcodeLibrary__.Html5Qrcode);
      camBox.hidden = false;
      scanner = new Lib('cam-reader');
      let camLast = { code: '', at: 0 };
      await scanner.start({ facingMode: 'environment' }, { fps: 10, qrbox: { width: 260, height: 160 } }, text => {
        const now = Date.now();
        if (text === camLast.code && now - camLast.at < 2000) return; // kamera aynı kodu sürekli okur
        camLast = { code: text, at: now };
        enqueue(text);
      });
    } catch (err) {
      camBox.hidden = true;
      show('err', '<div><b>Kamera açılamadı</b><small>' + esc(err && err.message ? err.message : err) + '</small></div>');
    }
  });
  $('[data-cam-close]').addEventListener('click', () => { stopCamera(); focusInput(); });
  window.addEventListener('pagehide', stopCamera);

  root.dataset.current = mode;
  renderCounts();
  input.focus();
})();

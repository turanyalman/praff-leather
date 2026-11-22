// PRAFF LEATHER — yönetim paneli etkileşimleri
(function () {
  'use strict';
  const $ = (s, r) => (r || document).querySelector(s);
  const $$ = (s, r) => Array.from((r || document).querySelectorAll(s));
  const csrf = () => ($('meta[name="csrf"]') || {}).content || '';

  // Yıkıcı işlemler için onay (form="..." ile bağlı düğmeler dahil)
  document.addEventListener('submit', e => {
    const f = e.target;
    if (f.matches('[data-confirm]') && !confirm(f.dataset.confirm)) { e.preventDefault(); return; }
    if (f.matches('[data-confirm-cancel]')) {
      const s = f.querySelector('select[name=status]');
      if (s && s.value === 'Cancelled' && !confirm('Sipariş iptal edilecek ve stoklar iade edilecek. Bu işlem geri alınamaz. Devam edilsin mi?')) e.preventDefault();
    }
  }, true);

  // Şifre göster/gizle
  $$('[data-toggle-pass]').forEach(b => b.addEventListener('click', () => {
    const inp = b.parentElement.querySelector('input');
    inp.type = inp.type === 'password' ? 'text' : 'password';
  }));

  // Barkod okuyucu Enter tuşuna basar: barkod kutularında formu göndermesin, sonraki alana geçsin.
  document.addEventListener('keydown', e => {
    const t = e.target;
    if (e.key === 'Enter' && t.matches && t.matches('[data-barcode-input]')) {
      e.preventDefault();
      t.value = t.value.trim();
      t.classList.add('flash-ok');
      setTimeout(() => t.classList.remove('flash-ok'), 600);
    }
  });

  // ---------------- Ürün formu: renk / varyant satırları ----------------
  const box = $('[data-colors]');
  const add = $('[data-add-color]');
  const totalInput = $('[data-total-stock]');
  const totalHint = $('[data-total-hint]');
  const rows = () => $$('.vr', box);
  const reindex = () => rows().forEach((row, i) => $$('[data-f]', row).forEach(inp => { inp.name = 'Form.Colors[' + i + '].' + inp.dataset.f; }));
  const recalc = () => {
    if (!totalInput) return;
    const list = rows().filter(r => $('[data-f=Name]', r).value.trim() !== '');
    if (list.length === 0) {
      totalInput.readOnly = false;
      if (totalHint) totalHint.textContent = 'Renk yoksa stok buradan girilir.';
      return;
    }
    totalInput.readOnly = true;
    totalInput.value = list.reduce((s, r) => s + (parseInt($('[data-f=Stock]', r).value, 10) || 0), 0);
    if (totalHint) totalHint.textContent = 'Renklerin toplamı (renk satırlarından düzenleyin).';
  };
  if (box && add) {
    add.addEventListener('click', () => {
      if (rows().length >= 12) return alert('En fazla 12 renk eklenebilir.');
      const row = document.createElement('div');
      row.className = 'cr vr';
      row.innerHTML =
        '<input type="hidden" value="0" data-f="Id" /><input type="hidden" value="" data-f="OriginalStock" />' +
        '<input placeholder="Renk adı (örn. Siyah)" maxlength="40" aria-label="Renk adı" data-f="Name" />' +
        '<input type="color" value="#161412" aria-label="Renk kodu" data-f="Hex" />' +
        '<input type="number" value="0" min="0" aria-label="Renk stoğu" data-f="Stock" />' +
        '<div class="bcin"><input maxlength="48" placeholder="Okutun veya oluşturun" aria-label="Renk barkodu" data-f="Barcode" data-barcode-input /><button class="sbtn" type="button" data-gen-barcode title="Barkod oluştur">Oluştur</button></div>' +
        '<button class="sbtn dl" type="button" aria-label="Rengi kaldır" data-remove-color>×</button>';
      box.appendChild(row);
      reindex();
      $('[data-f=Name]', row).focus();
      recalc();
    });
    box.addEventListener('click', e => {
      const b = e.target.closest('[data-remove-color]');
      if (!b) return;
      const row = b.closest('.vr');
      const stock = parseInt($('[data-f=Stock]', row).value, 10) || 0;
      if (stock > 0 && !confirm('Bu rengin ' + stock + ' adet stoğu düşülecek. Renk kaldırılsın mı?')) return;
      row.remove();
      reindex();
      recalc();
    });
    box.addEventListener('input', e => { if (e.target.matches('[data-f=Stock],[data-f=Name]')) recalc(); });
    recalc();
  }

  // "Barkod Oluştur": sunucudan bir sonraki boş kodu ister (formdaki diğer kodlarla çakışmaz).
  // İstekler sıraya alınır: art arda tıklamalarda iki alana aynı kod gelmesin.
  let genChain = Promise.resolve();
  document.addEventListener('click', e => {
    const b = e.target.closest('[data-gen-barcode]');
    if (b) genChain = genChain.then(() => generate(b));
  });
  async function generate(b) {
    const inp = $('input', b.parentElement);
    if (!inp || inp.value.trim() !== '') { if (inp) inp.focus(); return; }
    const reserved = $$('[data-barcode-input]').map(x => x.value.trim()).filter(Boolean).join(',');
    b.disabled = true;
    try {
      const body = new FormData(); body.append('reserved', reserved);
      const res = await fetch('/admin/urunler/barkod-oner', { method: 'POST', body, credentials: 'same-origin', headers: { 'RequestVerificationToken': csrf() } });
      const data = await res.json();
      if (data.code) { inp.value = data.code; inp.classList.add('flash-ok'); setTimeout(() => inp.classList.remove('flash-ok'), 600); }
    } catch { alert('Barkod oluşturulamadı. Lütfen tekrar deneyin.'); }
    finally { b.disabled = false; }
  }

  // Tümünü seç (barkod listesi)
  $$('[data-check-all]').forEach(all => all.addEventListener('change', () => {
    $$('input[type=checkbox][name="' + all.dataset.checkAll + '"]').forEach(c => { c.checked = all.checked; });
  }));

  // Gönderimde düğmeyi kilitle
  $$('form.fm').forEach(f => f.addEventListener('submit', e => {
    if (e.defaultPrevented) return;
    setTimeout(() => $$('button[type=submit]', f).forEach(b => b.disabled = true), 0);
  }));

  // Etiket sayfası: yazdır düğmesi
  $$('[data-print]').forEach(b => b.addEventListener('click', () => window.print()));

  // ---------------- Basit zengin metin editörü (kütüphanesiz) ----------------
  // Kalın, italik, başlık, liste, bağlantı. Kaydedilen HTML sunucuda ayrıca temizlenir.
  $$('[data-rte]').forEach(rte => {
    const area = $('[data-rte-area]', rte), src = $('[data-rte-src]', rte), form = rte.closest('form');
    let srcMode = false;
    const sync = () => { if (!srcMode) src.value = area.innerHTML.trim(); };
    try { document.execCommand('defaultParagraphSeparator', false, 'p'); } catch { }
    $$('[data-cmd]', rte).forEach(b => b.addEventListener('click', () => { area.focus(); document.execCommand(b.dataset.cmd, false, null); sync(); }));
    $$('[data-block]', rte).forEach(b => b.addEventListener('click', () => { area.focus(); document.execCommand('formatBlock', false, '<' + b.dataset.block + '>'); sync(); }));
    const linkBtn = $('[data-link]', rte);
    if (linkBtn) linkBtn.addEventListener('click', () => {
      const url = prompt('Bağlantı adresi (https://… , mailto:… veya /sayfa/…):', 'https://');
      if (!url || url === 'https://') return;
      if (!/^(https?:\/\/|mailto:|tel:|\/)/i.test(url)) return alert('Bağlantı https://, mailto:, tel: veya / ile başlamalı.');
      area.focus(); document.execCommand('createLink', false, url); sync();
    });
    const htmlBtn = $('[data-html]', rte);
    if (htmlBtn) htmlBtn.addEventListener('click', () => {
      if (!srcMode) { src.value = area.innerHTML.trim(); src.hidden = false; area.hidden = true; srcMode = true; htmlBtn.classList.add('ok'); }
      else { area.innerHTML = src.value; src.hidden = true; area.hidden = false; srcMode = false; htmlBtn.classList.remove('ok'); }
    });
    area.addEventListener('input', sync);
    // Yapıştırılan içerik düz metne çevrilir (Word'den gelen stil çöpünü önler).
    area.addEventListener('paste', e => {
      e.preventDefault();
      const text = (e.clipboardData || window.clipboardData).getData('text/plain');
      document.execCommand('insertText', false, text);
    });
    if (form) form.addEventListener('submit', () => { if (srcMode) { area.innerHTML = src.value; srcMode = false; } src.value = area.innerHTML.trim(); });
    sync();
  });

  // Toplu e-posta onayı
  $$('form[data-compose]').forEach(f => f.addEventListener('submit', e => {
    const mode = $('input[name=Mode]:checked', f);
    const btn = $('[data-confirm-bulk]', f);
    if (mode && mode.value === 'onayli' && !confirm((btn ? btn.dataset.confirmBulk : '') + ' müşteriye ayrı ayrı e-posta gönderilecek. Devam edilsin mi?')) e.preventDefault();
  }, true));
  $$('form[data-compose] input[name=Mode]').forEach(r => r.addEventListener('change', () => {
    const to = $('form[data-compose] input[name=To]');
    if (to) to.disabled = r.value === 'onayli' && r.checked;
  }));
})();

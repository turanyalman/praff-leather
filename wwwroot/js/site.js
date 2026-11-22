// PRAFF LEATHER — mağaza etkileşimleri (bağımlılık yok)
(function () {
  'use strict';
  const $ = (s, r) => (r || document).querySelector(s);
  const $$ = (s, r) => Array.from((r || document).querySelectorAll(s));
  const csrf = () => ($('meta[name="csrf"]') || {}).content || '';
  const money = v => '₺' + v.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  const num = v => v.toLocaleString('tr-TR');

  // ---------- Bildirim ----------
  function toast(msg, isErr, link) {
    const box = $('[data-toasts]');
    if (!box) return alert(msg);
    const t = document.createElement('div');
    t.className = 'toast' + (isErr ? ' err' : '');
    t.setAttribute('role', isErr ? 'alert' : 'status');
    const span = document.createElement('span');
    span.textContent = msg;
    t.appendChild(span);
    if (link) {
      const a = document.createElement('a');
      a.href = link.href; a.textContent = link.text;
      t.appendChild(a);
    }
    box.appendChild(t);
    setTimeout(() => { t.style.opacity = '0'; t.style.transition = 'opacity .3s'; setTimeout(() => t.remove(), 320); }, isErr ? 6000 : 4000);
  }
  window.praffToast = toast;

  function setCartCount(n) {
    $$('[data-cart-count]').forEach(el => {
      el.textContent = n;
      el.classList.add('bump');
      setTimeout(() => el.classList.remove('bump'), 250);
    });
    $$('.cart-btn').forEach(a => a.setAttribute('aria-label', 'Toptan sepet, ' + n + ' ürün'));
  }

  // ---------- Mobil menü ----------
  const menuBtn = $('[data-menu]'), mnav = $('#mnav');
  if (menuBtn && mnav) {
    menuBtn.addEventListener('click', () => {
      const open = mnav.classList.toggle('open');
      menuBtn.setAttribute('aria-expanded', open);
      document.body.style.overflow = open ? 'hidden' : '';
    });
    mnav.addEventListener('click', e => { if (e.target.closest('a')) { mnav.classList.remove('open'); document.body.style.overflow = ''; } });
  }

  // ---------- Onay diyalogları ----------
  document.addEventListener('submit', e => {
    const f = e.target;
    if (f.matches('[data-confirm]') && !confirm(f.dataset.confirm)) { e.preventDefault(); e.stopImmediatePropagation(); }
  }, true);

  // ---------- Çift gönderimi engelle ----------
  $$('form[data-once]').forEach(f => f.addEventListener('submit', () => {
    setTimeout(() => $$('button[type=submit]', f).forEach(b => { b.disabled = true; b.classList.add('is-busy'); }), 0);
  }));

  // ---------- Sepete ekle (AJAX) ----------
  $$('form[data-ajax-cart]').forEach(form => {
    form.addEventListener('submit', async e => {
      if (form.dataset.productForm !== undefined && !validateQty(form)) { e.preventDefault(); return; }
      e.preventDefault();
      const btn = $('button[type=submit]', form);
      if (btn) { btn.disabled = true; btn.classList.add('is-busy'); }
      try {
        const res = await fetch(form.action, {
          method: 'POST', body: new FormData(form), credentials: 'same-origin',
          headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json', 'RequestVerificationToken': csrf() }
        });
        let data = {};
        try { data = await res.json(); } catch { /* yok say */ }
        if (res.ok && data.ok) {
          setCartCount(data.count);
          toast(data.message, false, { href: '/sepet', text: 'Sepete git' });
          if (form.dataset.productForm !== undefined) {
            const q = $('input[name=quantity]', form);
            form.dataset.incart = (parseInt(form.dataset.incart || '0', 10) + parseInt(q.value, 10)).toString();
            if (form.dataset.incartColor !== undefined) {
              form.dataset.incartColor = (parseInt(form.dataset.incartColor || '0', 10) + parseInt(q.value, 10)).toString();
              const sel = $('[data-color].on', form);
              if (sel) sel.dataset.incart = form.dataset.incartColor;
            }
            updateProductForm(form);
          }
        } else if (res.status === 429) {
          toast('Çok fazla istek. Lütfen biraz bekleyin.', true);
        } else {
          toast(data.message || 'İşlem tamamlanamadı. Lütfen tekrar deneyin.', true);
        }
      } catch {
        toast('Bağlantı hatası. Lütfen tekrar deneyin.', true);
      } finally {
        if (btn) { btn.disabled = false; btn.classList.remove('is-busy'); }
      }
    });
  });

  // ---------- Ürün detay: adet, toplam, renk ----------
  function bounds(form) {
    const min = parseInt(form.dataset.min, 10) || 1;
    const max = parseInt(form.dataset.max, 10) || 0;
    const inCart = parseInt(form.dataset.incart || '0', 10);
    // Renkli ürünlerde stok renk bazındadır: üst sınır seçili rengin sepetteki adedine göre hesaplanır.
    const inCartColor = form.dataset.incartColor !== undefined ? parseInt(form.dataset.incartColor || '0', 10) : inCart;
    return { min: Math.max(1, min - inCart), max: Math.max(0, max - inCartColor), baseMin: min, inCart };
  }
  function validateQty(form) {
    const q = $('input[name=quantity]', form);
    const err = $('[data-qty-error]', form);
    const b = bounds(form);
    const v = parseInt(q.value, 10);
    let msg = '';
    if (!Number.isFinite(v) || v < 1) msg = 'Lütfen geçerli bir adet girin.';
    else if (b.max <= 0) msg = 'Stoktaki tüm adetler zaten sepetinizde.';
    else if (v > b.max) msg = 'Stok yetersiz. En fazla ' + num(b.max) + ' adet ekleyebilirsiniz.';
    else if (v < b.min) msg = 'Minimum sipariş ' + num(b.baseMin) + ' adettir' + (b.inCart ? ' (sepetinizdekilerle birlikte)' : '') + '.';
    if (err) err.textContent = msg;
    return !msg;
  }
  function updateProductForm(form) {
    const q = $('input[name=quantity]', form);
    if (!q) return;
    const b = bounds(form);
    q.min = b.min; q.max = Math.max(b.max, 1);
    const price = parseFloat(form.dataset.price);
    let v = parseInt(q.value, 10);
    if (!Number.isFinite(v)) v = 0;
    const tot = $('[data-total]', form), lbl = $('[data-total-label]', form);
    if (tot) tot.textContent = money(Math.max(v, 0) * price);
    if (lbl) lbl.textContent = 'Toplam (' + num(Math.max(v, 0)) + ' adet × ' + money(price) + ')';
    const [dec, inc] = [$('[data-step="-1"]', form), $('[data-step="1"]', form)];
    if (dec) dec.disabled = v <= b.min;
    if (inc) inc.disabled = v >= b.max;
    validateQty(form);
  }
  $$('form[data-product-form]').forEach(form => {
    const q = $('input[name=quantity]', form);
    if (q) {
      const b = bounds(form);
      if (parseInt(q.value, 10) < b.min || b.inCart) q.value = Math.min(Math.max(b.min, 1), Math.max(b.max, 1));
      $$('[data-step]', form).forEach(btn => btn.addEventListener('click', () => {
        const bb = bounds(form);
        let v = (parseInt(q.value, 10) || 0) + parseInt(btn.dataset.step, 10);
        v = Math.min(Math.max(v, bb.min), Math.max(bb.max, bb.min));
        q.value = v;
        updateProductForm(form);
      }));
      q.addEventListener('input', () => updateProductForm(form));
      updateProductForm(form);
    }
    const colorInput = $('input[name=color]', form), colorName = $('[data-color-name]', form);
    $$('[data-color]', form).forEach(sw => sw.addEventListener('click', () => {
      $$('[data-color]', form).forEach(x => { x.classList.remove('on'); x.setAttribute('aria-checked', 'false'); });
      sw.classList.add('on'); sw.setAttribute('aria-checked', 'true');
      if (colorInput) colorInput.value = sw.dataset.color;
      if (colorName) colorName.textContent = sw.dataset.color;
      if (sw.dataset.stock !== undefined) {
        form.dataset.max = sw.dataset.stock;
        form.dataset.incartColor = sw.dataset.incart || '0';
        const st = $('[data-stock-text]', form);
        const stock = parseInt(sw.dataset.stock, 10) || 0;
        if (st) { st.textContent = sw.dataset.stockText || (stock > 0 ? num(stock) + ' adet stokta' : 'Stokta yok'); st.className = stock > 0 ? 'ok' : 'no'; }
        updateProductForm(form);
      }
    }));
  });

  // ---------- Çerez bilgilendirme şeridi ----------
  const ckb = $('[data-cookie-bar]');
  if (ckb) {
    let seen = false;
    try { seen = localStorage.getItem('praff-cookie-ok') === '1'; } catch { }
    if (!seen) ckb.hidden = false;
    const ok = $('[data-cookie-ok]', ckb);
    if (ok) ok.addEventListener('click', () => { ckb.hidden = true; try { localStorage.setItem('praff-cookie-ok', '1'); } catch { } });
  }

  // ---------- Doğrulama kodu: yalnızca rakam ----------
  $$('input[data-code]').forEach(inp => inp.addEventListener('input', () => { inp.value = inp.value.replace(/\D/g, '').slice(0, 6); }));

  // ---------- Galeri + büyütme ----------
  const main = $('[data-main-image]');
  $$('[data-thumb]').forEach(t => t.addEventListener('click', () => {
    $$('[data-thumb]').forEach(x => x.classList.remove('on'));
    t.classList.add('on');
    if (main) { main.style.opacity = '0'; setTimeout(() => { main.src = t.dataset.thumb; main.style.opacity = '1'; }, 150); }
  }));
  const lb = $('[data-lightbox]');
  if (lb && main) {
    const open = () => { $('img', lb).src = main.src; lb.hidden = false; $('[data-lightbox-close]', lb).focus(); document.body.style.overflow = 'hidden'; };
    const close = () => { lb.hidden = true; document.body.style.overflow = ''; };
    main.addEventListener('click', open);
    const z = $('[data-zoom]'); if (z) z.addEventListener('click', open);
    lb.addEventListener('click', e => { if (e.target === lb || e.target.closest('[data-lightbox-close]')) close(); });
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && !lb.hidden) close(); });
  }

  // ---------- Sekmeler ----------
  $$('[data-tabs]').forEach(box => {
    const tabs = $$('[role=tab]', box);
    tabs.forEach(tab => tab.addEventListener('click', () => {
      tabs.forEach(t => { t.classList.remove('on'); t.setAttribute('aria-selected', 'false'); $('#' + t.getAttribute('aria-controls')).hidden = true; });
      tab.classList.add('on'); tab.setAttribute('aria-selected', 'true');
      $('#' + tab.getAttribute('aria-controls')).hidden = false;
    }));
  });

  // ---------- Sepet satırı adet güncelleme ----------
  $$('form[data-qty-form]').forEach(form => {
    const q = $('input[name=quantity]', form);
    let timer;
    const submit = () => { clearTimeout(timer); timer = setTimeout(() => form.submit(), 500); };
    $$('[data-step]', form).forEach(btn => btn.addEventListener('click', () => {
      const v = Math.max(1, (parseInt(q.value, 10) || 1) + parseInt(btn.dataset.step, 10));
      q.value = Math.min(v, parseInt(q.max, 10) || v);
      submit();
    }));
    q.addEventListener('change', submit);
  });

  // ---------- Katalog: otomatik filtre, görünüm ----------
  $$('form[data-autosubmit]').forEach(f => $$('select, input[type=checkbox]', f).forEach(el => el.addEventListener('change', () => f.submit())));
  const grid = $('[data-grid]');
  const viewBtns = $$('[data-view]');
  if (grid && viewBtns.length) {
    const apply = v => {
      grid.classList.toggle('list', v === 'list');
      viewBtns.forEach(b => { const on = b.dataset.view === v; b.classList.toggle('on', on); b.setAttribute('aria-pressed', on); });
    };
    let saved = 'grid';
    try { saved = localStorage.getItem('praff-view') || 'grid'; } catch { }
    apply(saved);
    viewBtns.forEach(b => b.addEventListener('click', () => { apply(b.dataset.view); try { localStorage.setItem('praff-view', b.dataset.view); } catch { } }));
  }

  // ---------- Şifre göster/gizle ----------
  $$('[data-toggle-pass]').forEach(b => b.addEventListener('click', () => {
    const inp = b.parentElement.querySelector('input');
    const show = inp.type === 'password';
    inp.type = show ? 'text' : 'password';
    b.setAttribute('aria-label', show ? 'Şifreyi gizle' : 'Şifreyi göster');
  }));

  // ---------- Ödeme: kayıtlı adres seçimi ----------
  const pick = $('[data-address-pick]');
  if (pick) {
    const set = (name, v) => { const el = document.querySelector('[name="Form.' + name + '"]'); if (el) el.value = v || ''; };
    pick.addEventListener('change', e => {
      const r = e.target;
      if (!r.matches('input[type=radio]')) return;
      if (r.dataset.new !== undefined) { ['AddressLine', 'City', 'District', 'PostalCode'].forEach(n => set(n, '')); return; }
      set('FullName', r.dataset.fullname); set('Phone', r.dataset.phone); set('City', r.dataset.city);
      set('District', r.dataset.district); set('PostalCode', r.dataset.postal); set('AddressLine', r.dataset.line);
    });
  }

  // ---------- Hero slayt ----------
  const hero = $('[data-hero]');
  if (hero) {
    const slides = $$('.slide', hero);
    const idx = $('[data-hero-index]', hero), bar = $('[data-hero-bar]', hero);
    let i = 0, timer;
    const show = n => {
      i = (n + slides.length) % slides.length;
      slides.forEach((s, k) => s.classList.toggle('on', k === i));
      if (idx) idx.textContent = String(i + 1).padStart(2, '0') + ' / ' + String(slides.length).padStart(2, '0');
      if (bar) bar.style.width = ((i + 1) / slides.length * 100) + '%';
    };
    const auto = () => { clearInterval(timer); if (slides.length > 1 && !matchMedia('(prefers-reduced-motion: reduce)').matches) timer = setInterval(() => show(i + 1), 6000); };
    const p = $('[data-hero-prev]', hero), n = $('[data-hero-next]', hero);
    if (p) p.addEventListener('click', () => { show(i - 1); auto(); });
    if (n) n.addEventListener('click', () => { show(i + 1); auto(); });
    auto();
  }

  // ---------- Yasal sayfa menüsü ----------
  const legal = $('[data-legal-nav]');
  if (legal && 'IntersectionObserver' in window) {
    const links = $$('a', legal);
    const io = new IntersectionObserver(entries => entries.forEach(en => {
      if (en.isIntersecting) links.forEach(a => a.classList.toggle('on', a.getAttribute('href') === '#' + en.target.id));
    }), { rootMargin: '-30% 0px -60% 0px' });
    $$('.lp').forEach(s => io.observe(s));
  }
})();

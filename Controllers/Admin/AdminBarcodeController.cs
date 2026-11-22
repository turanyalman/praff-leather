using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

/// <summary>Barkod/QR görselleri, Barkod &amp; QR ayarları, toplu barkod üretimi ve etiket yazdırma.</summary>
public class AdminBarcodeController(AppDbContext db, BarcodeService barcodes, SiteSettingsService settings) : AdminBaseController
{
    private static readonly string[] Sizes = ["50x30", "40x25", "a4-3x8"];

    private ContentResult Svg(string svg)
    {
        Response.Headers.CacheControl = "private, max-age=300";
        return Content(svg, "image/svg+xml; charset=utf-8");
    }

    [HttpGet("/admin/barcode/{productId:int}.svg")]
    public async Task<IActionResult> BarcodeSvg(int productId, int? renk)
    {
        string? code = renk is > 0
            ? await db.ProductColors.Where(c => c.Id == renk && c.ProductId == productId).Select(c => c.Barcode).FirstOrDefaultAsync()
            : await db.Products.Where(p => p.Id == productId).Select(p => p.Barcode).FirstOrDefaultAsync();
        if (string.IsNullOrEmpty(code)) return NotFound();
        try { return Svg(BarcodeService.RenderSvg(code)); }
        catch (ArgumentException) { return NotFound(); }
    }

    [HttpGet("/admin/qr/{productId:int}.svg")]
    public async Task<IActionResult> QrSvg(int productId)
    {
        var slug = await db.Products.Where(p => p.Id == productId).Select(p => p.Slug).FirstOrDefaultAsync();
        if (slug == null) return NotFound();
        return Svg(BarcodeEncoder.QrSvg($"{await barcodes.BaseUrlAsync(Request)}/urun/{slug}"));
    }

    // ------------------------------------------------ Barkod & QR sayfası
    [HttpGet("/admin/barkod")]
    public async Task<IActionResult> Index(string? q, string? filtre)
    {
        ViewData["Title"] = "Barkod & QR";
        ViewData["Nav"] = "barcode";
        var s = await settings.GetAsync();
        ViewData["Form"] = new BarcodeSettingsForm
        {
            Format = s[SettingKeys.BarcodeFormat], Prefix = s[SettingKeys.BarcodePrefix], LabelSize = s[SettingKeys.LabelSize],
            ShowName = s.Flag(SettingKeys.LabelShowName), ShowSku = s.Flag(SettingKeys.LabelShowSku), ShowPrice = s.Flag(SettingKeys.LabelShowPrice), ShowQr = s.Flag(SettingKeys.LabelShowQr)
        };
        ViewData["q"] = q; ViewData["filtre"] = filtre;
        var rows = await RowsAsync(q);
        ViewData["Missing"] = rows.Count(r => string.IsNullOrEmpty(r.Barcode));
        if (filtre == "eksik") rows = rows.Where(r => string.IsNullOrEmpty(r.Barcode)).ToList();
        return View(rows);
    }

    /// <summary>Etiketlenebilir satırlar: renksiz ürünler + her renk ayrı satır.</summary>
    private async Task<List<BarcodeRow>> RowsAsync(string? q)
    {
        var query = db.Products.AsNoTracking().Include(p => p.Colors).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, like) || EF.Functions.Like(p.Sku, like) || EF.Functions.Like(p.Barcode ?? "", like) || p.Colors.Any(c => EF.Functions.Like(c.Barcode ?? "", like)));
        }
        var list = await query.OrderBy(p => p.Name).ToListAsync();
        var rows = new List<BarcodeRow>();
        foreach (var p in list)
        {
            if (p.Colors.Count == 0 || !string.IsNullOrEmpty(p.Barcode))
                rows.Add(new BarcodeRow { ProductId = p.Id, Name = p.Name, Sku = p.Sku, Barcode = p.Barcode, Stock = p.StockQuantity, IsActive = p.IsActive });
            foreach (var c in p.Colors.OrderBy(c => c.SortOrder))
                rows.Add(new BarcodeRow { ProductId = p.Id, VariantId = c.Id, Name = p.Name, VariantName = c.Name, Sku = p.Sku, Barcode = c.Barcode, Stock = c.StockQuantity, IsActive = p.IsActive });
        }
        return rows;
    }

    [HttpPost("/admin/barkod/ayarlar")]
    public async Task<IActionResult> SaveSettings(BarcodeSettingsForm form)
    {
        if (form.Format is not (BarcodeService.FormatCode128 or BarcodeService.FormatEan13)) form.Format = BarcodeService.FormatCode128;
        if (!Sizes.Contains(form.LabelSize)) form.LabelSize = "50x30";
        await settings.SaveAsync(new Dictionary<string, string?>
        {
            [SettingKeys.BarcodeFormat] = form.Format,
            [SettingKeys.BarcodePrefix] = BarcodeService.CleanPrefix(form.Prefix),
            [SettingKeys.LabelSize] = form.LabelSize,
            [SettingKeys.LabelShowName] = form.ShowName ? "1" : "0",
            [SettingKeys.LabelShowSku] = form.ShowSku ? "1" : "0",
            [SettingKeys.LabelShowPrice] = form.ShowPrice ? "1" : "0",
            [SettingKeys.LabelShowQr] = form.ShowQr ? "1" : "0"
        });
        Ok("Barkod ve etiket ayarları kaydedildi.");
        return Redirect("/admin/barkod");
    }

    /// <summary>Barkodu olmayan tüm ürünlere (renkli ürünlerde her renge) barkod üretir.</summary>
    [HttpPost("/admin/barkod/eksikleri-olustur")]
    public async Task<IActionResult> GenerateMissing()
    {
        var products = await db.Products.Include(p => p.Colors).OrderBy(p => p.Id).ToListAsync();
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;
        foreach (var p in products)
        {
            if (p.Colors.Count == 0)
            {
                if (string.IsNullOrEmpty(p.Barcode)) { p.Barcode = await barcodes.NextAsync(reserved); reserved.Add(p.Barcode); count++; }
            }
            else
                foreach (var c in p.Colors.Where(c => string.IsNullOrEmpty(c.Barcode)).OrderBy(c => c.SortOrder))
                { c.Barcode = await barcodes.NextAsync(reserved); reserved.Add(c.Barcode); count++; }
        }
        await db.SaveChangesAsync();
        Ok(count == 0 ? "Tüm ürünlerin barkodu zaten var." : $"{count} ürüne/renge barkod oluşturuldu.");
        return Redirect("/admin/barkod");
    }

    // ------------------------------------------------ Etiket yazdırma
    /// <summary>
    /// Yazdırmaya uygun etiket sayfası. Parametreler: urun=ID (tek ürün, tüm renkleri) veya sec=P{id} / V{id} listesi;
    /// adet=etiket sayısı (her kalem için).
    /// </summary>
    [HttpGet("/admin/barkod/etiket")]
    public async Task<IActionResult> Labels(int? urun, [FromQuery] string[]? sec, int adet = 1, string? boyut = null)
    {
        adet = Math.Clamp(adet, 1, 500);
        var s = await settings.GetAsync();
        var baseUrl = await barcodes.BaseUrlAsync(Request);
        var showQr = s.Flag(SettingKeys.LabelShowQr);

        var wanted = new List<(int ProductId, int? VariantId)>();
        if (urun is > 0)
        {
            var p = await db.Products.AsNoTracking().Include(x => x.Colors).FirstOrDefaultAsync(x => x.Id == urun);
            if (p == null) return NotFound();
            if (p.Colors.Count == 0 || !string.IsNullOrEmpty(p.Barcode)) wanted.Add((p.Id, null));
            wanted.AddRange(p.Colors.OrderBy(c => c.SortOrder).Select(c => (p.Id, (int?)c.Id)));
        }
        foreach (var key in sec ?? [])
        {
            if (key.Length < 2 || !int.TryParse(key[1..], out var id)) continue;
            if (key[0] == 'P') wanted.Add((id, null));
            else if (key[0] == 'V')
            {
                var pid = await db.ProductColors.Where(c => c.Id == id).Select(c => (int?)c.ProductId).FirstOrDefaultAsync();
                if (pid != null) wanted.Add((pid.Value, id));
            }
        }
        if (wanted.Count == 0) { Err("Etiket yazdırmak için en az bir ürün seçin."); return Redirect("/admin/barkod"); }
        if (wanted.Count * adet > 2000) { Err("Tek seferde en fazla 2000 etiket yazdırılabilir."); return Redirect("/admin/barkod"); }

        var ids = wanted.Select(w => w.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Include(p => p.Colors).Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var labels = new List<LabelItem>();
        var skipped = 0;
        var qrCache = new Dictionary<int, string>();
        foreach (var (pid, vid) in wanted.Distinct())
        {
            if (!products.TryGetValue(pid, out var p)) continue;
            var variant = vid == null ? null : p.Colors.FirstOrDefault(c => c.Id == vid);
            var code = variant?.Barcode ?? (vid == null ? p.Barcode : null);
            if (string.IsNullOrEmpty(code)) { skipped++; continue; }
            string svg;
            try { svg = BarcodeService.RenderSvg(code); } catch (ArgumentException) { skipped++; continue; }
            if (showQr && !qrCache.ContainsKey(pid)) qrCache[pid] = BarcodeEncoder.QrSvg($"{baseUrl}/urun/{p.Slug}");
            var item = new LabelItem { Name = p.Name, VariantName = variant?.Name, Sku = p.Sku, Barcode = code, Price = p.Price, BarcodeSvg = svg, QrSvg = showQr ? qrCache[pid] : null };
            for (var i = 0; i < adet; i++) labels.Add(item);
        }

        ViewData["Title"] = "Etiket Yazdır";
        ViewData["Bare"] = true;
        return View(new LabelPageViewModel
        {
            Labels = labels,
            Size = Sizes.Contains(boyut) ? boyut! : s[SettingKeys.LabelSize],
            ShowName = s.Flag(SettingKeys.LabelShowName),
            ShowSku = s.Flag(SettingKeys.LabelShowSku),
            ShowPrice = s.Flag(SettingKeys.LabelShowPrice),
            ShowQr = showQr,
            Skipped = skipped
        });
    }
}

/// <summary>
/// "Barkodla Stok İşlemleri": NETUM gibi klavye modunda çalışan okuyucularla (veya telefon kamerasıyla)
/// stok girişi, çıkışı ve sayım. Tüm stok değişiklikleri StockService üzerinden yapılır.
/// </summary>
public class AdminScanController(AppDbContext db, BarcodeService barcodes, StockService stock) : AdminBaseController
{
    [HttpGet("/admin/barkod-stok")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Barkodla Stok İşlemleri";
        ViewData["Nav"] = "scan";
        return View();
    }

    private static object ProductJson(ScanMatch m) => new
    {
        id = m.Product.Id,
        name = m.Product.Name,
        sku = m.Product.Sku,
        image = m.Product.MainImageUrl,
        stock = m.Variant?.StockQuantity ?? m.Product.StockQuantity,
        variantId = m.Variant?.Id,
        variantName = m.Variant?.Name,
        matchedBy = m.MatchedBy,
        variants = m.Variant == null && m.Product.Colors.Count > 0
            ? m.Product.Colors.OrderBy(c => c.SortOrder).Select(c => new { id = c.Id, name = c.Name, hex = c.HexCode, stock = c.StockQuantity }).ToArray()
            : null
    };

    /// <summary>Yalnızca bulur (sayım modunda ve renk seçimi gerektiğinde).</summary>
    [HttpPost("/admin/barkod-stok/bul"), EnableRateLimiting("scan")]
    public async Task<IActionResult> Find([FromForm] string? code)
    {
        var m = await barcodes.FindAsync(code);
        if (m == null) return Json(new { ok = false, notFound = true, message = "Bu barkod kayıtlı değil." });
        return Json(new { ok = true, product = ProductJson(m) });
    }

    /// <summary>Giriş (+) veya çıkış (−). Renkli üründe ürün barkodu okutulursa renk seçimi istenir.</summary>
    [HttpPost("/admin/barkod-stok/okut"), EnableRateLimiting("scan")]
    public async Task<IActionResult> Scan([FromForm] string? code, [FromForm] string mode, [FromForm] int quantity = 1, [FromForm] int? variantId = null)
    {
        if (mode is not ("giris" or "cikis")) return Json(new { ok = false, message = "Geçersiz işlem." });
        if (quantity < 1 || quantity > 100_000) return Json(new { ok = false, message = "Adet 1 ile 100.000 arasında olmalı." });
        var m = await barcodes.FindAsync(code);
        if (m == null) return Json(new { ok = false, notFound = true, message = "Bu barkod kayıtlı değil." });

        var vid = m.Variant?.Id;
        if (vid == null && variantId != null && m.Product.Colors.Any(c => c.Id == variantId)) vid = variantId;
        if (vid == null && m.Product.Colors.Count > 0)
            return Json(new { ok = false, needVariant = true, message = "Bu ürün renkli. Lütfen rengi seçin.", product = ProductJson(m) });

        var type = mode == "giris" ? StockMovementType.Giris : StockMovementType.Cikis;
        var r = await stock.ChangeAsync(m.Product.Id, vid, type, mode == "giris" ? quantity : -quantity, $"Barkod ile {(mode == "giris" ? "giriş" : "çıkış")}", AdminEmail);
        var variantName = vid == null ? null : m.Product.Colors.First(c => c.Id == vid).Name;
        return Json(new
        {
            ok = r.Success,
            message = r.Message,
            movementId = r.MovementId,
            before = r.Before,
            after = r.After,
            product = new { id = m.Product.Id, name = m.Product.Name, sku = m.Product.Sku, image = m.Product.MainImageUrl, variantId = vid, variantName }
        });
    }

    [HttpPost("/admin/barkod-stok/geri-al"), EnableRateLimiting("scan")]
    public async Task<IActionResult> Undo([FromForm] int movementId)
    {
        var r = await stock.UndoAsync(movementId, AdminEmail);
        return Json(new { ok = r.Success, message = r.Success ? "İşlem geri alındı. " + r.Message : r.Message, after = r.After });
    }

    public record CountLine(int ProductId, int? VariantId, int Counted);

    /// <summary>Sayımı kaydeder: okutulan her ürün/rengin stoğu sayılan miktara eşitlenir, farklar döner.</summary>
    [HttpPost("/admin/barkod-stok/sayim-kaydet")]
    public async Task<IActionResult> SaveCount([FromBody] List<CountLine>? lines, [FromQuery] string? note)
    {
        if (lines == null || lines.Count == 0) return Json(new { ok = false, message = "Sayılan ürün yok." });
        if (lines.Count > 2000) return Json(new { ok = false, message = "Tek seferde en fazla 2000 kalem kaydedilebilir." });
        var results = new List<object>();
        foreach (var l in lines.GroupBy(x => (x.ProductId, x.VariantId)).Select(g => g.Last()))
        {
            if (l.Counted < 0 || l.Counted > StockService.MaxQuantity) { results.Add(new { l.ProductId, l.VariantId, ok = false, message = "Geçersiz miktar." }); continue; }
            var p = await db.Products.AsNoTracking().Include(x => x.Colors).FirstOrDefaultAsync(x => x.Id == l.ProductId);
            if (p == null) { results.Add(new { l.ProductId, l.VariantId, ok = false, message = "Ürün bulunamadı." }); continue; }
            if (p.Colors.Count > 0 && l.VariantId == null) { results.Add(new { l.ProductId, l.VariantId, ok = false, message = "Renk seçilmemiş." }); continue; }
            var r = await stock.SetAsync(p.Id, l.VariantId, StockMovementType.Sayim, l.Counted, string.IsNullOrWhiteSpace(note) ? "Barkodlu sayım" : note, AdminEmail);
            results.Add(new
            {
                l.ProductId, l.VariantId,
                name = p.Name, variantName = l.VariantId == null ? null : p.Colors.FirstOrDefault(c => c.Id == l.VariantId)?.Name,
                ok = r.Success, before = r.Before, after = r.Success ? r.After : r.Before, diff = r.After - r.Before, message = r.Message
            });
        }
        return Json(new { ok = true, results });
    }
}

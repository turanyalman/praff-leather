using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

public partial class AdminProductsController(AppDbContext db, StockService stock, ImageUploadService uploads, BarcodeService barcodes) : AdminBaseController
{
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")] private static partial Regex HexRx();

    [HttpGet("/admin/urunler")]
    public async Task<IActionResult> Index(string? q, int? kategori, string? durum)
    {
        // Barkod, stok kodu veya QR adresi okutulduysa doğrudan ürünün düzenleme sayfasına git.
        if (!string.IsNullOrWhiteSpace(q))
        {
            var hit = await barcodes.FindAsync(q);
            if (hit != null) return Redirect($"/admin/urunler/{hit.Product.Id}");
        }

        ViewData["Title"] = "Ürünler";
        ViewData["Nav"] = "products";
        var query = db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Images).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, like) || EF.Functions.Like(p.Sku, like) || EF.Functions.Like(p.Barcode ?? "", like)
                                     || p.Colors.Any(c => EF.Functions.Like(c.Barcode ?? "", like)));
        }
        if (kategori is > 0) query = query.Where(p => p.CategoryId == kategori);
        if (durum == "aktif") query = query.Where(p => p.IsActive);
        if (durum == "pasif") query = query.Where(p => !p.IsActive);
        ViewData["Categories"] = await db.Categories.AsNoTracking().OrderBy(c => c.DisplayOrder).ToListAsync();
        ViewData["q"] = q; ViewData["kategori"] = kategori; ViewData["durum"] = durum;
        return View(await query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).AsSplitQuery().ToListAsync());
    }

    [HttpGet("/admin/urunler/yeni")]
    public async Task<IActionResult> Create(string? barkod)
    {
        var cats = await db.Categories.AsNoTracking().OrderBy(c => c.DisplayOrder).ToListAsync();
        if (cats.Count == 0) { Err("Önce bir kategori oluşturun."); return Redirect("/admin/kategoriler"); }
        // Barkodla stok ekranında bulunamayan kod "yeni ürüne ata" ile buraya gelir.
        var code = BarcodeService.Validate(barkod?.Trim()) == null ? barkod?.Trim() : null;
        return await EditView(new ProductForm { CategoryId = cats[0].Id, MinOrderQuantity = 10, IsActive = true, CriticalStockLevel = 5, Colors = [], Barcode = code }, null);
    }

    [HttpGet("/admin/urunler/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await db.Products.AsNoTracking().Include(x => x.Colors).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        return await EditView(new ProductForm
        {
            Id = p.Id, Name = p.Name, Slug = p.Slug, Sku = p.Sku, Subtitle = p.Subtitle, ShortDescription = p.ShortDescription,
            Description = p.Description, Features = p.Features, Details = p.Details, Price = p.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ','), StockQuantity = p.StockQuantity,
            OriginalStock = p.StockQuantity, MinOrderQuantity = p.MinOrderQuantity, PackSize = p.PackSize, Badge = p.Badge,
            IsActive = p.IsActive, IsFeatured = p.IsFeatured, CategoryId = p.CategoryId, Barcode = p.Barcode, CriticalStockLevel = p.CriticalStockLevel,
            Colors = p.Colors.OrderBy(c => c.SortOrder).Select(c => new ColorRow { Id = c.Id, Name = c.Name, Hex = c.HexCode, Barcode = c.Barcode, Stock = c.StockQuantity, OriginalStock = c.StockQuantity }).ToList()
        }, p.Id);
    }

    private async Task<IActionResult> EditView(ProductForm form, int? id)
    {
        ViewData["Title"] = id == null ? "Yeni Ürün" : "Ürün Düzenle";
        ViewData["Nav"] = "products";
        return View("Edit", new ProductEditViewModel
        {
            Form = form,
            Categories = await db.Categories.AsNoTracking().OrderBy(c => c.DisplayOrder).ToListAsync(),
            Images = id == null ? [] : await db.ProductImages.AsNoTracking().Where(i => i.ProductId == id).OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToListAsync(),
            OrderCount = id == null ? 0 : await db.OrderItems.Where(i => i.ProductId == id).Select(i => i.OrderId).Distinct().CountAsync(),
            Movements = id == null ? [] : await db.StockMovements.AsNoTracking().Where(m => m.ProductId == id).OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(15).ToListAsync(),
            ProductUrl = string.IsNullOrEmpty(form.Slug) ? null : $"{await barcodes.BaseUrlAsync(Request)}/urun/{form.Slug}"
        });
    }

    /// <summary>"Barkod Oluştur" düğmesi: kaydetmeden bir sonraki boş kodu döndürür.</summary>
    [HttpPost("/admin/urunler/barkod-oner")]
    public async Task<IActionResult> SuggestBarcode([FromForm] string? reserved)
    {
        var taken = new HashSet<string>((reserved ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
        return Json(new { code = await barcodes.NextAsync(taken) });
    }

    [HttpPost("/admin/urunler/kaydet"), RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Save([Bind(Prefix = "Form")] ProductForm form)
    {
        form.Sku = form.Sku?.Trim().ToUpperInvariant() ?? "";
        form.Barcode = string.IsNullOrWhiteSpace(form.Barcode) ? null : form.Barcode.Trim();
        var colors = (form.Colors ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => new ColorRow { Id = c.Id, Name = c.Name!.Trim(), Hex = c.Hex?.Trim(), Barcode = string.IsNullOrWhiteSpace(c.Barcode) ? null : c.Barcode.Trim(), Stock = c.Stock, OriginalStock = c.OriginalStock }).ToList();
        if (colors.Count > 12) ModelState.AddModelError("Form.Colors", "En fazla 12 renk eklenebilir.");
        if (colors.Any(c => c.Name!.Length > 40)) ModelState.AddModelError("Form.Colors", "Renk adı en fazla 40 karakter olabilir.");
        if (colors.Any(c => c.Hex == null || !HexRx().IsMatch(c.Hex))) ModelState.AddModelError("Form.Colors", "Renk kodu #RRGGBB biçiminde olmalı.");
        if (colors.GroupBy(c => c.Name!.ToLowerInvariant()).Any(g => g.Count() > 1)) ModelState.AddModelError("Form.Colors", "Aynı renk adı iki kez kullanılamaz.");
        if (colors.Any(c => c.Stock < 0 || c.Stock > StockService.MaxQuantity)) ModelState.AddModelError("Form.Colors", "Renk stoğu negatif olamaz.");

        // Barkodlar: biçim, form içi tekrar ve başka ürünlerde kullanım kontrolü.
        var allCodes = colors.Where(c => c.Barcode != null).Select(c => c.Barcode!).ToList();
        if (form.Barcode != null) allCodes.Add(form.Barcode);
        foreach (var code in allCodes)
            if (BarcodeService.Validate(code) is { } be) { ModelState.AddModelError("Form.Barcode", $"{code}: {be}"); break; }
        if (allCodes.GroupBy(c => c, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            ModelState.AddModelError("Form.Barcode", "Aynı barkod bu üründe iki kez kullanılmış.");
        var ownVariantIds = colors.Where(c => c.Id > 0).Select(c => c.Id).ToList();
        foreach (var code in allCodes.Distinct())
        {
            var clashP = await db.Products.AsNoTracking().Where(p => p.Barcode == code && p.Id != form.Id).Select(p => p.Name).FirstOrDefaultAsync();
            // Ad ve renk ayrı seçilir: farklı collation'daki sütunlar SQL'de birleştirilemez.
            var clashV = await db.ProductColors.AsNoTracking().Where(c => c.Barcode == code && c.ProductId != form.Id).Select(c => new { P = c.Product!.Name, c.Name }).FirstOrDefaultAsync();
            if (clashP != null || clashV != null) ModelState.AddModelError("Form.Barcode", $"{code} barkodu başka bir üründe kullanılıyor: {clashP ?? $"{clashV!.P} — {clashV.Name}"}.");
        }

        var price = ProductForm.ParsePrice(form.Price);
        if (!string.IsNullOrWhiteSpace(form.Price) && (price == null || price <= 0 || price > 10_000_000))
            ModelState.AddModelError("Form.Price", "Geçerli bir fiyat girin (örn. 350 veya 1250,50).");
        if (!await db.Categories.AnyAsync(c => c.Id == form.CategoryId)) ModelState.AddModelError("Form.CategoryId", "Kategori seçin.");
        if (await db.Products.AnyAsync(p => p.Sku == form.Sku && p.Id != form.Id)) ModelState.AddModelError("Form.Sku", "Bu ürün kodu başka bir üründe kullanılıyor.");

        var slug = string.IsNullOrWhiteSpace(form.Slug) ? Slug.Create(form.Name ?? "") : form.Slug.Trim();
        if (await db.Products.AnyAsync(p => p.Slug == slug && p.Id != form.Id))
        {
            if (!string.IsNullOrWhiteSpace(form.Slug)) ModelState.AddModelError("Form.Slug", "Bu adres başka bir üründe kullanılıyor.");
            else { var b = slug; var n = 2; while (await db.Products.AnyAsync(p => p.Slug == slug && p.Id != form.Id)) slug = $"{b}-{n++}"; }
        }

        var newImages = (form.NewImages ?? []).Where(f => f.Length > 0).ToList();
        if (newImages.Count > 10) ModelState.AddModelError("Form.NewImages", "Tek seferde en fazla 10 görsel yükleyebilirsiniz.");

        if (!ModelState.IsValid)
        {
            form.Colors = colors;
            return await EditView(form, form.Id == 0 ? null : form.Id);
        }

        Product p;
        var isNew = form.Id == 0;
        if (isNew)
        {
            p = new Product { StockQuantity = 0 };
            db.Products.Add(p);
        }
        else
        {
            var found = await db.Products.Include(x => x.Colors).FirstOrDefaultAsync(x => x.Id == form.Id);
            if (found == null) return NotFound();
            p = found;
        }

        p.Name = form.Name!.Trim(); p.Slug = slug; p.Sku = form.Sku; p.Subtitle = Clean(form.Subtitle);
        p.ShortDescription = Clean(form.ShortDescription); p.Description = Clean(form.Description);
        p.Features = Clean(form.Features); p.Details = Clean(form.Details);
        p.Price = Math.Round(price!.Value, 2); p.MinOrderQuantity = form.MinOrderQuantity; p.PackSize = form.PackSize;
        p.Badge = Clean(form.Badge); p.IsActive = form.IsActive; p.IsFeatured = form.IsFeatured; p.CategoryId = form.CategoryId;
        p.Barcode = form.Barcode; p.CriticalStockLevel = form.CriticalStockLevel;
        p.UpdatedAt = DateTime.UtcNow;

        // Renkler kimliğiyle güncellenir (stok ve barkod korunur); yeni renkler 0 stokla eklenir, stok aşağıda hareketle yazılır.
        var hadVariants = p.Colors.Count > 0;
        var removed = p.Colors.Where(c => !colors.Any(r => r.Id == c.Id)).ToList();
        var newRows = new List<(ProductColor Entity, ColorRow Row)>();
        for (var i = 0; i < colors.Count; i++)
        {
            var row = colors[i];
            var entity = row.Id > 0 ? p.Colors.FirstOrDefault(c => c.Id == row.Id) : null;
            if (entity == null)
            {
                entity = new ProductColor { StockQuantity = 0 };
                p.Colors.Add(entity);
                newRows.Add((entity, row));
            }
            entity.Name = row.Name!;
            entity.HexCode = row.Hex!.ToUpperInvariant();
            entity.Barcode = row.Barcode;
            entity.SortOrder = i;
        }
        // Silinecek renklerin barkodu önce boşaltılır (benzersiz indeks çakışmasın).
        foreach (var r in removed) r.Barcode = null;
        await db.SaveChangesAsync();

        var stockErrors = new List<string>();
        void Check(StockResult r) { if (!r.Success) stockErrors.Add(r.Message); }
        var note = string.IsNullOrWhiteSpace(form.StockReason) ? "Ürün formundan güncellendi" : form.StockReason;

        // Silinen renklerin stoğu hareketle düşülür, sonra renk silinir.
        foreach (var r in removed)
        {
            if (r.StockQuantity > 0) Check(await stock.ChangeAsync(p.Id, r.Id, StockMovementType.Duzeltme, -r.StockQuantity, $"{r.Name} rengi kaldırıldı", AdminEmail));
            db.ProductColors.Remove(r);
        }
        if (removed.Count > 0) await db.SaveChangesAsync();

        if (colors.Count > 0)
        {
            // Renksizken renkli hale geldiyse ürün düzeyindeki stok sıfırlanır; stok artık renklerde tutulur.
            if (!hadVariants && !isNew && p.StockQuantity > 0)
                Check(await stock.SetAsync(p.Id, null, StockMovementType.Duzeltme, 0, "Stok renklere dağıtıldı", AdminEmail));
            foreach (var (entity, row) in newRows)
                if (row.Stock > 0) Check(await stock.SetAsync(p.Id, entity.Id, StockMovementType.Baslangic, row.Stock, isNew ? "Ürün oluşturuldu" : "Yeni renk eklendi", AdminEmail));
            foreach (var row in colors.Where(c => c.Id > 0 && c.OriginalStock != null && c.Stock != c.OriginalStock))
                Check(await stock.SetAsync(p.Id, row.Id, StockMovementType.Duzeltme, row.Stock, note, AdminEmail));
            await stock.SyncTotalAsync(p.Id);
        }
        else if (isNew)
        {
            if (form.StockQuantity > 0) Check(await stock.SetAsync(p.Id, null, StockMovementType.Baslangic, form.StockQuantity, "Ürün oluşturuldu", AdminEmail));
        }
        else if (hadVariants)
        {
            // Tüm renkler kaldırıldı: renk stokları yukarıda düşüldü; ürün stoğu formdaki değere ayarlanır.
            if (form.StockQuantity > 0)
                Check(await stock.SetAsync(p.Id, null, StockMovementType.Duzeltme, form.StockQuantity, "Renkler kaldırıldı, stok ürüne aktarıldı", AdminEmail));
        }
        else if (form.OriginalStock != null && form.StockQuantity != form.OriginalStock)
        {
            Check(await stock.SetAsync(p.Id, null, StockMovementType.Duzeltme, form.StockQuantity, note, AdminEmail));
        }
        if (stockErrors.Count > 0) Err("Stok güncellenemedi: " + string.Join(" · ", stockErrors.Distinct()));

        var errors = new List<string>();
        var nextOrder = (await db.ProductImages.Where(i => i.ProductId == p.Id).MaxAsync(i => (int?)i.SortOrder) ?? -1) + 1;
        foreach (var file in newImages)
        {
            var (url, error) = await uploads.SaveAsync(file, "products");
            if (url == null) { errors.Add($"{file.FileName}: {error}"); continue; }
            db.ProductImages.Add(new ProductImage { ProductId = p.Id, Url = url, AltText = p.Name, SortOrder = nextOrder++ });
        }
        await db.SaveChangesAsync();

        if (errors.Count > 0) Err("Bazı görseller yüklenemedi: " + string.Join(" · ", errors));
        Ok(isNew ? "Ürün oluşturuldu." : "Ürün güncellendi.");
        return Redirect($"/admin/urunler/{p.Id}");
    }

    [HttpPost("/admin/urunler/{id:int}/durum")]
    public async Task<IActionResult> Toggle(int id)
    {
        var p = await db.Products.FindAsync(id);
        if (p == null) return NotFound();
        p.IsActive = !p.IsActive;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Ok(p.IsActive ? $"{p.Name} yayına alındı." : $"{p.Name} pasife alındı; sitede görünmez.");
        return Redirect("/admin/urunler");
    }

    [HttpPost("/admin/urunler/{id:int}/sil")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.Products.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        var urls = p.Images.Select(i => i.Url).ToList();
        // Sipariş satırları ürün silinse de anlık görüntü (ad, kod, fiyat) ile korunur; ProductId null olur.
        db.Products.Remove(p);
        await db.SaveChangesAsync();
        foreach (var u in urls)
            if (!await db.ProductImages.AnyAsync(i => i.Url == u) && !await db.OrderItems.AnyAsync(i => i.ImageUrl == u))
                uploads.TryDelete(u); // sipariş geçmişinde kullanılan görseller silinmez
        Ok($"{p.Name} silindi. Geçmiş siparişlerdeki kayıtlar korunur.");
        return Redirect("/admin/urunler");
    }

    [HttpPost("/admin/urunler/{id:int}/gorsel/{imageId:int}/sil")]
    public async Task<IActionResult> DeleteImage(int id, int imageId)
    {
        var img = await db.ProductImages.FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == id);
        if (img == null) return NotFound();
        db.ProductImages.Remove(img);
        await db.SaveChangesAsync();
        if (!await db.ProductImages.AnyAsync(i => i.Url == img.Url) && !await db.OrderItems.AnyAsync(i => i.ImageUrl == img.Url))
            uploads.TryDelete(img.Url);
        Ok("Görsel silindi.");
        return Redirect($"/admin/urunler/{id}#gorseller");
    }

    [HttpPost("/admin/urunler/{id:int}/gorsel/{imageId:int}/tasi")]
    public async Task<IActionResult> MoveImage(int id, int imageId, string dir)
    {
        var imgs = await db.ProductImages.Where(i => i.ProductId == id).OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToListAsync();
        var idx = imgs.FindIndex(i => i.Id == imageId);
        if (idx < 0) return NotFound();
        var target = dir == "ana" ? 0 : dir == "sol" ? idx - 1 : idx + 1;
        if (target >= 0 && target < imgs.Count && target != idx)
        {
            var item = imgs[idx];
            imgs.RemoveAt(idx);
            imgs.Insert(target, item);
            for (var i = 0; i < imgs.Count; i++) imgs[i].SortOrder = i;
            await db.SaveChangesAsync();
        }
        return Redirect($"/admin/urunler/{id}#gorseller");
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Replace("\r\n", "\n");
}

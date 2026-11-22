using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

public class AdminInventoryController(AppDbContext db, StockService stock) : AdminBaseController
{
    [HttpGet("/admin/stok")]
    public async Task<IActionResult> Index(string? q, string? filtre)
    {
        ViewData["Title"] = "Stok Yönetimi";
        ViewData["Nav"] = "stock";
        var query = db.Products.AsNoTracking().Include(p => p.Colors).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, like) || EF.Functions.Like(p.Sku, like) || EF.Functions.Like(p.Barcode ?? "", like) || p.Colors.Any(c => EF.Functions.Like(c.Barcode ?? "", like)));
        }
        if (filtre == "kritik")
            query = query.Where(p => (!p.Colors.Any() && p.StockQuantity <= p.CriticalStockLevel) || p.Colors.Any(c => c.StockQuantity <= p.CriticalStockLevel));
        if (filtre == "yok") query = query.Where(p => p.StockQuantity == 0 || p.Colors.Any(c => c.StockQuantity == 0));
        ViewData["q"] = q; ViewData["filtre"] = filtre;
        return View(await query.OrderBy(p => p.Name).AsSplitQuery().ToListAsync());
    }

    [HttpPost("/admin/stok/guncelle")]
    public async Task<IActionResult> Adjust(StockAdjustForm form, string? returnQ)
    {
        if (!ModelState.IsValid || form.Amount < 0) Err("Geçersiz stok işlemi.");
        else
        {
            var hasVariants = await db.ProductColors.AnyAsync(c => c.ProductId == form.ProductId);
            if (hasVariants && form.VariantId == null) Err("Renkli üründe işlem renk satırından yapılmalıdır.");
            else
            {
                var r = form.Mode switch
                {
                    "increase" => await stock.ChangeAsync(form.ProductId, form.VariantId, StockMovementType.Giris, form.Amount, form.Reason, AdminEmail),
                    "decrease" => await stock.ChangeAsync(form.ProductId, form.VariantId, StockMovementType.Cikis, -form.Amount, form.Reason, AdminEmail),
                    "set" => await stock.SetAsync(form.ProductId, form.VariantId, StockMovementType.Duzeltme, form.Amount, form.Reason, AdminEmail),
                    _ => StockResult.Fail("Geçersiz stok işlemi.")
                };
                if (r.Success) Ok(r.Message); else Err(r.Message);
            }
        }
        return Redirect("/admin/stok" + (string.IsNullOrEmpty(returnQ) ? "" : "?q=" + Uri.EscapeDataString(returnQ)));
    }

    private IQueryable<StockMovement> Filtered(int? urun, string? tur, string? baslangic, string? bitis)
    {
        var query = db.StockMovements.AsNoTracking().Include(m => m.Product).AsQueryable();
        if (urun is > 0) query = query.Where(m => m.ProductId == urun);
        if (Enum.TryParse<StockMovementType>(tur, out var t) && Enum.IsDefined(t)) query = query.Where(m => m.Type == t);
        if (TryDate(baslangic, out var from)) { var f = TimeZoneInfo.ConvertTimeToUtc(from, Fmt.TrZone); query = query.Where(m => m.CreatedAt >= f); }
        if (TryDate(bitis, out var to)) { var e = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1), Fmt.TrZone); query = query.Where(m => m.CreatedAt < e); }
        return query;
    }

    private static bool TryDate(string? s, out DateTime d) =>
        DateTime.TryParseExact(s, ["yyyy-MM-dd", "dd.MM.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

    [HttpGet("/admin/stok/hareketler")]
    public async Task<IActionResult> History(int? urun, string? tur, string? baslangic, string? bitis, int sayfa = 1)
    {
        ViewData["Title"] = "Stok Hareketleri";
        ViewData["Nav"] = "stock";
        var query = Filtered(urun, tur, baslangic, bitis);
        if (urun is > 0) ViewData["Product"] = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == urun);
        const int size = 50;
        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        sayfa = Math.Clamp(sayfa, 1, pages);
        ViewData["urun"] = urun; ViewData["tur"] = tur; ViewData["baslangic"] = baslangic; ViewData["bitis"] = bitis;
        return View(new Paged<StockMovement>
        {
            Items = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Skip((sayfa - 1) * size).Take(size).ToListAsync(),
            Page = sayfa, TotalPages = pages, Total = total
        });
    }

    /// <summary>Filtrelenmiş stok hareketlerini Excel'de açılabilen CSV olarak indirir.</summary>
    [HttpGet("/admin/stok/hareketler.csv")]
    public async Task<IActionResult> HistoryCsv(int? urun, string? tur, string? baslangic, string? bitis)
    {
        var list = await Filtered(urun, tur, baslangic, bitis).OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(50_000).ToListAsync();
        var bytes = Fmt.CsvFile(
            ["Tarih", "Ürün", "Stok Kodu", "Renk", "İşlem", "Adet", "Önce", "Sonra", "Not", "Kim", "Sipariş No"],
            list.Select(m => new object?[] { m.CreatedAt, m.Product?.Name, m.Product?.Sku, m.VariantName, Fmt.MovementText(m.Type), m.Quantity, m.StockBefore, m.StockAfter, m.Note, m.CreatedBy, m.OrderId }));
        return File(bytes, "text/csv; charset=utf-8", $"stok-hareketleri-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }
}

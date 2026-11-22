using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

public class AdminOrdersController(AppDbContext db, OrderService orders) : AdminBaseController
{
    private IQueryable<Order> Filtered(string? q, string? durum, string? baslangic, string? bitis)
    {
        var query = db.Orders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(o => EF.Functions.Like(o.OrderNumber, like) || EF.Functions.Like(o.FullName, like) || EF.Functions.Like(o.Email, like) || EF.Functions.Like(o.Phone, like) || EF.Functions.Like(o.CompanyName ?? "", like));
        }
        if (Enum.TryParse<OrderStatus>(durum, out var st) && Enum.IsDefined(st)) query = query.Where(o => o.Status == st);
        if (TryDate(baslangic, out var from)) { var f = TimeZoneInfo.ConvertTimeToUtc(from, Fmt.TrZone); query = query.Where(o => o.CreatedAt >= f); }
        if (TryDate(bitis, out var to)) { var t = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1), Fmt.TrZone); query = query.Where(o => o.CreatedAt < t); }
        return query;
    }

    /// <summary>Filtrelenmiş siparişleri satır bazında (her ürün ayrı satır) CSV olarak indirir.</summary>
    [HttpGet("/admin/siparisler.csv")]
    public async Task<IActionResult> Csv(string? q, string? durum, string? baslangic, string? bitis)
    {
        var list = await Filtered(q, durum, baslangic, bitis).Include(o => o.Items).OrderByDescending(o => o.CreatedAt).Take(20_000).ToListAsync();
        var bytes = Fmt.CsvFile(
            ["Sipariş No", "Tarih", "Durum", "Ödeme", "Firma", "Ad Soyad", "E-posta", "Telefon", "Vergi No", "İl", "İlçe", "Ürün", "Stok Kodu", "Renk", "Adet", "Birim Fiyat", "Satır Tutarı", "Kargo", "Sipariş Toplamı"],
            list.SelectMany(o => o.Items.Select(i => new object?[]
            {
                o.OrderNumber, o.CreatedAt, Fmt.StatusText(o.Status), Fmt.PaymentText(o.PaymentStatus), o.CompanyName, o.FullName, o.Email, o.Phone, o.TaxNumber,
                o.City, o.District, i.ProductName, i.Sku, i.ColorName, i.Quantity, i.UnitPrice, i.LineTotal, o.ShippingCost, o.Total
            })));
        return File(bytes, "text/csv; charset=utf-8", $"siparisler-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    [HttpGet("/admin/siparisler")]
    public async Task<IActionResult> Index(string? q, string? durum, string? baslangic, string? bitis, int sayfa = 1)
    {
        ViewData["Title"] = "Siparişler";
        ViewData["Nav"] = "orders";
        var query = Filtered(q, durum, baslangic, bitis);

        const int size = 25;
        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        sayfa = Math.Clamp(sayfa, 1, pages);
        ViewData["q"] = q; ViewData["durum"] = durum; ViewData["baslangic"] = baslangic; ViewData["bitis"] = bitis;
        return View(new Paged<Order>
        {
            Items = await query.Include(o => o.Items).OrderByDescending(o => o.CreatedAt).Skip((sayfa - 1) * size).Take(size).ToListAsync(),
            Page = sayfa, TotalPages = pages, Total = total
        });
    }

    private static bool TryDate(string? s, out DateTime d) =>
        DateTime.TryParseExact(s, ["yyyy-MM-dd", "dd.MM.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

    [HttpGet("/admin/siparisler/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var o = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.StatusHistory).Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return NotFound();
        ViewData["Title"] = $"Sipariş {o.OrderNumber}";
        ViewData["Nav"] = "orders";
        return View(o);
    }

    [HttpPost("/admin/siparisler/{id:int}/durum")]
    public async Task<IActionResult> Status(int id, OrderStatus status, string? note)
    {
        var r = await orders.UpdateStatusAsync(id, status, note, AdminEmail);
        if (r.Success) Ok(r.Message); else Err(r.Message);
        return Redirect($"/admin/siparisler/{id}");
    }

    [HttpPost("/admin/siparisler/{id:int}/odeme")]
    public async Task<IActionResult> Payment(int id, PaymentStatus payment)
    {
        var o = await db.Orders.FindAsync(id);
        if (o == null) return NotFound();
        if (!Enum.IsDefined(payment)) { Err("Geçersiz ödeme durumu."); return Redirect($"/admin/siparisler/{id}"); }
        if (o.Status == OrderStatus.Cancelled && payment == PaymentStatus.Paid) { Err("İptal edilmiş sipariş “Ödendi” yapılamaz."); return Redirect($"/admin/siparisler/{id}"); }
        o.PaymentStatus = payment;
        o.UpdatedAt = DateTime.UtcNow;
        db.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = id, Status = o.Status, ChangedBy = AdminEmail, Note = $"Ödeme durumu: {Fmt.PaymentText(payment)}" });
        await db.SaveChangesAsync();
        Ok($"Ödeme durumu “{Fmt.PaymentText(payment)}” olarak güncellendi.");
        return Redirect($"/admin/siparisler/{id}");
    }

    [HttpPost("/admin/siparisler/{id:int}/not")]
    public async Task<IActionResult> Note(int id, string? adminNote)
    {
        var o = await db.Orders.FindAsync(id);
        if (o == null) return NotFound();
        o.AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim()[..Math.Min(adminNote.Trim().Length, 1000)];
        await db.SaveChangesAsync();
        Ok("Yönetici notu kaydedildi.");
        return Redirect($"/admin/siparisler/{id}");
    }
}

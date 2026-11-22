using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

public class AdminDashboardController(AppDbContext db, SiteSettingsService settings) : AdminBaseController
{
    [HttpGet("/admin")]
    public async Task<IActionResult> Index()
    {
        var s = await settings.GetAsync();
        var low = s.Int(SettingKeys.LowStockThreshold, 50);
        var valid = db.Orders.Where(o => o.Status != OrderStatus.Cancelled);

        // Dönem sınırları Türkiye saatine göre hesaplanır, UTC'ye çevrilerek sorgulanır.
        var nowTr = Fmt.Local(DateTime.UtcNow);
        DateTime Utc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Fmt.TrZone);
        var todayStart = Utc(nowTr.Date);
        var weekStart = Utc(nowTr.Date.AddDays(-(((int)nowTr.DayOfWeek + 6) % 7))); // Pazartesi
        var monthStart = Utc(new DateTime(nowTr.Year, nowTr.Month, 1));
        var prevMonthStart = Utc(new DateTime(nowTr.Year, nowTr.Month, 1).AddMonths(-1));

        async Task<(decimal, int)> Period(DateTime from, DateTime? to = null)
        {
            var q = valid.Where(o => o.CreatedAt >= from);
            if (to != null) q = q.Where(o => o.CreatedAt < to);
            return (await q.SumAsync(o => (decimal?)o.Total) ?? 0, await q.CountAsync());
        }

        var since = Utc(nowTr.Date.AddDays(-13));
        var recent = await valid.Where(o => o.CreatedAt >= since).Select(o => new { o.CreatedAt, o.Total }).ToListAsync();
        var daily = Enumerable.Range(0, 14).Select(i =>
        {
            var day = nowTr.Date.AddDays(-13 + i);
            var list = recent.Where(o => Fmt.Local(o.CreatedAt).Date == day).ToList();
            return (day, list.Sum(o => o.Total), list.Count);
        }).ToList();

        var totalSales = await valid.SumAsync(o => (decimal?)o.Total) ?? 0;

        // Kritik stok: renksiz ürünlerde ürün stoğu, renkli ürünlerde her renk ayrı değerlendirilir.
        var criticalProducts = await db.Products.AsNoTracking().Include(p => p.Colors)
            .Where(p => (!p.Colors.Any() && p.StockQuantity <= p.CriticalStockLevel) || p.Colors.Any(c => c.StockQuantity <= p.CriticalStockLevel))
            .AsSplitQuery().ToListAsync();
        var critical = criticalProducts.SelectMany(p => p.Colors.Count == 0
                ? [(p, (string?)null, p.StockQuantity)]
                : p.Colors.Where(c => c.StockQuantity <= p.CriticalStockLevel).Select(c => (p, (string?)c.Name, c.StockQuantity)))
            .OrderBy(x => x.Item3).Take(12).ToList();
        var validCount = await valid.CountAsync();
        var awaiting = valid.Where(o => o.PaymentStatus == PaymentStatus.Awaiting);

        var soldItems = await db.OrderItems.Where(i => i.Order!.Status != OrderStatus.Cancelled)
            .Select(i => new { i.ProductName, i.Sku, i.Quantity, i.LineTotal }).ToListAsync();
        var top = soldItems.GroupBy(i => new { i.ProductName, i.Sku })
            .Select(g => new { g.Key.ProductName, g.Key.Sku, Qty = g.Sum(i => i.Quantity), Rev = g.Sum(i => i.LineTotal) })
            .OrderByDescending(x => x.Qty).Take(5).ToList();

        var stock = await db.Products.Select(p => new { p.StockQuantity, p.Price }).ToListAsync();

        ViewData["Title"] = "Dashboard";
        ViewData["Nav"] = "dash";
        return View(new DashboardViewModel
        {
            TotalSales = totalSales,
            PaidSales = await valid.Where(o => o.PaymentStatus == PaymentStatus.Paid).SumAsync(o => (decimal?)o.Total) ?? 0,
            Critical = critical,
            UnverifiedEmails = await db.Users.CountAsync(u => !u.EmailConfirmed && u.IsActive),
            TotalOrders = await db.Orders.CountAsync(),
            PendingOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending),
            TotalCustomers = await db.Users.CountAsync(),
            TotalProducts = await db.Products.CountAsync(),
            ActiveProducts = await db.Products.CountAsync(p => p.IsActive),
            UnreadMessages = await db.ContactMessages.CountAsync(m => !m.IsRead),
            LowStockThreshold = low,
            LowStock = await db.Products.AsNoTracking().Where(p => p.StockQuantity <= low).OrderBy(p => p.StockQuantity).Take(10).ToListAsync(),
            RecentOrders = await db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt).Take(8).ToListAsync(),
            Daily = daily,
            Today = await Period(todayStart),
            Week = await Period(weekStart),
            Month = await Period(monthStart),
            PrevMonth = await Period(prevMonthStart, monthStart),
            AverageOrder = validCount == 0 ? 0 : totalSales / validCount,
            AwaitingPayment = (await awaiting.SumAsync(o => (decimal?)o.Total) ?? 0, await awaiting.CountAsync()),
            StatusCounts = await db.Orders.GroupBy(o => o.Status).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C),
            PendingApprovals = await db.Users.CountAsync(u => !u.IsApproved && u.IsActive),
            PendingCustomers = await db.Users.AsNoTracking().Where(u => !u.IsApproved && u.IsActive).OrderBy(u => u.CreatedAt).Take(5)
                .Select(u => new User { Id = u.Id, FirstName = u.FirstName, LastName = u.LastName, CompanyName = u.CompanyName, City = u.City, CreatedAt = u.CreatedAt }).ToListAsync(),
            NewCustomersMonth = await db.Users.CountAsync(u => u.CreatedAt >= monthStart),
            ApprovedCustomers = await db.Users.CountAsync(u => u.IsApproved),
            TopProducts = top.Select(t => (t.ProductName, t.Sku, t.Qty, t.Rev)).ToList(),
            InventoryValue = stock.Sum(p => p.StockQuantity * p.Price),
            TotalStockUnits = stock.Sum(p => p.StockQuantity),
            OutOfStock = stock.Count(p => p.StockQuantity == 0),
            RecentMessages = await db.ContactMessages.AsNoTracking().Where(m => !m.IsRead).OrderByDescending(m => m.CreatedAt).Take(3).ToListAsync(),
            PinnedNotes = await db.Notes.AsNoTracking().Where(n => n.IsPinned && !n.IsDone).OrderByDescending(n => n.UpdatedAt).Take(4).ToListAsync()
        });
    }
}

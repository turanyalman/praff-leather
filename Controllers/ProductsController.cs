using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers;

[Authorize(Policy = AuthSchemes.ApprovedPolicy)]
public class ProductsController(AppDbContext db, SiteSettingsService settings, CartService cart) : Controller
{
    private const int PageSize = 12;

    [HttpGet("/urunler")]
    public async Task<IActionResult> Index(string? q, string? kategori, string? siralama, int? stok, int sayfa = 1)
    {
        ViewData["Nav"] = "products";
        ViewData["Title"] = "Ürünler";
        var s = await settings.GetAsync();
        var query = db.Products.AsNoTracking().Where(p => p.IsActive && p.Category!.IsActive);
        var allCount = await query.CountAsync();

        q = q?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            if (q.Length > 80) q = q[..80];
            var like = $"%{q}%";
            query = query.Where(p => EF.Functions.Like(p.Name, like) || EF.Functions.Like(p.Sku, like) || EF.Functions.Like(p.Subtitle ?? "", like));
        }
        if (!string.IsNullOrEmpty(kategori)) query = query.Where(p => p.Category!.Slug == kategori);
        var inStock = stok == 1;
        if (inStock) query = query.Where(p => p.StockQuantity > 0);

        var sort = siralama is "artan" or "azalan" or "ad" ? siralama : "yeni";
        query = sort switch
        {
            "artan" => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "azalan" => query.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            "ad" => query.OrderBy(p => p.Name),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        sayfa = Math.Clamp(sayfa, 1, pages);
        var products = await query.Include(p => p.Images).Include(p => p.Category).Include(p => p.Colors)
            .Skip((sayfa - 1) * PageSize).Take(PageSize).AsSplitQuery().ToListAsync();

        return View(new CatalogViewModel
        {
            Products = products,
            Categories = await HomeController.CategoryTilesAsync(db),
            AllCount = allCount,
            Query = q,
            CategorySlug = kategori,
            Sort = sort,
            InStockOnly = inStock,
            Page = sayfa,
            TotalPages = pages,
            TotalCount = total,
            LowStock = s.Int(SettingKeys.LowStockThreshold, 50)
        });
    }

    [HttpGet("/urun/{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        var p = await db.Products.AsNoTracking()
            .Include(x => x.Images).Include(x => x.Colors).Include(x => x.Category)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive && x.Category!.IsActive);
        if (p == null) return NotFound();

        var related = await db.Products.AsNoTracking()
            .Where(x => x.IsActive && x.Id != p.Id && x.Category!.IsActive)
            .OrderByDescending(x => x.CategoryId == p.CategoryId).ThenByDescending(x => x.IsFeatured).ThenByDescending(x => x.CreatedAt)
            .Include(x => x.Images).Include(x => x.Category).Include(x => x.Colors)
            .Take(4).AsSplitQuery().ToListAsync();

        var view = await cart.GetAsync();
        var s = await settings.GetAsync();
        ViewData["Nav"] = "products";
        ViewData["Title"] = p.Name;
        ViewData["Description"] = p.ShortDescription;
        return View(new ProductDetailViewModel
        {
            Product = p,
            Related = related,
            InCartQuantity = view.Lines.Where(l => l.ProductId == p.Id).Sum(l => l.Quantity),
            InCartByColor = view.Lines.Where(l => l.ProductId == p.Id).GroupBy(l => l.ColorName).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity)),
            LowStock = s.Int(SettingKeys.LowStockThreshold, 50)
        });
    }
}

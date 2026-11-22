using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers;

public class HomeController(AppDbContext db, SiteSettingsService settings, ILogger<HomeController> log) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index()
    {
        ViewData["Nav"] = "home";
        return View(await BuildHomeAsync(new ContactForm()));
    }

    private async Task<HomeViewModel> BuildHomeAsync(ContactForm contact)
    {
        var active = db.Products.AsNoTracking().Where(p => p.IsActive && p.Category!.IsActive);
        // Ürün ve fiyatlar yalnızca onaylı müşterilere gönderilir (HTML'e hiç yazılmaz).
        if (!User.IsApprovedCustomer())
            return new HomeViewModel { Settings = await settings.GetAsync(), Categories = await CategoryTilesAsync(db), TotalProducts = await active.CountAsync(), Contact = contact };
        var featured = await active.Where(p => p.IsFeatured)
            .Include(p => p.Images).Include(p => p.Category).Include(p => p.Colors)
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Take(8).ToListAsync();
        if (featured.Count == 0)
            featured = await active.Include(p => p.Images).Include(p => p.Category).Include(p => p.Colors)
                .OrderByDescending(p => p.CreatedAt).Take(8).ToListAsync();

        return new HomeViewModel
        {
            Settings = await settings.GetAsync(),
            Featured = featured,
            Categories = await CategoryTilesAsync(db),
            TotalProducts = await active.CountAsync(),
            Contact = contact
        };
    }

    public static async Task<List<CategoryTile>> CategoryTilesAsync(AppDbContext db)
    {
        var cats = await db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .Select(c => new
            {
                Category = c,
                Count = c.Products.Count(p => p.IsActive),
                Fallback = c.Products.Where(p => p.IsActive).SelectMany(p => p.Images).OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
            }).ToListAsync();
        return cats.Select(c => new CategoryTile { Category = c.Category, ProductCount = c.Count, ImageUrl = c.Category.ImageUrl ?? c.Fallback }).ToList();
    }

    [HttpPost("/iletisim"), EnableRateLimiting("forms")]
    public async Task<IActionResult> Contact([Bind(Prefix = "Contact")] ContactForm form)
    {
        if (!ContactForm.Subjects.Contains(form.Subject)) ModelState.AddModelError("Contact.Subject", "Konu seçin.");
        if (!string.IsNullOrEmpty(form.Website))
        {
            // Bot: sessizce başarılı göster.
            TempData["Ok"] = "Mesajınız alındı. En kısa sürede dönüş yapacağız.";
            return Redirect("/#iletisim");
        }
        if (!ModelState.IsValid)
        {
            ViewData["Nav"] = "home";
            ViewData["ContactError"] = true;
            return View("Index", await BuildHomeAsync(form));
        }
        db.ContactMessages.Add(new ContactMessage
        {
            FullName = form.FullName.Trim(),
            Email = form.Email.Trim(),
            Phone = form.Phone?.Trim(),
            Subject = form.Subject,
            Message = form.Message.Trim()
        });
        await db.SaveChangesAsync();
        TempData["Ok"] = "Mesajınız alındı. En kısa sürede dönüş yapacağız.";
        return Redirect("/#iletisim");
    }

    /// <summary>Eski yasal sayfa adresi: içerikler artık /sayfa/{slug} altında, yönetimden düzenleniyor.</summary>
    [HttpGet("/yasal")]
    public IActionResult Legal() => RedirectPermanent("/sayfa/sikca-sorulan-sorular");

    [HttpGet("/sayfa/{slug}")]
    public async Task<IActionResult> Page(string slug, [FromServices] ContentPageService pages)
    {
        var page = await pages.FindActiveAsync(slug);
        if (page == null) return NotFound();
        ViewData["Title"] = page.Title;
        ViewData["Description"] = $"{page.Title} — PRAFF LEATHER";
        return View(page);
    }

    [HttpGet("/robots.txt")]
    public async Task<IActionResult> Robots([FromServices] BarcodeService barcodes)
    {
        var baseUrl = await barcodes.BaseUrlAsync(Request);
        var txt = "User-agent: *\nDisallow: /admin\nDisallow: /hesap\nDisallow: /sepet\nDisallow: /odeme\nDisallow: /siparis\n" +
                  $"Allow: /\n\nSitemap: {baseUrl}/sitemap.xml\n";
        return Content(txt, "text/plain; charset=utf-8");
    }

    /// <summary>Herkese açık sayfalar (ürün sayfaları yalnızca onaylı müşterilere açık olduğundan eklenmez).</summary>
    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap([FromServices] BarcodeService barcodes)
    {
        var baseUrl = await barcodes.BaseUrlAsync(Request);
        var pages = await db.ContentPages.AsNoTracking().Where(p => p.IsActive).Select(p => new { p.Slug, p.UpdatedAt }).ToListAsync();
        var sb = new System.Text.StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
        void Add(string path, DateTime? mod) => sb.Append($"  <url><loc>{System.Security.SecurityElement.Escape(baseUrl + path)}</loc>{(mod == null ? "" : $"<lastmod>{mod:yyyy-MM-dd}</lastmod>")}</url>\n");
        Add("/", null);
        Add("/hesap/kayit", null);
        foreach (var p in pages) Add($"/sayfa/{p.Slug}", p.UpdatedAt);
        sb.Append("</urlset>\n");
        return Content(sb.ToString(), "application/xml; charset=utf-8");
    }

    // Fiil kısıtı yok: POST isteklerindeki hatalar da (ör. CSRF 400) yeniden yürütülebilsin.
    [Route("/hata"), Route("/hata/{code:int}"), IgnoreAntiforgeryToken]
    public IActionResult Error(int? code)
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature?.Error != null) log.LogError(feature.Error, "İşlenmeyen hata: {Path}", feature.Path);
        var status = code ?? 500;
        Response.StatusCode = status;
        ViewData["Title"] = status == 404 ? "Sayfa bulunamadı" : "Bir sorun oluştu";
        return View("Error", status);
    }
}

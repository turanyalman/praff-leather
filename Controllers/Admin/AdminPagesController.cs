using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

/// <summary>Site Ayarları › Sayfalar ve Yasal Metinler (KVKK, Hakkımızda, Çerez Politikası…).</summary>
public class AdminPagesController(AppDbContext db, ContentPageService pages) : AdminBaseController
{
    [HttpGet("/admin/sayfalar")]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Sayfalar ve Yasal Metinler";
        ViewData["Nav"] = "pages";
        return View(await db.ContentPages.AsNoTracking().OrderBy(p => p.FooterOrder).ThenBy(p => p.Title).ToListAsync());
    }

    [HttpGet("/admin/sayfalar/yeni")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Yeni Sayfa";
        ViewData["Nav"] = "pages";
        return View("Edit", new ContentPageForm());
    }

    [HttpGet("/admin/sayfalar/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await db.ContentPages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        ViewData["Title"] = p.Title;
        ViewData["Nav"] = "pages";
        ViewData["UpdatedAt"] = p.UpdatedAt;
        return View(new ContentPageForm { Id = p.Id, Title = p.Title, Slug = p.Slug, Content = p.Content, ShowInFooter = p.ShowInFooter, IsActive = p.IsActive, IsSystem = p.IsSystem });
    }

    [HttpPost("/admin/sayfalar/kaydet"), RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Save(ContentPageForm form)
    {
        var existing = form.Id == 0 ? null : await db.ContentPages.FirstOrDefaultAsync(x => x.Id == form.Id);
        if (form.Id != 0 && existing == null) return NotFound();

        // Sistem sayfasının adresi değiştirilemez (formlardaki KVKK bağlantıları bozulmasın).
        var slug = existing?.IsSystem == true ? existing.Slug
            : string.IsNullOrWhiteSpace(form.Slug) ? Slug.Create(form.Title ?? "") : form.Slug.Trim();
        if (slug == "urun") slug = "sayfa";
        if (await db.ContentPages.AnyAsync(p => p.Slug == slug && p.Id != form.Id))
            ModelState.AddModelError(nameof(form.Slug), "Bu adres başka bir sayfada kullanılıyor.");
        var clean = ContentPageService.Sanitize(form.Content);
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = form.Id == 0 ? "Yeni Sayfa" : form.Title;
            ViewData["Nav"] = "pages";
            form.Content = clean;
            form.IsSystem = existing?.IsSystem ?? false;
            return View("Edit", form);
        }

        var p = existing ?? new ContentPage { FooterOrder = (await db.ContentPages.MaxAsync(x => (int?)x.FooterOrder) ?? 0) + 1 };
        if (existing == null) db.ContentPages.Add(p);
        p.Title = (form.Title ?? "").Trim();
        p.Slug = slug;
        p.Content = clean;
        p.ShowInFooter = form.ShowInFooter;
        p.IsActive = p.IsSystem || form.IsActive; // sistem sayfası pasife alınamaz
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        pages.Invalidate();
        Ok("Sayfa kaydedildi.");
        return Redirect($"/admin/sayfalar/{p.Id}");
    }

    [HttpPost("/admin/sayfalar/{id:int}/durum")]
    public async Task<IActionResult> Toggle(int id, string alan)
    {
        var p = await db.ContentPages.FindAsync(id);
        if (p == null) return NotFound();
        if (alan == "footer") p.ShowInFooter = !p.ShowInFooter;
        else if (alan == "aktif")
        {
            if (p.IsSystem) { Err("KVKK Aydınlatma Metni pasife alınamaz."); return Redirect("/admin/sayfalar"); }
            p.IsActive = !p.IsActive;
        }
        await db.SaveChangesAsync();
        pages.Invalidate();
        return Redirect("/admin/sayfalar");
    }

    [HttpPost("/admin/sayfalar/{id:int}/tasi")]
    public async Task<IActionResult> Move(int id, string dir)
    {
        var list = await db.ContentPages.OrderBy(p => p.FooterOrder).ThenBy(p => p.Title).ToListAsync();
        var idx = list.FindIndex(p => p.Id == id);
        if (idx < 0) return NotFound();
        var target = dir == "yukari" ? idx - 1 : idx + 1;
        if (target >= 0 && target < list.Count)
        {
            (list[idx], list[target]) = (list[target], list[idx]);
            for (var i = 0; i < list.Count; i++) list[i].FooterOrder = i + 1;
            await db.SaveChangesAsync();
            pages.Invalidate();
        }
        return Redirect("/admin/sayfalar");
    }

    [HttpPost("/admin/sayfalar/{id:int}/sil")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.ContentPages.FindAsync(id);
        if (p == null) return NotFound();
        if (p.IsSystem) { Err("KVKK Aydınlatma Metni silinemez; yalnızca düzenlenebilir."); return Redirect("/admin/sayfalar"); }
        db.ContentPages.Remove(p);
        await db.SaveChangesAsync();
        pages.Invalidate();
        Ok($"“{p.Title}” sayfası silindi.");
        return Redirect("/admin/sayfalar");
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

public class AdminCategoriesController(AppDbContext db, ImageUploadService uploads) : AdminBaseController
{
    [HttpGet("/admin/kategoriler")]
    public async Task<IActionResult> Index(int? id)
    {
        var form = new CategoryForm();
        if (id is > 0)
        {
            var c = await db.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound();
            form = new CategoryForm { Id = c.Id, Name = c.Name, Slug = c.Slug, Description = c.Description, IsActive = c.IsActive, ImageUrl = c.ImageUrl };
        }
        return View(await BuildAsync(form));
    }

    private async Task<CategoriesViewModel> BuildAsync(CategoryForm form)
    {
        ViewData["Title"] = "Kategoriler";
        ViewData["Nav"] = "categories";
        var rows = await db.Categories.AsNoTracking().OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id)
            .Select(c => new { c, n = c.Products.Count() }).ToListAsync();
        return new CategoriesViewModel { Rows = rows.Select(r => (r.c, r.n)).ToList(), Form = form };
    }

    [HttpPost("/admin/kategoriler/kaydet")]
    public async Task<IActionResult> Save([Bind(Prefix = "Form")] CategoryForm form)
    {
        var slug = string.IsNullOrWhiteSpace(form.Slug) ? Slug.Create(form.Name ?? "") : form.Slug.Trim();
        if (await db.Categories.AnyAsync(c => c.Slug == slug && c.Id != form.Id))
            ModelState.AddModelError("Form.Slug", "Bu adres başka bir kategoride kullanılıyor.");
        if (!ModelState.IsValid) return View("Index", await BuildAsync(form));

        Category c;
        if (form.Id == 0)
        {
            c = new Category { DisplayOrder = (await db.Categories.MaxAsync(x => (int?)x.DisplayOrder) ?? 0) + 1 };
            db.Categories.Add(c);
        }
        else
        {
            var found = await db.Categories.FindAsync(form.Id);
            if (found == null) return NotFound();
            c = found;
        }
        c.Name = (form.Name ?? "").Trim();
        c.Slug = slug;
        c.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        c.IsActive = form.IsActive;

        if (form.Image is { Length: > 0 })
        {
            var (url, error) = await uploads.SaveAsync(form.Image, "categories");
            if (url == null)
            {
                ModelState.AddModelError("Form.Image", error!);
                form.ImageUrl = c.ImageUrl;
                return View("Index", await BuildAsync(form));
            }
            uploads.TryDelete(c.ImageUrl);
            c.ImageUrl = url;
        }
        else if (form.RemoveImage)
        {
            uploads.TryDelete(c.ImageUrl);
            c.ImageUrl = null;
        }
        await db.SaveChangesAsync();
        Ok(form.Id == 0 ? "Kategori oluşturuldu." : "Kategori güncellendi.");
        return Redirect($"/admin/kategoriler?id={c.Id}");
    }

    [HttpPost("/admin/kategoriler/{id:int}/tasi")]
    public async Task<IActionResult> Move(int id, string dir)
    {
        var list = await db.Categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).ToListAsync();
        var i = list.FindIndex(c => c.Id == id);
        if (i < 0) return NotFound();
        var j = dir == "yukari" ? i - 1 : i + 1;
        if (j >= 0 && j < list.Count)
        {
            (list[i], list[j]) = (list[j], list[i]);
            for (var k = 0; k < list.Count; k++) list[k].DisplayOrder = k + 1;
            await db.SaveChangesAsync();
        }
        return Redirect("/admin/kategoriler");
    }

    [HttpPost("/admin/kategoriler/{id:int}/durum")]
    public async Task<IActionResult> Toggle(int id)
    {
        var c = await db.Categories.FindAsync(id);
        if (c == null) return NotFound();
        c.IsActive = !c.IsActive;
        await db.SaveChangesAsync();
        Ok(c.IsActive ? $"{c.Name} sitede gösteriliyor." : $"{c.Name} gizlendi; içindeki ürünler de sitede görünmez.");
        return Redirect("/admin/kategoriler");
    }

    [HttpPost("/admin/kategoriler/{id:int}/sil")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Categories.FindAsync(id);
        if (c == null) return NotFound();
        var count = await db.Products.CountAsync(p => p.CategoryId == id);
        if (count > 0)
        {
            Err($"“{c.Name}” kategorisinde {count} ürün var. Önce ürünleri başka bir kategoriye taşıyın veya kategoriyi pasife alın.");
            return Redirect("/admin/kategoriler");
        }
        db.Categories.Remove(c);
        await db.SaveChangesAsync();
        uploads.TryDelete(c.ImageUrl);
        Ok("Kategori silindi.");
        return Redirect("/admin/kategoriler");
    }
}

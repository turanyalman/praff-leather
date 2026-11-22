using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

public class AdminNotesController(AppDbContext db) : AdminBaseController
{
    [HttpGet("/admin/notlar")]
    public async Task<IActionResult> Index(int? duzenle, string? goster)
    {
        ViewData["Title"] = "Notlar";
        ViewData["Nav"] = "notes";
        var q = db.Notes.AsNoTracking().AsQueryable();
        if (goster != "tumu") q = q.Where(n => !n.IsDone);
        ViewData["goster"] = goster;
        ViewData["DoneCount"] = await db.Notes.CountAsync(n => n.IsDone);
        var form = new NoteForm();
        if (duzenle is > 0)
        {
            var n = await db.Notes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == duzenle);
            if (n == null) return NotFound();
            form = new NoteForm { Id = n.Id, Title = n.Title, Content = n.Content, IsPinned = n.IsPinned, Color = n.Color };
        }
        ViewData["Form"] = form;
        return View(await q.OrderByDescending(n => n.IsPinned).ThenBy(n => n.IsDone).ThenByDescending(n => n.UpdatedAt).ToListAsync());
    }

    [HttpPost("/admin/notlar/kaydet")]
    public async Task<IActionResult> Save(NoteForm form)
    {
        if (!NoteForm.Colors.Contains(form.Color)) form.Color = "gold";
        if (!ModelState.IsValid)
        {
            Err(ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Not kaydedilemedi.");
            return Redirect(form.Id > 0 ? $"/admin/notlar?duzenle={form.Id}" : "/admin/notlar");
        }
        Note n;
        if (form.Id == 0) { n = new Note { CreatedBy = AdminEmail }; db.Notes.Add(n); }
        else
        {
            var found = await db.Notes.FindAsync(form.Id);
            if (found == null) return NotFound();
            n = found;
        }
        n.Title = string.IsNullOrWhiteSpace(form.Title) ? null : form.Title.Trim();
        n.Content = form.Content.Trim();
        n.IsPinned = form.IsPinned;
        n.Color = form.Color;
        n.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Ok(form.Id == 0 ? "Not eklendi." : "Not güncellendi.");
        return Redirect($"/admin/notlar#not-{n.Id}");
    }

    [HttpPost("/admin/notlar/{id:int}/{islem}")]
    public async Task<IActionResult> Act(int id, string islem)
    {
        var n = await db.Notes.FindAsync(id);
        if (n == null) return NotFound();
        switch (islem)
        {
            case "sabitle": n.IsPinned = !n.IsPinned; break;
            case "tamam": n.IsDone = !n.IsDone; break;
            case "sil": db.Notes.Remove(n); await db.SaveChangesAsync(); Ok("Not silindi."); return Redirect("/admin/notlar");
            default: return BadRequest();
        }
        n.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Redirect($"/admin/notlar#not-{id}");
    }
}

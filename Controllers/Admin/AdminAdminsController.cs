using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

/// <summary>Yönetici hesapları: ekleme ve silme (kendini ve son yöneticiyi silemez). Her işlem mevcut şifreyle onaylanır.</summary>
public class AdminAdminsController(AppDbContext db, PasswordService passwords, ILogger<AdminAdminsController> log) : AdminBaseController
{
    [HttpGet("/admin/yoneticiler")]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Yöneticiler";
        ViewData["Nav"] = "admins";
        ViewData["Admins"] = await db.Admins.AsNoTracking().OrderBy(a => a.CreatedAt)
            .Select(a => new Models.Admin { Id = a.Id, Email = a.Email, FullName = a.FullName, CreatedAt = a.CreatedAt, LastLoginAt = a.LastLoginAt }).ToListAsync();
        return View(new AdminCreateForm());
    }

    [HttpPost("/admin/yoneticiler/ekle"), EnableRateLimiting("login")]
    public async Task<IActionResult> Create(AdminCreateForm form)
    {
        var me = await db.Admins.FirstOrDefaultAsync(a => a.Id == AdminId);
        if (me == null) return Forbid();
        if (!string.IsNullOrEmpty(form.CurrentPassword) && !passwords.Verify(me.PasswordHash, form.CurrentPassword))
            ModelState.AddModelError(nameof(form.CurrentPassword), "Mevcut şifreniz hatalı.");
        if (AdminAccountController.AdminStrength(form.Password) is { } pe) ModelState.AddModelError(nameof(form.Password), pe);
        var email = form.Email?.Trim().ToLowerInvariant() ?? "";
        if (await db.Admins.AnyAsync(a => a.Email == email)) ModelState.AddModelError(nameof(form.Email), "Bu e-posta ile bir yönetici zaten var.");
        if (await db.Users.AnyAsync(u => u.Email == email)) ModelState.AddModelError(nameof(form.Email), "Bu e-posta bir müşteri hesabında kullanılıyor; farklı bir adres seçin.");
        if (!ModelState.IsValid)
        {
            Err(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return Redirect("/admin/yoneticiler");
        }
        db.Admins.Add(new Models.Admin { Email = email, FullName = form.FullName.Trim(), PasswordHash = passwords.Hash(form.Password) });
        await db.SaveChangesAsync();
        log.LogWarning("Yeni yönetici eklendi: {Email} (ekleyen {By})", email, AdminEmail);
        Ok($"{email} yönetici olarak eklendi. Şifreyi kendisine güvenli bir yoldan iletin; ilk girişte değiştirmesini isteyin.");
        return Redirect("/admin/yoneticiler");
    }

    [HttpPost("/admin/yoneticiler/{id:int}/sil"), EnableRateLimiting("login")]
    public async Task<IActionResult> Delete(int id, string? currentPassword)
    {
        var me = await db.Admins.FirstOrDefaultAsync(a => a.Id == AdminId);
        if (me == null) return Forbid();
        if (string.IsNullOrEmpty(currentPassword) || !passwords.Verify(me.PasswordHash, currentPassword)) { Err("Mevcut şifreniz hatalı."); return Redirect("/admin/yoneticiler"); }
        if (id == me.Id) { Err("Kendi hesabınızı silemezsiniz."); return Redirect("/admin/yoneticiler"); }
        if (await db.Admins.CountAsync() <= 1) { Err("Son yönetici silinemez."); return Redirect("/admin/yoneticiler"); }
        var a = await db.Admins.FindAsync(id);
        if (a == null) return NotFound();
        db.Admins.Remove(a);
        await db.SaveChangesAsync();
        log.LogWarning("Yönetici silindi: {Email} (silen {By})", a.Email, AdminEmail);
        Ok($"{a.Email} yönetici hesabı silindi; açık oturumları kapandı.");
        return Redirect("/admin/yoneticiler");
    }
}

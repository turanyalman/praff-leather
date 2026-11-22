using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

public class AdminMessagesController(AppDbContext db) : AdminBaseController
{
    [HttpGet("/admin/mesajlar")]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Mesajlar";
        ViewData["Nav"] = "messages";
        return View(await db.ContactMessages.AsNoTracking().OrderBy(m => m.IsRead).ThenByDescending(m => m.CreatedAt).Take(300).ToListAsync());
    }

    [HttpPost("/admin/mesajlar/{id:int}/okundu")]
    public async Task<IActionResult> Read(int id)
    {
        var m = await db.ContactMessages.FindAsync(id);
        if (m == null) return NotFound();
        m.IsRead = !m.IsRead;
        await db.SaveChangesAsync();
        return Redirect("/admin/mesajlar");
    }

    [HttpPost("/admin/mesajlar/{id:int}/sil")]
    public async Task<IActionResult> Delete(int id)
    {
        await db.ContactMessages.Where(m => m.Id == id).ExecuteDeleteAsync();
        Ok("Mesaj silindi.");
        return Redirect("/admin/mesajlar");
    }
}

public class AdminSettingsController(SiteSettingsService settings, ImageUploadService uploads) : AdminBaseController
{
    private static readonly string[] UrlKeys = [SettingKeys.InstagramUrl, SettingKeys.YoutubeUrl, SettingKeys.LinkedinUrl, SettingKeys.TiktokUrl];
    /// <summary>Bu formda kaydedilen anahtarlar (barkod ayarları kendi sayfasında kaydedilir).</summary>
    private static readonly string[] FormKeys = [.. SettingKeys.GeneralKeys, SettingKeys.AdminNotifyEmail, SettingKeys.RequireEmailVerification];
    private static readonly string[] FlagKeys = [SettingKeys.CookieBanner, SettingKeys.RequireEmailVerification];

    [HttpGet("/admin/ayarlar")]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Site Ayarları";
        ViewData["Nav"] = "settings";
        var s = await settings.GetAsync();
        return View(new SettingsForm { Values = SettingKeys.Defaults.Keys.ToDictionary(k => k, k => (string?)s.Raw(k)) });
    }

    [HttpPost("/admin/ayarlar"), RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Save(SettingsForm form)
    {
        ViewData["Title"] = "Site Ayarları";
        ViewData["Nav"] = "settings";
        var values = FormKeys.ToDictionary(k => k, k => form.Values.GetValueOrDefault(k)?.Trim());
        foreach (var k in FlagKeys) values[k] = values[k] == "1" ? "1" : "0"; // işaretsiz onay kutusu gönderilmez
        var site = values[SettingKeys.SiteUrl];
        if (!string.IsNullOrEmpty(site))
        {
            if (Uri.TryCreate(site, UriKind.Absolute, out var su) && (su.Scheme == Uri.UriSchemeHttps || su.Scheme == Uri.UriSchemeHttp) && su.AbsolutePath == "/")
                values[SettingKeys.SiteUrl] = $"{su.Scheme}://{su.Authority}";
            else ModelState.AddModelError($"Values[{SettingKeys.SiteUrl}]", "Site adresi https://www.alanadi.com biçiminde olmalı (sonunda yol olmadan).");
        }
        var notify = values[SettingKeys.AdminNotifyEmail];
        if (!string.IsNullOrEmpty(notify) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(notify))
            ModelState.AddModelError($"Values[{SettingKeys.AdminNotifyEmail}]", "Geçerli bir e-posta girin.");

        foreach (var k in UrlKeys)
        {
            var v = values[k];
            if (!string.IsNullOrEmpty(v) && !(Uri.TryCreate(v, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps))
                ModelState.AddModelError($"Values[{k}]", "Adres https:// ile başlayan geçerli bir bağlantı olmalı.");
        }
        if (!string.IsNullOrEmpty(values[SettingKeys.ContactEmail]) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(values[SettingKeys.ContactEmail]))
            ModelState.AddModelError($"Values[{SettingKeys.ContactEmail}]", "Geçerli bir e-posta girin.");
        foreach (var k in new[] { SettingKeys.ShippingCost, SettingKeys.FreeShippingThreshold })
        {
            var v = values[k]?.Replace(',', '.');
            if (!string.IsNullOrEmpty(v) && !(decimal.TryParse(v, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var d) && d >= 0))
                ModelState.AddModelError($"Values[{k}]", "Geçerli bir tutar girin.");
            else values[k] = v;
        }
        if (!string.IsNullOrEmpty(values[SettingKeys.LowStockThreshold]) && !(int.TryParse(values[SettingKeys.LowStockThreshold], out var lt) && lt >= 0))
            ModelState.AddModelError($"Values[{SettingKeys.LowStockThreshold}]", "Geçerli bir sayı girin.");
        // Görsel yolları yalnızca yüklenen dosyalardan gelebilir.
        var current = await settings.GetAsync();
        values[SettingKeys.LogoUrl] = current.Raw(SettingKeys.LogoUrl);
        values[SettingKeys.HeroImage] = current.Raw(SettingKeys.HeroImage);

        if (form.HeroFile is { Length: > 0 })
        {
            var (url, err) = await uploads.SaveAsync(form.HeroFile, "site");
            if (url == null) ModelState.AddModelError("HeroFile", err!); else values[SettingKeys.HeroImage] = url;
        }
        if (form.LogoFile is { Length: > 0 })
        {
            var (url, err) = await uploads.SaveAsync(form.LogoFile, "site");
            if (url == null) ModelState.AddModelError("LogoFile", err!); else values[SettingKeys.LogoUrl] = url;
        }

        if (!ModelState.IsValid)
        {
            var all = SettingKeys.Defaults.Keys.ToDictionary(k => k, k => (string?)current.Raw(k));
            foreach (var (k, v) in values) all[k] = v;
            form.Values = all;
            return View("Index", form);
        }
        if (values[SettingKeys.HeroImage] != current.Raw(SettingKeys.HeroImage)) uploads.TryDelete(current.Raw(SettingKeys.HeroImage));
        if (values[SettingKeys.LogoUrl] != current.Raw(SettingKeys.LogoUrl)) uploads.TryDelete(current.Raw(SettingKeys.LogoUrl));
        await settings.SaveAsync(values);
        Ok("Site ayarları kaydedildi.");
        return Redirect("/admin/ayarlar");
    }

    [HttpPost("/admin/ayarlar/hero-sifirla")]
    public async Task<IActionResult> ResetHero()
    {
        var current = await settings.GetAsync();
        uploads.TryDelete(current.Raw(SettingKeys.HeroImage));
        await settings.SaveAsync(new Dictionary<string, string?> { [SettingKeys.HeroImage] = SettingKeys.Defaults[SettingKeys.HeroImage] });
        Ok("Ana sayfa görseli varsayılana döndü.");
        return Redirect("/admin/ayarlar");
    }
}

public class AdminAccountController(AppDbContext db, PasswordService passwords, ILogger<AdminAccountController> log) : AdminBaseController
{
    [HttpGet("/admin/hesap")]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Hesabım";
        ViewData["Nav"] = "account";
        ViewData["Admin"] = await db.Admins.AsNoTracking().Where(a => a.Id == AdminId).Select(a => new Models.Admin { Email = a.Email, FullName = a.FullName, LastLoginAt = a.LastLoginAt, CreatedAt = a.CreatedAt }).FirstOrDefaultAsync();
        return View(new AdminPasswordForm());
    }

    [HttpPost("/admin/hesap/sifre"), EnableRateLimiting("login")]
    public async Task<IActionResult> ChangePassword(AdminPasswordForm form)
    {
        var admin = await db.Admins.FirstOrDefaultAsync(a => a.Id == AdminId);
        if (admin == null) return Forbid();
        if (!string.IsNullOrEmpty(form.CurrentPassword) && !passwords.Verify(admin.PasswordHash, form.CurrentPassword))
            ModelState.AddModelError(nameof(form.CurrentPassword), "Mevcut şifre hatalı.");
        var err = AdminStrength(form.NewPassword);
        if (err != null) ModelState.AddModelError(nameof(form.NewPassword), err);
        if (!string.IsNullOrEmpty(form.NewPassword) && form.NewPassword == form.CurrentPassword)
            ModelState.AddModelError(nameof(form.NewPassword), "Yeni şifre mevcut şifreyle aynı olamaz.");
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Hesabım";
            ViewData["Nav"] = "account";
            ViewData["Admin"] = new Models.Admin { Email = admin.Email, FullName = admin.FullName, LastLoginAt = admin.LastLoginAt, CreatedAt = admin.CreatedAt };
            return View("Index", new AdminPasswordForm());
        }
        admin.PasswordHash = passwords.Hash(form.NewPassword);
        admin.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        await HttpContext.SignInAsync(AuthSchemes.Admin, AuthSchemes.AdminPrincipal(admin));
        log.LogInformation("Yönetici {Email} şifresini değiştirdi", admin.Email);
        Ok("Şifreniz değiştirildi. Diğer oturumlar kapatıldı.");
        return Redirect("/admin/hesap");
    }

    [HttpPost("/admin/hesap/bilgiler"), EnableRateLimiting("login")]
    public async Task<IActionResult> Profile(AdminProfileForm form)
    {
        var admin = await db.Admins.FirstOrDefaultAsync(a => a.Id == AdminId);
        if (admin == null) return Forbid();
        var email = form.Email?.Trim().ToLowerInvariant() ?? "";
        if (!string.IsNullOrEmpty(form.CurrentPassword) && !passwords.Verify(admin.PasswordHash, form.CurrentPassword))
            ModelState.AddModelError(nameof(form.CurrentPassword), "Mevcut şifre hatalı.");
        if (await db.Admins.AnyAsync(a => a.Email == email && a.Id != admin.Id))
            ModelState.AddModelError(nameof(form.Email), "Bu e-posta başka bir yöneticide kayıtlı.");
        if (!ModelState.IsValid)
        {
            Err(string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return Redirect("/admin/hesap");
        }
        admin.FullName = form.FullName.Trim();
        admin.Email = email;
        await db.SaveChangesAsync();
        await HttpContext.SignInAsync(AuthSchemes.Admin, AuthSchemes.AdminPrincipal(admin));
        Ok("Yönetici bilgileri güncellendi.");
        return Redirect("/admin/hesap");
    }

    /// <summary>Yönetici şifresi: en az 10 karakter, büyük/küçük harf ve rakam.</summary>
    public static string? AdminStrength(string? p)
    {
        if (string.IsNullOrEmpty(p) || p.Length < 10) return "Yönetici şifresi en az 10 karakter olmalıdır.";
        if (p.Length > 128) return "Şifre en fazla 128 karakter olabilir.";
        if (!p.Any(char.IsUpper) || !p.Any(char.IsLower) || !p.Any(char.IsDigit)) return "Şifre büyük harf, küçük harf ve rakam içermelidir.";
        return null;
    }
}

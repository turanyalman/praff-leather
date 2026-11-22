using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

/// <summary>Yöneticinin müşterilere e-posta göndermesi, gönderim kaydı ve SMTP testi.</summary>
public class AdminEmailController(AppDbContext db, EmailService email, SiteSettingsService settings, BackgroundJobQueue jobs) : AdminBaseController
{
    public const int MaxBulk = 300;

    private async Task<IActionResult> ViewAsync(ComposeEmailForm form, int sayfa = 1)
    {
        ViewData["Title"] = "E-posta";
        ViewData["Nav"] = "email";
        ViewData["Configured"] = email.IsConfigured;
        ViewData["BulkCount"] = await BulkQuery().CountAsync();
        ViewData["Customers"] = await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.CompanyName).ThenBy(u => u.FirstName)
            .Select(u => new User { Id = u.Id, FirstName = u.FirstName, LastName = u.LastName, CompanyName = u.CompanyName, Email = u.Email }).Take(1000).ToListAsync();
        const int size = 30;
        var total = await db.EmailLogs.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        sayfa = Math.Clamp(sayfa, 1, pages);
        ViewData["Log"] = new Paged<EmailLog>
        {
            Items = await db.EmailLogs.AsNoTracking().OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id).Skip((sayfa - 1) * size).Take(size).ToListAsync(),
            Page = sayfa, TotalPages = pages, Total = total
        };
        return View("Index", form);
    }

    private IQueryable<User> BulkQuery() => db.Users.Where(u => u.IsActive && u.IsApproved && u.EmailConfirmed);

    [HttpGet("/admin/eposta")]
    public async Task<IActionResult> Index(int? musteri, int sayfa = 1)
    {
        var form = new ComposeEmailForm();
        if (musteri is > 0)
        {
            var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == musteri);
            if (u != null) { form.UserId = u.Id; form.To = u.Email; form.Subject = $"{(await settings.GetAsync())[SettingKeys.SiteTitle]} — "; form.Message = $"Merhaba {u.FirstName},\n\n"; }
        }
        return await ViewAsync(form, sayfa);
    }

    [HttpPost("/admin/eposta/gonder"), EnableRateLimiting("forms")]
    public async Task<IActionResult> Send(ComposeEmailForm form)
    {
        if (form.Mode == "tek" && string.IsNullOrWhiteSpace(form.To)) ModelState.AddModelError(nameof(form.To), "Alıcı e-posta adresi gerekli.");
        if (!string.IsNullOrWhiteSpace(form.ButtonUrl) && !(Uri.TryCreate(form.ButtonUrl, UriKind.Absolute, out var bu) && (bu.Scheme == Uri.UriSchemeHttps || bu.Scheme == Uri.UriSchemeHttp)))
            ModelState.AddModelError(nameof(form.ButtonUrl), "Düğme bağlantısı http(s):// ile başlamalı.");
        if (!email.IsConfigured) ModelState.AddModelError("", "SMTP ayarları yapılmadığı için e-posta gönderilemez. appsettings.Production.json içindeki Smtp bölümünü doldurun.");
        if (!ModelState.IsValid) return await ViewAsync(form);

        var inner = EmailService.TextToHtml(form.Message);
        var buttonText = string.IsNullOrWhiteSpace(form.ButtonText) || string.IsNullOrWhiteSpace(form.ButtonUrl) ? null : form.ButtonText.Trim();
        var buttonUrl = buttonText == null ? null : form.ButtonUrl!.Trim();
        var s = await settings.GetAsync();
        var replyTo = s.Has(SettingKeys.ContactEmail) ? s.Raw(SettingKeys.ContactEmail) : null;

        if (form.Mode == "onayli")
        {
            var recipients = await BulkQuery().OrderBy(u => u.Id).Select(u => new { u.Id, u.Email, u.FirstName }).Take(MaxBulk + 1).ToListAsync();
            if (recipients.Count == 0) { Err("Onaylı ve e-postası doğrulanmış müşteri yok."); return Redirect("/admin/eposta"); }
            if (recipients.Count > MaxBulk) { Err($"Toplu gönderim en fazla {MaxBulk} alıcı ile sınırlıdır."); return Redirect("/admin/eposta"); }
            var subject = form.Subject.Trim();
            var by = AdminEmail;
            // Arka planda gönderilir (istek zaman aşımına uğramasın). Her alıcıya ayrı e-posta; alıcılar birbirini görmez.
            await jobs.EnqueueAsync(async (sp, ct) =>
            {
                var mailer = sp.GetRequiredService<EmailService>();
                foreach (var r in recipients)
                {
                    if (ct.IsCancellationRequested) break;
                    var personal = inner.Replace("{ad}", EmailService.H(r.FirstName));
                    await mailer.SendAsync(r.Email, subject, personal, EmailCategory.Manual, by, r.Id, buttonText, buttonUrl, replyTo: replyTo);
                    await Task.Delay(1500, ct); // posta sunucusunun saatlik sınırlarına takılmamak için yavaş gönderim
                }
            });
            Ok($"{recipients.Count} müşteriye e-posta gönderimi başlatıldı. Sonuçlar birkaç dakika içinde aşağıdaki gönderim kaydında görünür.");
            return Redirect("/admin/eposta");
        }

        var to = form.To!.Trim().ToLowerInvariant();
        var user = form.UserId is > 0 ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == form.UserId && u.Email == to)
                                      : await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == to);
        var body = inner.Replace("{ad}", EmailService.H(user?.FirstName ?? ""));
        var result = await email.SendAsync(to, form.Subject.Trim(), body, EmailCategory.Manual, AdminEmail, user?.Id, buttonText, buttonUrl, replyTo: replyTo);
        if (result.Success) Ok($"E-posta {to} adresine gönderildi."); else Err($"E-posta gönderilemedi: {result.Error}");
        return Redirect(user != null ? $"/admin/musteriler/{user.Id}" : "/admin/eposta");
    }

    [HttpPost("/admin/eposta/test"), EnableRateLimiting("forms")]
    public async Task<IActionResult> Test(string? to)
    {
        to = string.IsNullOrWhiteSpace(to) ? AdminEmail : to.Trim();
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(to)) { Err("Geçerli bir e-posta girin."); return Redirect("/admin/eposta"); }
        var r = await email.SendAsync(to, "PRAFF LEATHER — Test e-postası",
            "<p style=\"margin:0 0 14px\">Bu bir test e-postasıdır. Bu mesajı görüyorsanız SMTP ayarlarınız doğru çalışıyor.</p>", EmailCategory.Test, AdminEmail);
        if (r.Success) Ok($"Test e-postası {to} adresine gönderildi. Gelen kutunuzu (ve spam klasörünü) kontrol edin.");
        else Err($"Test e-postası gönderilemedi: {r.Error}");
        return Redirect("/admin/eposta");
    }

    [HttpGet("/admin/eposta/kayit/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var log = await db.EmailLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        if (log == null) return NotFound();
        ViewData["Title"] = "E-posta Kaydı";
        ViewData["Nav"] = "email";
        return View(log);
    }
}

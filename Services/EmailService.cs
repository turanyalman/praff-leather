using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;

namespace PraffLeather.Services;

public static class EmailCategory
{
    public const string Verification = "Doğrulama";
    public const string Password = "Şifre";
    public const string Order = "Sipariş";
    public const string Account = "Hesap";
    public const string AdminNotice = "Yönetici Bildirimi";
    public const string Manual = "Yönetici Mesajı";
    public const string Test = "Test";
}

/// <summary>
/// Markalı e-posta şablonları, gönderim kaydı (EmailLog), kayıt doğrulama kodları ve otomatik bildirimler.
/// Gönderim hataları isteği bozmaz; sonuç kayda yazılır.
/// </summary>
public class EmailService(AppDbContext db, IEmailSender sender, SiteSettingsService settings, IHttpContextAccessor http, IConfiguration config, ILogger<EmailService> log)
{
    public const int CodeMinutes = 15;
    public const int MaxCodeAttempts = 5;

    public bool IsConfigured => sender.IsConfigured;

    public static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Düz metni (satır sonlarıyla) güvenli HTML paragraflarına çevirir.</summary>
    public static string TextToHtml(string text) =>
        string.Join("", text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => $"<p style=\"margin:0 0 14px\">{H(p.Trim()).Replace("\n", "<br>")}</p>"));

    public async Task<string> BaseUrlAsync()
    {
        var s = await settings.GetAsync();
        var url = s.Raw(SettingKeys.SiteUrl).Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(url)) return url;
        url = config["App:BaseUrl"]?.TrimEnd('/') ?? "";
        if (!string.IsNullOrEmpty(url)) return url;
        var req = http.HttpContext?.Request;
        return req == null ? "" : $"{req.Scheme}://{req.Host}";
    }

    /// <summary>İçeriği marka şablonuna yerleştirir (e-posta istemcileri için tablo + satır içi stil).</summary>
    public async Task<string> WrapAsync(string innerHtml, string? buttonText = null, string? buttonUrl = null)
    {
        var s = await settings.GetAsync();
        var baseUrl = await BaseUrlAsync();
        var title = H(s[SettingKeys.SiteTitle]);
        var button = string.IsNullOrEmpty(buttonText) || string.IsNullOrEmpty(buttonUrl) ? "" :
            $"<p style=\"margin:24px 0 8px\"><a href=\"{H(buttonUrl)}\" style=\"display:inline-block;background:#C99A4A;color:#0A0806;text-decoration:none;font-weight:bold;padding:12px 22px;letter-spacing:1px\">{H(buttonText)}</a></p>";
        var contact = new List<string>();
        if (s.Has(SettingKeys.Phone)) contact.Add(H(s.Raw(SettingKeys.Phone)));
        if (s.Has(SettingKeys.ContactEmail)) contact.Add(H(s.Raw(SettingKeys.ContactEmail)));
        return $"""
<!DOCTYPE html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
<body style="margin:0;background:#f4efe7;font-family:Arial,Helvetica,sans-serif;color:#2a211a">
<table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4efe7;padding:24px 12px"><tr><td align="center">
<table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background:#ffffff;border:1px solid #e6dccd">
<tr><td style="background:#0A0806;padding:22px 28px;text-align:center"><span style="color:#E8C98C;font-family:Georgia,serif;font-size:22px;letter-spacing:6px">PRAFF</span><br><span style="color:#C99A4A;font-size:11px;letter-spacing:8px">LEATHER</span></td></tr>
<tr><td style="padding:28px;font-size:15px;line-height:1.6">{innerHtml}{button}</td></tr>
<tr><td style="padding:18px 28px;border-top:1px solid #eee4d6;font-size:12px;color:#8a7a68;line-height:1.5">{title}{(contact.Count > 0 ? " · " + string.Join(" · ", contact) : "")}<br><a href="{H(baseUrl)}" style="color:#8a6a2f">{H(baseUrl)}</a></td></tr>
</table></td></tr></table></body></html>
""";
    }

    /// <summary>E-postayı gönderir ve kaydını tutar. <paramref name="logBody"/> null ise gövde kaydedilir.</summary>
    public async Task<EmailSendResult> SendAsync(string to, string subject, string innerHtml, string category, string? sentBy = null, int? userId = null,
        string? buttonText = null, string? buttonUrl = null, string? logBody = null, string? replyTo = null)
    {
        to = to.Trim();
        var html = await WrapAsync(innerHtml, buttonText, buttonUrl);
        var result = await sender.SendAsync(to, subject, html, replyTo);
        try
        {
            db.EmailLogs.Add(new EmailLog
            {
                ToEmail = to.Length > 160 ? to[..160] : to,
                Subject = subject.Length > 200 ? subject[..200] : subject,
                Body = logBody ?? innerHtml,
                Category = category,
                Success = result.Success,
                Error = result.Error,
                SentBy = sentBy,
                UserId = userId
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { log.LogWarning(ex, "E-posta kaydı yazılamadı"); }
        return result;
    }

    private async Task<string> SiteTitleAsync() => (await settings.GetAsync())[SettingKeys.SiteTitle];

    // ------------------------------------------------------------------ Kayıt doğrulama kodu

    private static string CodeHash(int userId, string code) => TokenHelper.Sha256($"{userId}:{code}");

    /// <summary>Yeni 6 haneli kod üretir, öncekileri geçersiz kılar ve e-postayla gönderir.</summary>
    public async Task<EmailSendResult> SendVerificationCodeAsync(User user)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var now = DateTime.UtcNow;
        await db.EmailVerificationCodes.Where(c => c.UserId == user.Id && c.UsedAt == null).ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, now));
        db.EmailVerificationCodes.Add(new EmailVerificationCode { UserId = user.Id, CodeHash = CodeHash(user.Id, code), ExpiresAt = now.AddMinutes(CodeMinutes) });
        await db.SaveChangesAsync();
        if (!sender.IsConfigured) log.LogWarning("SMTP yok — {Email} için doğrulama kodu: {Code}", user.Email, code);

        var inner = $"""
<p style="margin:0 0 14px">Merhaba {H(user.FirstName)},</p>
<p style="margin:0 0 14px">{H(await SiteTitleAsync())} toptan müşteri başvurunuzu tamamlamak için e-posta doğrulama kodunuz:</p>
<p style="margin:20px 0;font-size:32px;letter-spacing:10px;font-weight:bold;color:#0A0806;text-align:center;background:#f4efe7;padding:16px">{code}</p>
<p style="margin:0 0 14px">Kod {CodeMinutes} dakika geçerlidir. Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz.</p>
""";
        // Kod konu satırına yazılmaz (gönderim kaydında görünmesin).
        return await SendAsync(user.Email, $"{await SiteTitleAsync()} — E-posta doğrulama kodunuz", inner, EmailCategory.Verification, "Sistem", user.Id,
            logBody: "<p>[Doğrulama kodu güvenlik nedeniyle kayda yazılmaz]</p>");
    }

    /// <summary>Son gönderimden bu yana geçen süre (tekrar gönder sınırı için).</summary>
    public async Task<TimeSpan?> SinceLastCodeAsync(int userId)
    {
        var last = await db.EmailVerificationCodes.Where(c => c.UserId == userId).OrderByDescending(c => c.CreatedAt).Select(c => (DateTime?)c.CreatedAt).FirstOrDefaultAsync();
        return last == null ? null : DateTime.UtcNow - last.Value;
    }

    public async Task<int> CodesLastHourAsync(int userId)
    {
        var since = DateTime.UtcNow.AddHours(-1);
        return await db.EmailVerificationCodes.CountAsync(c => c.UserId == userId && c.CreatedAt >= since);
    }

    /// <summary>Kodu doğrular. Başarılıysa kullanıcıyı "doğrulandı" yapar.</summary>
    public async Task<OpResult> VerifyCodeAsync(int userId, string? code)
    {
        code = new string((code ?? "").Where(char.IsAsciiDigit).ToArray());
        var now = DateTime.UtcNow;
        var row = await db.EmailVerificationCodes.Where(c => c.UserId == userId && c.UsedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();
        if (row == null) return OpResult.Fail("Kodun süresi doldu. Lütfen yeni kod isteyin.");
        if (row.Attempts >= MaxCodeAttempts) return OpResult.Fail("Çok fazla hatalı deneme. Lütfen yeni kod isteyin.");
        if (code.Length != 6 || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(row.CodeHash), Encoding.ASCII.GetBytes(CodeHash(userId, code))))
        {
            row.Attempts++;
            await db.SaveChangesAsync();
            var left = MaxCodeAttempts - row.Attempts;
            return OpResult.Fail(left > 0 ? $"Kod hatalı. Kalan deneme hakkı: {left}." : "Çok fazla hatalı deneme. Lütfen yeni kod isteyin.");
        }
        row.UsedAt = now;
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.EmailConfirmed = true;
        user.EmailConfirmedAt = now;
        await db.SaveChangesAsync();
        return OpResult.Ok("E-posta adresiniz doğrulandı.");
    }

    // ------------------------------------------------------------------ Otomatik bildirimler

    private async Task<string?> AdminInboxAsync()
    {
        var s = await settings.GetAsync();
        var to = s.Has(SettingKeys.AdminNotifyEmail) ? s.Raw(SettingKeys.AdminNotifyEmail) : s.Raw(SettingKeys.ContactEmail);
        return string.IsNullOrWhiteSpace(to) ? null : to.Trim();
    }

    public async Task NotifyAdminNewApplicationAsync(User u)
    {
        var to = await AdminInboxAsync();
        if (to == null) return;
        var baseUrl = await BaseUrlAsync();
        var inner = $"""
<p style="margin:0 0 14px"><b>Yeni toptan müşteri başvurusu</b></p>
<table style="font-size:14px;border-collapse:collapse">
<tr><td style="padding:4px 12px 4px 0;color:#8a7a68">Firma</td><td>{H(u.CompanyName)}</td></tr>
<tr><td style="padding:4px 12px 4px 0;color:#8a7a68">Yetkili</td><td>{H(u.FullName)}</td></tr>
<tr><td style="padding:4px 12px 4px 0;color:#8a7a68">E-posta</td><td>{H(u.Email)} {(u.EmailConfirmed ? "(doğrulandı)" : "(doğrulanmadı)")}</td></tr>
<tr><td style="padding:4px 12px 4px 0;color:#8a7a68">Telefon</td><td>{H(u.Phone)}</td></tr>
<tr><td style="padding:4px 12px 4px 0;color:#8a7a68">İl</td><td>{H(u.City)}</td></tr>
<tr><td style="padding:4px 12px 4px 0;color:#8a7a68">Vergi</td><td>{H(u.TaxOffice)} / {H(u.TaxNumber)}</td></tr>
</table>
""";
        await SendAsync(to, $"Yeni başvuru: {u.CompanyName ?? u.FullName}", inner, EmailCategory.AdminNotice, "Sistem", u.Id, "Başvuruyu İncele", $"{baseUrl}/admin/musteriler/{u.Id}");
    }

    public async Task NotifyApprovedAsync(User u, string? by)
    {
        var baseUrl = await BaseUrlAsync();
        var inner = $"<p style=\"margin:0 0 14px\">Merhaba {H(u.FirstName)},</p><p style=\"margin:0 0 14px\">Toptan müşteri hesabınız onaylandı. Artık ürünlerimizi ve toptan fiyatlarımızı görebilir, sipariş verebilirsiniz.</p>";
        await SendAsync(u.Email, $"{await SiteTitleAsync()} — Hesabınız onaylandı", inner, EmailCategory.Account, by ?? "Sistem", u.Id, "Koleksiyonu Görüntüle", $"{baseUrl}/urunler");
    }

    public async Task<EmailSendResult> SendPasswordResetAsync(User u, string link)
    {
        var inner = $"<p style=\"margin:0 0 14px\">Merhaba {H(u.FirstName)},</p><p style=\"margin:0 0 14px\">Şifrenizi sıfırlamak için aşağıdaki düğmeyi 1 saat içinde kullanın. Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz.</p>";
        return await SendAsync(u.Email, $"{await SiteTitleAsync()} — Şifre sıfırlama", inner, EmailCategory.Password, "Sistem", u.Id, "Yeni Şifre Belirle", link,
            logBody: "<p>[Şifre sıfırlama bağlantısı güvenlik nedeniyle kayda yazılmaz]</p>");
    }

    private static string OrderTable(Order o)
    {
        var sb = new StringBuilder("<table style=\"width:100%;font-size:14px;border-collapse:collapse;margin:8px 0 14px\">");
        foreach (var i in o.Items)
            sb.Append($"<tr><td style=\"padding:6px 0;border-bottom:1px solid #eee4d6\">{H(i.ProductName)}{(string.IsNullOrEmpty(i.ColorName) ? "" : " — " + H(i.ColorName))}<br><small style=\"color:#8a7a68\">{H(i.Sku)} · {Fmt.Number(i.Quantity)} × {Fmt.Money(i.UnitPrice)}</small></td><td style=\"padding:6px 0;border-bottom:1px solid #eee4d6;text-align:right;white-space:nowrap\">{Fmt.Money(i.LineTotal)}</td></tr>");
        sb.Append($"<tr><td style=\"padding:6px 0\">Ara toplam</td><td style=\"text-align:right\">{Fmt.Money(o.Subtotal)}</td></tr>");
        sb.Append($"<tr><td style=\"padding:6px 0\">Kargo</td><td style=\"text-align:right\">{(o.ShippingCost == 0 ? "Ücretsiz" : Fmt.Money(o.ShippingCost))}</td></tr>");
        sb.Append($"<tr><td style=\"padding:6px 0\"><b>Toplam</b></td><td style=\"text-align:right\"><b>{Fmt.Money(o.Total)}</b></td></tr></table>");
        return sb.ToString();
    }

    /// <summary>Sipariş oluşunca müşteriye özet + havale bilgisi, yöneticiye bildirim.</summary>
    public async Task NotifyOrderPlacedAsync(Order o)
    {
        var s = await settings.GetAsync();
        var baseUrl = await BaseUrlAsync();
        var bank = s.Raw(SettingKeys.BankInfo);
        var inner = $"""
<p style="margin:0 0 14px">Merhaba {H(o.FullName)},</p>
<p style="margin:0 0 14px"><b>{H(o.OrderNumber)}</b> numaralı siparişiniz alındı. Ödemeniz onaylandıktan sonra siparişiniz hazırlanacaktır.</p>
{OrderTable(o)}
{(string.IsNullOrWhiteSpace(bank) ? "" : $"<p style=\"margin:0 0 6px\"><b>Havale / EFT bilgileri</b></p><p style=\"margin:0 0 14px;background:#f4efe7;padding:12px;white-space:pre-line\">{H(bank)}</p><p style=\"margin:0 0 14px\">Açıklama kısmına sipariş numaranızı ({H(o.OrderNumber)}) yazmayı unutmayın.</p>")}
""";
        await SendAsync(o.Email, $"Siparişiniz alındı — {o.OrderNumber}", inner, EmailCategory.Order, "Sistem", o.UserId, "Siparişi Görüntüle", $"{baseUrl}/hesap/siparisler/{o.OrderNumber}");

        var admin = await AdminInboxAsync();
        if (admin != null)
            await SendAsync(admin, $"Yeni sipariş: {o.OrderNumber} — {Fmt.Money(o.Total)}",
                $"<p style=\"margin:0 0 14px\"><b>{H(o.CompanyName ?? o.FullName)}</b> yeni bir sipariş verdi.</p>{OrderTable(o)}",
                EmailCategory.AdminNotice, "Sistem", o.UserId, "Siparişi Aç", $"{baseUrl}/admin/siparisler/{o.Id}");
    }

    public async Task NotifyOrderStatusAsync(Order o, string? note)
    {
        var baseUrl = await BaseUrlAsync();
        var text = o.Status switch
        {
            OrderStatus.Confirmed => "Siparişiniz onaylandı.",
            OrderStatus.Processing => "Siparişiniz hazırlanıyor.",
            OrderStatus.Shipped => "Siparişiniz kargoya verildi.",
            OrderStatus.Delivered => "Siparişiniz teslim edildi. Bizi tercih ettiğiniz için teşekkür ederiz.",
            OrderStatus.Cancelled => "Siparişiniz iptal edildi.",
            _ => $"Sipariş durumunuz: {Fmt.StatusText(o.Status)}."
        };
        var inner = $"<p style=\"margin:0 0 14px\">Merhaba {H(o.FullName)},</p><p style=\"margin:0 0 14px\"><b>{H(o.OrderNumber)}</b> — {H(text)}</p>"
            + (string.IsNullOrWhiteSpace(note) ? "" : $"<p style=\"margin:0 0 14px;background:#f4efe7;padding:12px\">{H(note)}</p>");
        await SendAsync(o.Email, $"Sipariş {o.OrderNumber}: {Fmt.StatusText(o.Status)}", inner, EmailCategory.Order, "Sistem", o.UserId, "Siparişi Görüntüle", $"{baseUrl}/hesap/siparisler/{o.OrderNumber}");
    }

    public async Task NotifyAdminOrderCancelledByCustomerAsync(Order o)
    {
        var admin = await AdminInboxAsync();
        if (admin == null) return;
        var baseUrl = await BaseUrlAsync();
        await SendAsync(admin, $"Müşteri siparişi iptal etti: {o.OrderNumber}",
            $"<p style=\"margin:0 0 14px\"><b>{H(o.CompanyName ?? o.FullName)}</b>, {H(o.OrderNumber)} numaralı siparişini iptal etti. Stoklar otomatik iade edildi.</p>",
            EmailCategory.AdminNotice, "Sistem", o.UserId, "Siparişi Aç", $"{baseUrl}/admin/siparisler/{o.Id}");
    }
}

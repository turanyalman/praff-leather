using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PraffLeather.Services;

public record EmailSendResult(bool Success, string? Error)
{
    public static readonly EmailSendResult NotConfigured = new(false, "SMTP ayarları yapılmadı.");
}

public interface IEmailSender
{
    /// <summary>Başarılı sonuç yalnızca e-posta gerçekten SMTP sunucusuna teslim edildiyse döner.</summary>
    Task<EmailSendResult> SendAsync(string to, string subject, string htmlBody, string? replyTo = null);
    bool IsConfigured { get; }
    string? FromAddress { get; }
}

public class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    /// <summary>Eski ayar: true ise bağlantı şifrelenir (Security boşsa kullanılır).</summary>
    public bool EnableSsl { get; set; } = true;
    /// <summary>Auto | StartTls | SslOnConnect | None. 587 için StartTls, 465 için SslOnConnect.</summary>
    public string? Security { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    public string? FromName { get; set; } = "PRAFF LEATHER";
}

/// <summary>
/// MailKit ile SMTP gönderimi (Plesk / Turhost posta sunucusu, Gmail, Yandex, Brevo vb. ile çalışır).
/// SMTP ayarı yoksa e-postayı göndermez; geliştirme ortamında içeriği sunucu günlüğüne yazar.
/// </summary>
public class EmailSender(Microsoft.Extensions.Options.IOptionsMonitor<SmtpOptions> options, ILogger<EmailSender> log, IWebHostEnvironment env) : IEmailSender
{
    // IOptionsMonitor: appsettings.Production.json kaydedilince yeni SMTP ayarı yeniden başlatmadan kullanılır.
    private SmtpOptions _o => options.CurrentValue;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.Host) && !string.IsNullOrWhiteSpace(_o.From);
    public string? FromAddress => _o.From;

    public async Task<EmailSendResult> SendAsync(string to, string subject, string htmlBody, string? replyTo = null)
    {
        if (!IsConfigured)
        {
            if (env.IsDevelopment())
                log.LogWarning("SMTP yapılandırılmadı; e-posta GÖNDERİLMEDİ. Alıcı: {To} | Konu: {Subject}\n{Body}", to, subject, htmlBody);
            else
                log.LogWarning("SMTP yapılandırılmadı; e-posta gönderilemedi. Konu: {Subject}", subject);
            return EmailSendResult.NotConfigured;
        }
        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_o.FromName ?? "PRAFF LEATHER", _o.From!));
            msg.To.Add(MailboxAddress.Parse(to));
            if (!string.IsNullOrWhiteSpace(replyTo)) msg.ReplyTo.Add(MailboxAddress.Parse(replyTo));
            msg.Subject = subject;
            msg.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = HtmlToText(htmlBody) }.ToMessageBody();

            using var client = new SmtpClient { Timeout = 20_000 };
            await client.ConnectAsync(_o.Host!, _o.Port, SecurityOption());
            // Bazı posta sunucuları (ör. Turhost/SmarterMail) CRAM-MD5'i ilan eder ama doğru şifreyle bile 535 döndürür.
            // Bağlantı zaten TLS ile şifreli olduğundan PLAIN/LOGIN güvenlidir; özet tabanlı yöntemler devre dışı bırakılır.
            if (client.IsSecure)
                foreach (var mech in new[] { "CRAM-MD5", "DIGEST-MD5", "NTLM", "XOAUTH2", "OAUTHBEARER" })
                    client.AuthenticationMechanisms.Remove(mech);
            if (!string.IsNullOrEmpty(_o.User)) await client.AuthenticateAsync(_o.User, _o.Password ?? "");
            await client.SendAsync(new SendRequest(msg));
            await client.DisconnectAsync(true);
            return new EmailSendResult(true, null);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "E-posta gönderilemedi: {Subject}", subject);
            var reason = ex switch
            {
                AuthenticationException => "SMTP kullanıcı adı veya şifresi hatalı.",
                SmtpCommandException sc => $"SMTP sunucusu reddetti: {sc.Message}",
                SslHandshakeException => "SSL/TLS bağlantısı kurulamadı (port ve güvenlik ayarını kontrol edin).",
                System.Net.Sockets.SocketException => "SMTP sunucusuna bağlanılamadı (sunucu adı/port).",
                _ => ex.Message
            };
            return new EmailSendResult(false, reason.Length > 480 ? reason[..480] : reason);
        }
    }

    private SecureSocketOptions SecurityOption() => (_o.Security ?? "").Trim().ToLowerInvariant() switch
    {
        "starttls" => SecureSocketOptions.StartTls,
        "sslonconnect" or "ssl" => SecureSocketOptions.SslOnConnect,
        "none" => SecureSocketOptions.None,
        "auto" => SecureSocketOptions.Auto,
        _ => _o.EnableSsl ? (_o.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable) : SecureSocketOptions.None
    };

    private static string HtmlToText(string html)
    {
        var t = System.Text.RegularExpressions.Regex.Replace(html, "<(br|/p|/h[1-6]|/tr|/li)[^>]*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        t = System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", "");
        return System.Net.WebUtility.HtmlDecode(t).Trim();
    }
}

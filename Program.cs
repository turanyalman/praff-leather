using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Services;

try
{
var builder = WebApplication.CreateBuilder(args);
var appData = Path.Combine(builder.Environment.ContentRootPath, "App_Data");

// Paylaşımlı hostingte uygulama havuzunun yazma izni kapalı olabilir. Yazılamıyorsa
// dosya günlüğü ve kalıcı anahtarlar atlanır; site yine de açılır (uyarı günlüğe düşer).
static bool CanWrite(string dir)
{
    try
    {
        Directory.CreateDirectory(dir);
        var probe = Path.Combine(dir, $".yazma-testi-{Guid.NewGuid():N}");
        File.WriteAllText(probe, "ok");
        File.Delete(probe);
        return true;
    }
    catch { return false; }
}
var logsDir = Path.Combine(appData, "logs");
var keysDir = Path.Combine(appData, "keys");
var canWriteLogs = CanWrite(logsDir);
var canWriteKeys = CanWrite(keysDir);

// ---- Günlük: uyarı ve hatalar App_Data/logs altına da yazılır ----
if (canWriteLogs) builder.Logging.AddProvider(new FileLoggerProvider(logsDir, LogLevel.Warning));

// ---- Veritabanı: SQL Server (geliştirmede LocalDB, yayında Plesk MSSQL) ----
var cs = builder.Configuration.GetConnectionString("DefaultConnection")
         ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection tanımlı değil (appsettings.json / appsettings.Production.json).");
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cs, sql => sql.EnableRetryOnFailure()));

// ---- Oturum anahtarları kalıcı olsun: uygulama yeniden başlayınca oturumlar ve formlar bozulmasın ----
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("PraffLeather");
if (canWriteKeys) dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysDir));

// ---- Ters vekil (Cloudflare / proxy) arkasında gerçek IP ve https bilgisi ----
// IIS in-process modda gerekmez; yalnızca App:TrustForwardedHeaders=true ise açılır.
var trustForwarded = builder.Configuration.GetValue<bool>("App:TrustForwardedHeaders");
if (trustForwarded)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = 2;
        foreach (var ip in (builder.Configuration["App:KnownProxies"] ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (IPAddress.TryParse(ip, out var addr)) o.KnownProxies.Add(addr);
        if (o.KnownProxies.Count == 0) { o.KnownIPNetworks.Clear(); o.KnownProxies.Clear(); }
    });
}

// ---- Servisler ----
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<SiteSettingsService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ImageUploadService>();
builder.Services.AddScoped<BarcodeService>();
builder.Services.AddScoped<ContentPageService>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<BackgroundJobQueue>();
builder.Services.AddHostedService<BackgroundJobRunner>();

// ---- Kimlik doğrulama: müşteri ve yönetici için ayrı çerezler ----
builder.Services.AddAuthentication(AuthSchemes.Customer)
    .AddCookie(AuthSchemes.Customer, o =>
    {
        o.Cookie.Name = ".Praff.Customer";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.LoginPath = "/hesap/giris";
        o.LogoutPath = "/hesap/cikis";
        o.AccessDeniedPath = "/hesap/onay-bekleniyor";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = AuthSchemes.ValidateCustomer;
    })
    .AddCookie(AuthSchemes.Admin, o =>
    {
        o.Cookie.Name = ".Praff.Admin";
        o.Cookie.Path = "/admin";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.LoginPath = "/hesap/giris";
        o.LogoutPath = "/admin/cikis";
        o.AccessDeniedPath = "/hesap/giris";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = AuthSchemes.ValidateAdmin;
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("AdminOnly", p => p.AddAuthenticationSchemes(AuthSchemes.Admin).RequireAuthenticatedUser().RequireRole(AuthSchemes.AdminRole));
    o.AddPolicy("CustomerOnly", p => p.AddAuthenticationSchemes(AuthSchemes.Customer).RequireAuthenticatedUser().RequireRole(AuthSchemes.CustomerRole));
    o.AddPolicy(AuthSchemes.ApprovedPolicy, p => p.AddAuthenticationSchemes(AuthSchemes.Customer).RequireAuthenticatedUser()
        .RequireRole(AuthSchemes.CustomerRole).RequireClaim(AuthSchemes.ApprovedClaim, "true"));
});

// ---- Giriş denemelerinde hız sınırı ----
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
    o.AddPolicy("forms", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    // Barkod okutma çok hızlı olabilir: yönetici başına dakikada 240 istek.
    o.AddPolicy("scan", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 240, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync("Çok fazla deneme yaptınız. Lütfen birkaç dakika sonra tekrar deneyin.", ct);
    };
});

builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "RequestVerificationToken";
    o.Cookie.Name = ".Praff.Af";
    o.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddControllersWithViews(o =>
{
    // Tüm POST/PUT/DELETE isteklerinde CSRF doğrulaması.
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Türkçe karakterler HTML entity'ye çevrilmeden yazılsın (güvenlik kodlaması sürer).
builder.Services.Configure<Microsoft.Extensions.WebEncoders.WebEncoderOptions>(o =>
    o.TextEncoderSettings = new System.Text.Encodings.Web.TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All));

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 30 * 1024 * 1024);

var app = builder.Build();

if (!canWriteLogs || !canWriteKeys)
    app.Logger.LogWarning("App_Data klasörüne yazılamıyor (günlük: {Logs}, anahtarlar: {Keys}). Hosting panelinden uygulama havuzuna yazma izni verin; aksi halde oturumlar yeniden başlatmada düşer ve görsel yüklenemez.", canWriteLogs, canWriteKeys);

// Açılışta veritabanı hazırlanamazsa site çökmez; her isteğe nedeni açıklayan bir sayfa döner.
string? startupError = null;
app.Use(async (ctx, next) =>
{
    if (startupError == null) { await next(); return; }
    ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    ctx.Response.ContentType = "text/plain; charset=utf-8";
    await ctx.Response.WriteAsync("PRAFF LEATHER — site kurulumu tamamlanamadı.\n\n" + startupError +
        "\n\nNe yapmalı: appsettings.Production.json içindeki veritabanı bilgilerini (sunucu, veritabanı adı, kullanıcı, şifre) kontrol edin, sonra siteyi yeniden başlatın.");
});

if (trustForwarded) app.UseForwardedHeaders();

var tr = new CultureInfo("tr-TR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(tr),
    SupportedCultures = [tr],
    SupportedUICultures = [tr]
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/hata");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/hata/{0}");

// Güvenlik başlıkları
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    // Kamera yalnızca kendi sitemizde (Barkodla Stok İşlemleri sayfasında telefonla okutma) kullanılabilir.
    h["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=()";
    h["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data: blob:; media-src 'self' blob:; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

try
{
    await DbInitializer.InitializeAsync(app.Services, app.Configuration, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Veritabanı hazırlanamadı");
    // Şifre içermeyen kısa açıklama (SqlException mesajları şifre göstermez).
    var inner = ex.GetBaseException();
    startupError = $"Veritabanına bağlanılamadı veya tablolar oluşturulamadı.\nHata: {inner.GetType().Name}: {inner.Message}";
}

app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException) // EF araçlarının kullandığı istisna geçirilir
{
    // Acil durum modu: uygulama açılışta çökerse (ör. ayar dosyasında yazım hatası) IIS'in boş
    // 500.30 sayfası yerine nedeni gösteren basit bir sayfa sunulur. Hiçbir ayar dosyası okunmaz.
    var inner = ex.GetBaseException();
    var message = System.Text.RegularExpressions.Regex.Replace(
        $"{inner.GetType().Name}: {inner.Message}", "(?i)(password|pwd)=[^;\"\\s]*", "$1=***");
    var fallback = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { Args = args });
    fallback.WebHost.UseKestrel();
    fallback.WebHost.UseIIS();
    var errorApp = fallback.Build();
    errorApp.Run(async ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        ctx.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.Response.WriteAsync("PRAFF LEATHER — site başlatılamadı.\n\nHata: " + message +
            "\n\nEn sık nedenler: appsettings.Production.json içinde eksik/kaymış tırnak veya virgül, şifrede \" ya da \\ karakteri, yanlış veritabanı bilgisi.");
    });
    errorApp.Run();
}

public partial class Program;

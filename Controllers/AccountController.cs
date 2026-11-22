using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers;

[Route("hesap")]
public class AccountController(AppDbContext db, PasswordService passwords, CartService cart, EmailService email, SiteSettingsService settings, OrderService orders, ILogger<AccountController> log) : Controller
{
    /// <summary>Ayar açıksa ve e-posta doğrulanmamışsa doğrulama sayfasına yönlendirilir.</summary>
    private async Task<bool> NeedsVerificationAsync(User u) => !u.EmailConfirmed && (await settings.GetAsync()).Flag(SettingKeys.RequireEmailVerification);

    // Kullanıcı bulunamadığında da aynı süre harcansın diye sahte özet.
    private static readonly string DummyHash = new PasswordService().Hash("dummy-password-1");

    private int CurrentUserId => User.UserId() ?? throw new UnauthorizedAccessException();

    private string SafeReturn(string? url) => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) && !url.StartsWith("/admin") ? url : "/hesap";

    // ------------------------------------------------ Giriş / Kayıt
    [HttpGet("giris")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(SafeReturn(returnUrl));
        ViewData["Title"] = "Giriş";
        ViewData["Nav"] = "account";
        return View(new LoginForm { ReturnUrl = returnUrl });
    }

    [HttpPost("giris"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginForm form)
    {
        ViewData["Title"] = "Giriş";
        ViewData["Nav"] = "account";
        if (!ModelState.IsValid) return View(form);

        var emailNorm = form.Email.Trim().ToLowerInvariant();

        // Tek giriş sayfası: e-posta bir yöneticiye aitse ve şifre doğruysa yönetim paneline yönlendir.
        // Yönetici ve müşteri oturumları yine ayrı çerezlerde tutulur.
        var admin = await db.Admins.FirstOrDefaultAsync(a => a.Email == emailNorm);
        if (admin != null && passwords.Verify(admin.PasswordHash, form.Password))
        {
            admin.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await HttpContext.SignInAsync(AuthSchemes.Admin, AuthSchemes.AdminPrincipal(admin), new AuthenticationProperties { IsPersistent = false });
            var ret = form.ReturnUrl;
            return LocalRedirect(!string.IsNullOrEmpty(ret) && Url.IsLocalUrl(ret) && ret.StartsWith("/admin") ? ret : "/admin");
        }
        if (admin != null)
            log.LogWarning("Başarısız yönetici girişi: {Email} ({Ip})", emailNorm, HttpContext.Connection.RemoteIpAddress);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == emailNorm);
        var valid = passwords.Verify(user?.PasswordHash ?? DummyHash, form.Password) && user != null;
        if (!valid)
        {
            ModelState.AddModelError("", "E-posta veya şifre hatalı.");
            return View(form);
        }
        if (!user!.IsActive)
        {
            ModelState.AddModelError("", "Hesabınız pasif durumda. Lütfen bizimle iletişime geçin.");
            return View(form);
        }

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await cart.MergeGuestCartAsync(user.Id);
        await HttpContext.SignInAsync(AuthSchemes.Customer, AuthSchemes.CustomerPrincipal(user),
            new AuthenticationProperties { IsPersistent = form.RememberMe });
        if (await NeedsVerificationAsync(user)) return Redirect("/hesap/dogrula");
        return LocalRedirect(SafeReturn(form.ReturnUrl));
    }

    [HttpGet("kayit")]
    public IActionResult Register(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect("/hesap");
        ViewData["Title"] = "Üye Ol";
        ViewData["Nav"] = "account";
        return View(new RegisterForm { ReturnUrl = returnUrl });
    }

    [HttpPost("kayit"), EnableRateLimiting("forms")]
    public async Task<IActionResult> Register(RegisterForm form)
    {
        ViewData["Title"] = "Üye Ol";
        ViewData["Nav"] = "account";
        var pwErr = PasswordService.StrengthError(form.Password);
        if (pwErr != null) ModelState.AddModelError(nameof(form.Password), pwErr);
        var emailNorm = form.Email?.Trim().ToLowerInvariant() ?? "";
        if (ModelState.IsValid && (await db.Users.AnyAsync(u => u.Email == emailNorm) || await db.Admins.AnyAsync(a => a.Email == emailNorm)))
            ModelState.AddModelError(nameof(form.Email), "Bu e-posta adresiyle kayıtlı bir hesap zaten var.");
        if (!ModelState.IsValid) return View(form);

        var user = new User
        {
            FirstName = form.FirstName.Trim(),
            LastName = form.LastName.Trim(),
            Email = emailNorm,
            Phone = form.Phone.Trim(),
            CompanyName = form.CompanyName!.Trim(),
            TaxOffice = form.TaxOffice!.Trim(),
            TaxNumber = form.TaxNumber!.Trim(),
            City = form.City!.Trim(),
            PasswordHash = passwords.Hash(form.Password),
            LastLoginAt = DateTime.UtcNow,
            IsApproved = false // yönetici onaylayana kadar ürün ve fiyatları göremez
        };
        db.Users.Add(user);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(form.Email), "Bu e-posta adresiyle kayıtlı bir hesap zaten var.");
            return View(form);
        }

        await cart.MergeGuestCartAsync(user.Id);
        await HttpContext.SignInAsync(AuthSchemes.Customer, AuthSchemes.CustomerPrincipal(user), new AuthenticationProperties { IsPersistent = true });

        // Doğrulama kodu gönderilir; yöneticiye yeni başvuru bildirilir. E-posta hatası kaydı bozmaz.
        try { await email.SendVerificationCodeAsync(user); } catch (Exception ex) { log.LogWarning(ex, "Doğrulama kodu gönderilemedi"); }
        try { await email.NotifyAdminNewApplicationAsync(user); } catch (Exception ex) { log.LogWarning(ex, "Başvuru bildirimi gönderilemedi"); }

        TempData["Ok"] = $"Hoş geldiniz, {user.FirstName}! Başvurunuz alındı. E-posta adresinize gönderdiğimiz 6 haneli kodu girerek doğrulayın.";
        return Redirect("/hesap/dogrula");
    }

    // ------------------------------------------------ E-posta doğrulama (6 haneli kod)
    [HttpGet("dogrula"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Verify()
    {
        var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == CurrentUserId);
        if (u.EmailConfirmed) return Redirect("/hesap");
        ViewData["Title"] = "E-posta Doğrulama";
        ViewData["Nav"] = "account";
        ViewData["Email"] = u.Email;
        ViewData["EmailConfigured"] = email.IsConfigured;
        return View(new VerifyEmailForm());
    }

    [HttpPost("dogrula"), Authorize(Policy = "CustomerOnly"), EnableRateLimiting("login")]
    public async Task<IActionResult> Verify(VerifyEmailForm form)
    {
        var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == CurrentUserId);
        if (u.EmailConfirmed) return Redirect("/hesap");
        ViewData["Title"] = "E-posta Doğrulama";
        ViewData["Nav"] = "account";
        ViewData["Email"] = u.Email;
        ViewData["EmailConfigured"] = email.IsConfigured;
        if (!ModelState.IsValid) return View(new VerifyEmailForm());
        var r = await email.VerifyCodeAsync(u.Id, form.Code);
        if (!r.Success)
        {
            ModelState.AddModelError(nameof(form.Code), r.Message);
            return View(new VerifyEmailForm());
        }
        TempData["Ok"] = "E-posta adresiniz doğrulandı. Başvurunuz yönetici onayına sunuldu.";
        return Redirect("/hesap");
    }

    [HttpPost("dogrula/tekrar"), Authorize(Policy = "CustomerOnly"), EnableRateLimiting("forms")]
    public async Task<IActionResult> ResendCode()
    {
        var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == CurrentUserId);
        if (u.EmailConfirmed) return Redirect("/hesap");
        var since = await email.SinceLastCodeAsync(u.Id);
        if (since != null && since.Value < TimeSpan.FromSeconds(60))
            TempData["Err"] = $"Yeni kod istemek için {60 - (int)since.Value.TotalSeconds} saniye bekleyin.";
        else if (await email.CodesLastHourAsync(u.Id) >= 5)
            TempData["Err"] = "Bir saat içinde en fazla 5 kod gönderilebilir. Lütfen daha sonra tekrar deneyin.";
        else
        {
            var r = await email.SendVerificationCodeAsync(u);
            if (r.Success) TempData["Ok"] = $"Yeni kod {u.Email} adresine gönderildi.";
            else TempData["Err"] = "Kod gönderilemedi. Lütfen daha sonra tekrar deneyin veya bizimle iletişime geçin.";
        }
        return Redirect("/hesap/dogrula");
    }

    /// <summary>Onaylanmamış müşterinin ürün/fiyat sayfalarına erişmeye çalıştığında gördüğü sayfa.</summary>
    [HttpGet("onay-bekleniyor")]
    public IActionResult Pending()
    {
        if (User.Identity?.IsAuthenticated != true || User.Identity.AuthenticationType != AuthSchemes.Customer) return Redirect("/hesap/giris");
        if (User.IsApprovedCustomer()) return Redirect("/urunler");
        ViewData["Title"] = "Onay Bekleniyor";
        ViewData["Nav"] = "account";
        return View();
    }

    [HttpPost("cikis")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Customer);
        TempData["Ok"] = "Çıkış yaptınız.";
        return LocalRedirect("/");
    }

    // ------------------------------------------------ Şifre sıfırlama
    [HttpGet("sifremi-unuttum")]
    public IActionResult Forgot()
    {
        ViewData["Title"] = "Şifremi Unuttum";
        return View(new ForgotForm());
    }

    [HttpPost("sifremi-unuttum"), EnableRateLimiting("forms")]
    public async Task<IActionResult> Forgot(ForgotForm form)
    {
        ViewData["Title"] = "Şifremi Unuttum";
        if (!ModelState.IsValid) return View(form);
        var emailNorm = form.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == emailNorm && u.IsActive);
        if (user != null)
        {
            var token = TokenHelper.NewToken();
            db.PasswordResetTokens.Add(new PasswordResetToken { UserId = user.Id, TokenHash = TokenHelper.Sha256(token), ExpiresAt = DateTime.UtcNow.AddHours(1) });
            await db.SaveChangesAsync();
            var link = $"{await email.BaseUrlAsync()}/hesap/sifre-sifirla?token={Uri.EscapeDataString(token)}";
            if (!email.IsConfigured) log.LogWarning("SMTP yok — şifre sıfırlama bağlantısı ({Email}): {Link}", user.Email, link);
            await email.SendPasswordResetAsync(user, link);
        }
        ViewData["Sent"] = true;
        ViewData["EmailConfigured"] = email.IsConfigured;
        return View(new ForgotForm());
    }

    [HttpGet("sifre-sifirla")]
    public async Task<IActionResult> Reset(string? token)
    {
        ViewData["Title"] = "Yeni Şifre";
        ViewData["Invalid"] = string.IsNullOrEmpty(token) || await FindTokenAsync(token) == null;
        return View(new ResetForm { Token = token ?? "" });
    }

    [HttpPost("sifre-sifirla"), EnableRateLimiting("forms")]
    public async Task<IActionResult> Reset(ResetForm form)
    {
        ViewData["Title"] = "Yeni Şifre";
        var pwErr = PasswordService.StrengthError(form.Password);
        if (pwErr != null) ModelState.AddModelError(nameof(form.Password), pwErr);
        var t = await FindTokenAsync(form.Token);
        if (t == null) { ViewData["Invalid"] = true; return View(form); }
        if (!ModelState.IsValid) return View(form);

        t.UsedAt = DateTime.UtcNow;
        t.User!.PasswordHash = passwords.Hash(form.Password);
        t.User.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        TempData["Ok"] = "Şifreniz güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";
        return Redirect("/hesap/giris");
    }

    private async Task<PasswordResetToken?> FindTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 100) return null;
        var hash = TokenHelper.Sha256(token);
        var now = DateTime.UtcNow;
        return await db.PasswordResetTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now && t.User!.IsActive);
    }

    // ------------------------------------------------ Hesabım (yalnızca giriş yapmış müşteri)
    [HttpGet(""), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Index()
    {
        var id = CurrentUserId;
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == id);
        if (await NeedsVerificationAsync(user)) return Redirect("/hesap/dogrula");
        var myOrders = db.Orders.AsNoTracking().Where(o => o.UserId == id);
        var valid = myOrders.Where(o => o.Status != OrderStatus.Cancelled);
        ViewData["Title"] = "Hesabım";
        ViewData["Nav"] = "account";
        ViewData["Side"] = "home";
        return View(new AccountDashboardViewModel
        {
            User = user,
            OrderCount = await myOrders.CountAsync(),
            ItemCount = await valid.SelectMany(o => o.Items).SumAsync(i => (int?)i.Quantity) ?? 0,
            TotalSpent = await valid.SumAsync(o => (decimal?)o.Total) ?? 0,
            RecentOrders = await myOrders.OrderByDescending(o => o.CreatedAt).Take(5).ToListAsync()
        });
    }

    [HttpGet("siparisler"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Orders()
    {
        var id = CurrentUserId;
        ViewData["Title"] = "Siparişlerim";
        ViewData["Nav"] = "account";
        ViewData["Side"] = "orders";
        var list = await db.Orders.AsNoTracking().Where(o => o.UserId == id).Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt).ToListAsync();
        return View(list);
    }

    [HttpGet("siparisler/{number}"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> OrderDetail(string number)
    {
        var id = CurrentUserId;
        // IDOR koruması: sipariş yalnızca sahibine gösterilir.
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.OrderNumber == number && o.UserId == id);
        if (order == null) return NotFound();
        ViewData["Title"] = $"Sipariş {order.OrderNumber}";
        ViewData["Nav"] = "account";
        ViewData["Side"] = "orders";
        return View(order);
    }

    [HttpPost("siparisler/{number}/iptal"), Authorize(Policy = "CustomerOnly"), EnableRateLimiting("forms")]
    public async Task<IActionResult> CancelOrder(string number)
    {
        var r = await orders.CancelByCustomerAsync(CurrentUserId, number);
        TempData[r.Success ? "Ok" : "Err"] = r.Message;
        return Redirect($"/hesap/siparisler/{Uri.EscapeDataString(number)}");
    }

    [HttpGet("bilgiler"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Profile()
    {
        var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == CurrentUserId);
        ViewData["Title"] = "Hesap Bilgilerim";
        ViewData["Nav"] = "account";
        ViewData["Side"] = "profile";
        return View(new ProfileForm { FirstName = u.FirstName, LastName = u.LastName, Phone = u.Phone ?? "", CompanyName = u.CompanyName, TaxNumber = u.TaxNumber, TaxOffice = u.TaxOffice, City = u.City, Email = u.Email });
    }

    [HttpPost("bilgiler"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Profile(ProfileForm form)
    {
        var u = await db.Users.FirstAsync(x => x.Id == CurrentUserId);
        ViewData["Title"] = "Hesap Bilgilerim";
        ViewData["Nav"] = "account";
        ViewData["Side"] = "profile";
        form.Email = u.Email;
        if (!ModelState.IsValid) return View(form);
        u.FirstName = form.FirstName.Trim();
        u.LastName = form.LastName.Trim();
        u.Phone = form.Phone.Trim();
        u.CompanyName = string.IsNullOrWhiteSpace(form.CompanyName) ? null : form.CompanyName.Trim();
        u.TaxNumber = string.IsNullOrWhiteSpace(form.TaxNumber) ? null : form.TaxNumber.Trim();
        u.TaxOffice = string.IsNullOrWhiteSpace(form.TaxOffice) ? null : form.TaxOffice.Trim();
        u.City = string.IsNullOrWhiteSpace(form.City) ? null : form.City.Trim();
        await db.SaveChangesAsync();
        // Görünen adı güncel tutmak için oturumu yenile.
        await HttpContext.SignInAsync(AuthSchemes.Customer, AuthSchemes.CustomerPrincipal(u), new AuthenticationProperties { IsPersistent = true });
        TempData["Ok"] = "Bilgileriniz güncellendi.";
        return Redirect("/hesap/bilgiler");
    }

    [HttpGet("sifre"), Authorize(Policy = "CustomerOnly")]
    public IActionResult ChangePassword()
    {
        ViewData["Title"] = "Şifre Değiştir";
        ViewData["Nav"] = "account";
        ViewData["Side"] = "password";
        return View(new ChangePasswordForm());
    }

    [HttpPost("sifre"), Authorize(Policy = "CustomerOnly"), EnableRateLimiting("login")]
    public async Task<IActionResult> ChangePassword(ChangePasswordForm form)
    {
        ViewData["Title"] = "Şifre Değiştir";
        ViewData["Nav"] = "account";
        ViewData["Side"] = "password";
        var u = await db.Users.FirstAsync(x => x.Id == CurrentUserId);
        if (!string.IsNullOrEmpty(form.CurrentPassword) && !passwords.Verify(u.PasswordHash, form.CurrentPassword))
            ModelState.AddModelError(nameof(form.CurrentPassword), "Mevcut şifre hatalı.");
        var pwErr = PasswordService.StrengthError(form.NewPassword);
        if (pwErr != null) ModelState.AddModelError(nameof(form.NewPassword), pwErr);
        if (!ModelState.IsValid) return View(new ChangePasswordForm());

        u.PasswordHash = passwords.Hash(form.NewPassword);
        u.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        await HttpContext.SignInAsync(AuthSchemes.Customer, AuthSchemes.CustomerPrincipal(u), new AuthenticationProperties { IsPersistent = true });
        log.LogInformation("Müşteri {Id} şifresini değiştirdi", u.Id);
        TempData["Ok"] = "Şifreniz değiştirildi. Diğer cihazlardaki oturumlar kapatıldı.";
        return Redirect("/hesap/sifre");
    }

    // ------------------------------------------------ Adresler
    [HttpGet("adresler"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> Addresses()
    {
        ViewData["Title"] = "Adres Bilgilerim";
        ViewData["Nav"] = "account";
        ViewData["Side"] = "addresses";
        var list = await db.Addresses.AsNoTracking().Where(a => a.UserId == CurrentUserId).OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id).ToListAsync();
        return View(list);
    }

    [HttpGet("adresler/yeni"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> NewAddress()
    {
        var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == CurrentUserId);
        ViewData["Title"] = "Yeni Adres";
        ViewData["Nav"] = "account";
        ViewData["Side"] = "addresses";
        return View("AddressForm", new AddressForm { FullName = u.FullName, Phone = u.Phone ?? "" });
    }

    [HttpGet("adresler/{id:int}"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> EditAddress(int id)
    {
        var a = await db.Addresses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUserId);
        if (a == null) return NotFound();
        ViewData["Title"] = "Adresi Düzenle";
        ViewData["Nav"] = "account";
        ViewData["Side"] = "addresses";
        return View("AddressForm", new AddressForm
        {
            Id = a.Id, Title = a.Title, FullName = a.FullName, Phone = a.Phone, City = a.City, District = a.District,
            PostalCode = a.PostalCode, AddressLine = a.AddressLine, IsDefault = a.IsDefault
        });
    }

    [HttpPost("adresler/kaydet"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> SaveAddress(AddressForm form)
    {
        var uid = CurrentUserId;
        ViewData["Nav"] = "account";
        ViewData["Side"] = "addresses";
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = form.Id == 0 ? "Yeni Adres" : "Adresi Düzenle";
            return View("AddressForm", form);
        }
        Address a;
        if (form.Id == 0)
        {
            if (await db.Addresses.CountAsync(x => x.UserId == uid) >= 20)
            {
                TempData["Err"] = "En fazla 20 adres kaydedebilirsiniz.";
                return Redirect("/hesap/adresler");
            }
            a = new Address { UserId = uid };
            db.Addresses.Add(a);
        }
        else
        {
            var found = await db.Addresses.FirstOrDefaultAsync(x => x.Id == form.Id && x.UserId == uid);
            if (found == null) return NotFound();
            a = found;
        }
        a.Title = form.Title.Trim(); a.FullName = form.FullName.Trim(); a.Phone = form.Phone.Trim();
        a.City = form.City.Trim(); a.District = form.District.Trim(); a.PostalCode = form.PostalCode?.Trim();
        a.AddressLine = form.AddressLine.Trim();
        var hasDefault = await db.Addresses.AnyAsync(x => x.UserId == uid && x.IsDefault && x.Id != a.Id);
        if (form.IsDefault || !hasDefault)
        {
            await db.Addresses.Where(x => x.UserId == uid && x.Id != a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false));
            a.IsDefault = true;
        }
        await db.SaveChangesAsync();
        TempData["Ok"] = "Adres kaydedildi.";
        return Redirect("/hesap/adresler");
    }

    [HttpPost("adresler/{id:int}/sil"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        var uid = CurrentUserId;
        var a = await db.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        if (a == null) return NotFound();
        db.Addresses.Remove(a);
        await db.SaveChangesAsync();
        if (a.IsDefault)
        {
            var next = await db.Addresses.Where(x => x.UserId == uid).OrderBy(x => x.Id).FirstOrDefaultAsync();
            if (next != null) { next.IsDefault = true; await db.SaveChangesAsync(); }
        }
        TempData["Ok"] = "Adres silindi.";
        return Redirect("/hesap/adresler");
    }

    [HttpPost("adresler/{id:int}/varsayilan"), Authorize(Policy = "CustomerOnly")]
    public async Task<IActionResult> DefaultAddress(int id)
    {
        var uid = CurrentUserId;
        if (!await db.Addresses.AnyAsync(x => x.Id == id && x.UserId == uid)) return NotFound();
        await db.Addresses.Where(x => x.UserId == uid).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, x => x.Id == id));
        TempData["Ok"] = "Varsayılan adres güncellendi.";
        return Redirect("/hesap/adresler");
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;

namespace PraffLeather.Services;

/// <summary>
/// Müşteri ve yönetici oturumları tamamen ayrı çerezlerle (ayrı şemalarla) tutulur.
/// Bir müşteri çerezi hiçbir koşulda yönetici yetkisi taşımaz.
/// </summary>
public static class AuthSchemes
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";
    public const string AdminRole = "Admin";
    public const string CustomerRole = "Customer";
    public const string StampClaim = "stamp";
    public const string ApprovedClaim = "approved";
    /// <summary>Ürün, fiyat, sepet ve sipariş yalnızca bu politikayı geçen (onaylı) müşterilere açıktır.</summary>
    public const string ApprovedPolicy = "ApprovedCustomer";

    public static ClaimsPrincipal CustomerPrincipal(Models.User u) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
        new Claim(ClaimTypes.Name, u.FullName),
        new Claim(ClaimTypes.Email, u.Email),
        new Claim(ClaimTypes.Role, CustomerRole),
        new Claim(StampClaim, u.SecurityStamp),
        new Claim(ApprovedClaim, u.IsApproved ? "true" : "false")
    ], Customer));

    /// <summary>Görünümler için: bu istekteki kullanıcı onaylı toptan müşteri mi?</summary>
    public static bool IsApprovedCustomer(this ClaimsPrincipal p) =>
        p.Identity?.IsAuthenticated == true && p.Identity.AuthenticationType == Customer && p.FindFirstValue(ApprovedClaim) == "true";

    public static ClaimsPrincipal AdminPrincipal(Models.Admin a) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, a.Id.ToString()),
        new Claim(ClaimTypes.Name, a.FullName),
        new Claim(ClaimTypes.Email, a.Email),
        new Claim(ClaimTypes.Role, AdminRole),
        new Claim(StampClaim, a.SecurityStamp)
    ], Admin));

    /// <summary>Her istekte: hesap pasifleştirildiyse veya şifre değiştiyse oturumu düşürür.</summary>
    public static async Task ValidateCustomer(CookieValidatePrincipalContext ctx)
    {
        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var id = int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var i) ? i : 0;
        var stamp = ctx.Principal?.FindFirstValue(StampClaim);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.IsActive && u.SecurityStamp == stamp);
        if (user == null)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(Customer);
            return;
        }
        // Yönetici onayı verdi/geri aldıysa ya da bilgileri değiştirdiyse oturumu yeniden giriş gerektirmeden güncelle.
        var approvedNow = user.IsApproved ? "true" : "false";
        var principal = ctx.Principal!;
        if (principal.FindFirstValue(ApprovedClaim) != approvedNow || principal.FindFirstValue(ClaimTypes.Name) != user.FullName)
        {
            ctx.ReplacePrincipal(CustomerPrincipal(user));
            ctx.ShouldRenew = true;
        }
    }

    public static async Task ValidateAdmin(CookieValidatePrincipalContext ctx)
    {
        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var id = int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var i) ? i : 0;
        var stamp = ctx.Principal?.FindFirstValue(StampClaim);
        var ok = await db.Admins.AnyAsync(a => a.Id == id && a.SecurityStamp == stamp);
        if (!ok)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(Admin);
        }
    }

    public static int? UserId(this ClaimsPrincipal p) =>
        int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

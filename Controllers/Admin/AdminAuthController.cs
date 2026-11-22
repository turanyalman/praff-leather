using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
public abstract class AdminBaseController : Controller
{
    protected string AdminEmail => User.FindFirstValue(ClaimTypes.Email) ?? "admin";
    protected int AdminId => User.UserId() ?? 0;
    protected void Ok(string m) => TempData["Ok"] = m;
    protected void Err(string m) => TempData["Err"] = m;
}

/// <summary>Yönetici girişi artık ortak giriş sayfasında (/hesap/giris) yapılır; eski adres oraya yönlendirir.</summary>
public class AdminAuthController : Controller
{
    [HttpGet("/admin/giris"), AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl)
    {
        var r = await HttpContext.AuthenticateAsync(AuthSchemes.Admin);
        if (r.Succeeded) return Redirect("/admin");
        var ret = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith("/admin") ? returnUrl : "/admin";
        return Redirect("/hesap/giris?ReturnUrl=" + Uri.EscapeDataString(ret));
    }

    [HttpPost("/admin/cikis"), Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Admin);
        TempData["Ok"] = "Yönetim panelinden çıkış yaptınız.";
        return Redirect("/hesap/giris");
    }
}
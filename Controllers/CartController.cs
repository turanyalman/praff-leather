using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PraffLeather.Services;

namespace PraffLeather.Controllers;

[Authorize(Policy = AuthSchemes.ApprovedPolicy)]
public class CartController(CartService cart, SiteSettingsService settings) : Controller
{
    private bool IsAjax => Request.Headers.XRequestedWith == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json");

    [HttpGet("/sepet")]
    public async Task<IActionResult> Index()
    {
        ViewData["Nav"] = "cart";
        ViewData["Title"] = "Toptan Sepetim";
        var view = await cart.GetAsync();
        var s = await settings.GetAsync();
        ViewData["Shipping"] = SiteSettingsService.ShippingFor(s, view.Subtotal);
        return View(view);
    }

    [HttpGet("/sepet/adet")]
    public async Task<IActionResult> Count() => Json(new { count = await cart.CountAsync() });

    [HttpPost("/sepet/ekle")]
    public async Task<IActionResult> Add(int productId, string? color, int quantity, string? returnUrl)
    {
        var r = await cart.AddAsync(productId, color, quantity);
        return await Respond(r, returnUrl);
    }

    [HttpPost("/sepet/guncelle")]
    public async Task<IActionResult> Update(int itemId, int quantity)
    {
        var r = await cart.UpdateAsync(itemId, quantity);
        return await Respond(r, "/sepet");
    }

    [HttpPost("/sepet/sil")]
    public async Task<IActionResult> Remove(int itemId)
    {
        var r = await cart.RemoveAsync(itemId);
        return await Respond(r, "/sepet");
    }

    [HttpPost("/sepet/temizle")]
    public async Task<IActionResult> Clear()
    {
        await cart.ClearAsync();
        return await Respond(OpResult.Ok("Sepet temizlendi."), "/sepet");
    }

    private async Task<IActionResult> Respond(OpResult r, string? returnUrl)
    {
        if (IsAjax)
        {
            var count = await cart.CountAsync();
            return r.Success ? Json(new { ok = true, message = r.Message, count }) : BadRequest(new { ok = false, message = r.Message, count });
        }
        TempData[r.Success ? "Ok" : "Err"] = r.Message;
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/sepet");
    }
}

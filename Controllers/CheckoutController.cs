using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers;

[Authorize(Policy = AuthSchemes.ApprovedPolicy)]
public class CheckoutController(AppDbContext db, CartService cart, OrderService orders, SiteSettingsService settings) : Controller
{
    private int UserId => User.UserId() ?? throw new UnauthorizedAccessException();

    [HttpGet("/odeme")]
    public async Task<IActionResult> Index()
    {
        var view = await cart.GetAsync();
        if (view.IsEmpty) { TempData["Err"] = "Sepetiniz boş."; return Redirect("/sepet"); }
        if (view.HasProblems) { TempData["Err"] = "Sepetinizde düzeltilmesi gereken satırlar var."; return Redirect("/sepet"); }

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == UserId);
        var addresses = await db.Addresses.AsNoTracking().Where(a => a.UserId == UserId).OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id).ToListAsync();
        var def = addresses.FirstOrDefault();
        var form = new CheckoutForm
        {
            FullName = def?.FullName ?? user.FullName,
            Email = user.Email,
            Phone = def?.Phone ?? user.Phone ?? "",
            CompanyName = user.CompanyName,
            TaxNumber = user.TaxNumber,
            AddressLine = def?.AddressLine ?? "",
            City = def?.City ?? "",
            District = def?.District ?? "",
            PostalCode = def?.PostalCode,
            AddressId = def?.Id,
            SaveAddress = addresses.Count == 0,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };
        return View(await BuildAsync(form, view, addresses));
    }

    private async Task<CheckoutViewModel> BuildAsync(CheckoutForm form, CartView view, List<Address>? addresses = null)
    {
        var s = await settings.GetAsync();
        ViewData["Title"] = "Siparişi Tamamla";
        ViewData["Nav"] = "cart";
        return new CheckoutViewModel
        {
            Form = form,
            Cart = view,
            Shipping = SiteSettingsService.ShippingFor(s, view.Subtotal),
            Addresses = addresses ?? await db.Addresses.AsNoTracking().Where(a => a.UserId == UserId).OrderByDescending(a => a.IsDefault).ToListAsync(),
            BankInfo = s.Raw(SettingKeys.BankInfo)
        };
    }

    [HttpPost("/odeme")]
    public async Task<IActionResult> Place([Bind(Prefix = "Form")] CheckoutForm form)
    {
        var view = await cart.GetAsync();
        if (view.IsEmpty)
        {
            // Çift tıklamada ilk istek sepeti boşaltmış olabilir: oluşan siparişe yönlendir.
            var done = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.IdempotencyKey == form.IdempotencyKey && o.UserId == UserId);
            if (done != null) return Redirect($"/siparis/{done.OrderNumber}/tamamlandi");
            TempData["Err"] = "Sepetiniz boş.";
            return Redirect("/sepet");
        }
        if (!ModelState.IsValid) return View("Index", await BuildAsync(form, view));

        var cartId = await cart.CartIdAsync();
        var result = await orders.PlaceOrderAsync(UserId, cartId!.Value, new CheckoutData
        {
            FullName = form.FullName, Email = form.Email, Phone = form.Phone, CompanyName = form.CompanyName, TaxNumber = form.TaxNumber,
            AddressLine = form.AddressLine, City = form.City, District = form.District, PostalCode = form.PostalCode, Notes = form.Notes
        }, form.IdempotencyKey);

        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message);
            form.IdempotencyKey = Guid.NewGuid().ToString("N");
            return View("Index", await BuildAsync(form, await cart.GetAsync()));
        }

        if (form.SaveAddress && !await db.Addresses.AnyAsync(a => a.UserId == UserId && a.AddressLine == form.AddressLine.Trim() && a.City == form.City.Trim()))
        {
            var first = !await db.Addresses.AnyAsync(a => a.UserId == UserId);
            db.Addresses.Add(new Address
            {
                UserId = UserId, Title = first ? "Teslimat Adresi" : $"Adres {DateTime.UtcNow:dd.MM.yyyy}", FullName = form.FullName.Trim(), Phone = form.Phone.Trim(),
                City = form.City.Trim(), District = form.District.Trim(), PostalCode = form.PostalCode?.Trim(), AddressLine = form.AddressLine.Trim(), IsDefault = first
            });
            await db.SaveChangesAsync();
        }
        return Redirect($"/siparis/{result.Order!.OrderNumber}/tamamlandi");
    }

    [HttpGet("/siparis/{number}/tamamlandi")]
    public async Task<IActionResult> Success(string number)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.OrderNumber == number && o.UserId == UserId);
        if (order == null) return NotFound();
        var s = await settings.GetAsync();
        ViewData["Title"] = "Siparişiniz Alındı";
        ViewData["BankInfo"] = s.Raw(SettingKeys.BankInfo);
        return View(order);
    }
}

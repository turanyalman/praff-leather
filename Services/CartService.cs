using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;

namespace PraffLeather.Services;

public record OpResult(bool Success, string Message)
{
    public static OpResult Ok(string m) => new(true, m);
    public static OpResult Fail(string m) => new(false, m);
}

public class CartLine
{
    public int ItemId { get; init; }
    public int ProductId { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
    public string Sku { get; init; } = "";
    public string ColorName { get; init; } = "";
    public string? ImageUrl { get; init; }
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; }
    public int Stock { get; init; }
    public int MinOrder { get; init; }
    public bool Available { get; init; }
    public decimal LineTotal => UnitPrice * Quantity;
    public string? Problem { get; set; }
}

public class CartView
{
    public List<CartLine> Lines { get; init; } = new();
    public decimal Subtotal => Lines.Where(l => l.Available).Sum(l => l.LineTotal);
    public int TotalQuantity => Lines.Sum(l => l.Quantity);
    public int LineCount => Lines.Count;
    public bool IsEmpty => Lines.Count == 0;
    public bool HasProblems => Lines.Any(l => l.Problem != null);
}

/// <summary>
/// Sepet veritabanında tutulur. Misafirler çerezdeki rastgele anahtarla, üyeler kullanıcı kimliğiyle eşleşir.
/// Tüm fiyat, stok ve minimum adet kontrolleri burada (sunucuda) yapılır.
/// </summary>
public class CartService(AppDbContext db, IHttpContextAccessor http)
{
    public const string CookieName = ".Praff.Cart";
    private const int MaxLineQuantity = 100_000;

    private HttpContext Ctx => http.HttpContext ?? throw new InvalidOperationException("HttpContext yok");

    public int? CurrentUserId
    {
        get
        {
            var user = Ctx.User;
            if (user.Identity?.IsAuthenticated != true || user.Identity.AuthenticationType != AuthSchemes.Customer) return null;
            return int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        }
    }

    // Aynı istekte yeni oluşturulan sepet (çerezi henüz istekte yok).
    private Cart? _resolved;

    private async Task<Cart?> FindCartAsync(bool create)
    {
        if (_resolved != null) return _resolved;
        var cart = await ResolveAsync(create);
        if (cart != null) _resolved = cart;
        return cart;
    }

    private async Task<Cart?> ResolveAsync(bool create)
    {
        var userId = CurrentUserId;
        Cart? cart;
        if (userId != null)
        {
            cart = await db.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
            if (cart == null && create)
            {
                cart = new Cart { UserId = userId };
                db.Carts.Add(cart);
                await db.SaveChangesAsync();
            }
            return cart;
        }

        var token = Ctx.Request.Cookies[CookieName];
        cart = string.IsNullOrEmpty(token) ? null : await db.Carts.FirstOrDefaultAsync(c => c.Token == token && c.UserId == null);
        if (cart == null && create)
        {
            cart = new Cart();
            db.Carts.Add(cart);
            await db.SaveChangesAsync();
            Ctx.Response.Cookies.Append(CookieName, cart.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = Ctx.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        }
        return cart;
    }

    public async Task<int?> CartIdAsync() => (await FindCartAsync(false))?.Id;

    public async Task<int> CountAsync()
    {
        var cart = await FindCartAsync(false);
        return cart == null ? 0 : await db.CartItems.CountAsync(i => i.CartId == cart.Id);
    }

    public async Task<CartView> GetAsync()
    {
        var cart = await FindCartAsync(false);
        if (cart == null) return new CartView();
        var items = await db.CartItems.AsNoTracking()
            .Where(i => i.CartId == cart.Id)
            .Include(i => i.Product!).ThenInclude(p => p.Images)
            .Include(i => i.Product!).ThenInclude(p => p.Category)
            .Include(i => i.Product!).ThenInclude(p => p.Colors)
            .OrderBy(i => i.AddedAt).ThenBy(i => i.Id)
            .AsSplitQuery()
            .ToListAsync();

        var view = new CartView
        {
            Lines = items.Select(i =>
            {
                var variant = i.Product!.Colors.FirstOrDefault(c => c.Name == i.ColorName);
                return new CartLine
                {
                    ItemId = i.Id,
                    ProductId = i.ProductId,
                    Name = i.Product.Name,
                    Slug = i.Product.Slug,
                    Sku = i.Product.Sku,
                    ColorName = i.ColorName,
                    ImageUrl = i.Product.MainImageUrl,
                    UnitPrice = i.Product.Price,
                    Quantity = i.Quantity,
                    // Renkli ürünlerde stok renk bazındadır; renk silinmişse 0 kabul edilir.
                    Stock = i.Product.Colors.Count == 0 ? i.Product.StockQuantity : variant?.StockQuantity ?? 0,
                    MinOrder = i.Product.MinOrderQuantity,
                    Available = i.Product.IsActive && i.Product.Category!.IsActive && (i.Product.Colors.Count == 0 || variant != null)
                };
            }).ToList()
        };
        Annotate(view);
        return view;
    }

    /// <summary>Her satıra stok (satır/renk bazında) ve minimum sipariş (ürün bazında) sorunlarını yazar.</summary>
    public static void Annotate(CartView view)
    {
        foreach (var group in view.Lines.GroupBy(l => l.ProductId))
        {
            var total = group.Sum(l => l.Quantity);
            foreach (var l in group)
            {
                string? problem = null;
                if (!l.Available) problem = "Bu ürün/renk artık satışta değil. Lütfen sepetten çıkarın.";
                else if (l.Stock <= 0) problem = "Bu ürün stokta kalmadı.";
                else if (l.Quantity > l.Stock) problem = $"Stokta yalnızca {Fmt.Number(l.Stock)} adet var.";
                else if (total < l.MinOrder) problem = $"Bu ürün için minimum sipariş {Fmt.Number(l.MinOrder)} adettir (renkler toplamı).";
                l.Problem = problem;
            }
        }
    }

    /// <summary><paramref name="lineQuantity"/>: bu renk satırının yeni adedi; <paramref name="productTotal"/>: ürünün tüm renkleri toplamı.</summary>
    private static string? CheckProduct(Product p, ProductColor? variant, int lineQuantity, int productTotal)
    {
        if (!p.IsActive || p.Category is { IsActive: false }) return "Bu ürün şu anda satışta değil.";
        var stock = variant?.StockQuantity ?? p.StockQuantity;
        var label = variant == null ? "" : $" ({variant.Name})";
        if (stock <= 0) return $"Bu ürün{label} stokta yok.";
        if (lineQuantity > stock) return $"Stok yetersiz{label}. En fazla {Fmt.Number(stock)} adet sipariş verebilirsiniz.";
        if (productTotal < p.MinOrderQuantity) return $"Minimum sipariş miktarı {Fmt.Number(p.MinOrderQuantity)} adettir (renkler toplamı).";
        return null;
    }

    public async Task<OpResult> AddAsync(int productId, string? color, int quantity)
    {
        if (quantity <= 0 || quantity > MaxLineQuantity) return OpResult.Fail("Geçersiz adet.");
        var product = await db.Products.Include(p => p.Colors).Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null) return OpResult.Fail("Ürün bulunamadı.");

        color = (color ?? "").Trim();
        ProductColor? variant = null;
        if (product.Colors.Count > 0)
        {
            variant = product.Colors.FirstOrDefault(c => string.Equals(c.Name, color, StringComparison.OrdinalIgnoreCase));
            if (variant == null) return OpResult.Fail("Lütfen geçerli bir renk seçin.");
            color = variant.Name;
        }
        else color = "";

        var cart = await FindCartAsync(true);
        var existingForProduct = await db.CartItems.Where(i => i.CartId == cart!.Id && i.ProductId == productId).ToListAsync();
        var newTotal = existingForProduct.Sum(i => i.Quantity) + quantity;
        var line = existingForProduct.FirstOrDefault(i => i.ColorName == color);
        var error = CheckProduct(product, variant, (line?.Quantity ?? 0) + quantity, newTotal);
        if (error != null) return OpResult.Fail(error);

        if (line != null) line.Quantity += quantity;
        else db.CartItems.Add(new CartItem { CartId = cart!.Id, ProductId = productId, ColorName = color, Quantity = quantity });
        cart!.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return OpResult.Ok(line != null
            ? $"{product.Name} sepetteki adedi güncellendi ({Fmt.Number(line.Quantity)} adet)."
            : $"{product.Name} sepete eklendi ({Fmt.Number(quantity)} adet).");
    }

    public async Task<OpResult> UpdateAsync(int itemId, int quantity)
    {
        if (quantity <= 0 || quantity > MaxLineQuantity) return OpResult.Fail("Geçersiz adet.");
        var cart = await FindCartAsync(false);
        if (cart == null) return OpResult.Fail("Sepet bulunamadı.");
        var item = await db.CartItems.FirstOrDefaultAsync(i => i.Id == itemId && i.CartId == cart.Id);
        if (item == null) return OpResult.Fail("Sepet satırı bulunamadı.");
        var product = await db.Products.Include(p => p.Category).Include(p => p.Colors).FirstAsync(p => p.Id == item.ProductId);
        var others = await db.CartItems.Where(i => i.CartId == cart.Id && i.ProductId == item.ProductId && i.Id != item.Id).SumAsync(i => i.Quantity);
        var variant = product.Colors.FirstOrDefault(c => c.Name == item.ColorName);
        if (product.Colors.Count > 0 && variant == null) return OpResult.Fail("Bu renk artık mevcut değil. Lütfen satırı sepetten çıkarın.");
        var error = CheckProduct(product, variant, quantity, others + quantity);
        if (error != null) return OpResult.Fail(error);
        item.Quantity = quantity;
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return OpResult.Ok("Adet güncellendi.");
    }

    public async Task<OpResult> RemoveAsync(int itemId)
    {
        var cart = await FindCartAsync(false);
        if (cart == null) return OpResult.Fail("Sepet bulunamadı.");
        var deleted = await db.CartItems.Where(i => i.Id == itemId && i.CartId == cart.Id).ExecuteDeleteAsync();
        return deleted > 0 ? OpResult.Ok("Ürün sepetten çıkarıldı.") : OpResult.Fail("Sepet satırı bulunamadı.");
    }

    public async Task ClearAsync()
    {
        var cart = await FindCartAsync(false);
        if (cart != null) await db.CartItems.Where(i => i.CartId == cart.Id).ExecuteDeleteAsync();
    }

    /// <summary>Girişten sonra misafir sepetini üyenin sepetine taşır; aynı ürün+renk satırlarını birleştirir.</summary>
    public async Task MergeGuestCartAsync(int userId)
    {
        var token = Ctx.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(token)) return;
        var guest = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.Token == token && c.UserId == null);
        Ctx.Response.Cookies.Delete(CookieName);
        if (guest == null) return;

        var userCart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
        if (userCart == null)
        {
            guest.UserId = userId;
            guest.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            foreach (var gi in guest.Items)
            {
                var existing = userCart.Items.FirstOrDefault(i => i.ProductId == gi.ProductId && i.ColorName == gi.ColorName);
                if (existing != null) existing.Quantity += gi.Quantity;
                else userCart.Items.Add(new CartItem { ProductId = gi.ProductId, ColorName = gi.ColorName, Quantity = gi.Quantity });
            }
            userCart.UpdatedAt = DateTime.UtcNow;
            db.Carts.Remove(guest);
        }
        await db.SaveChangesAsync();
    }
}

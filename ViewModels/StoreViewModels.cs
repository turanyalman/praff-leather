using System.ComponentModel.DataAnnotations;
using PraffLeather.Models;
using PraffLeather.Services;

namespace PraffLeather.ViewModels;

public class CategoryTile
{
    public Category Category { get; init; } = null!;
    public int ProductCount { get; init; }
    public string? ImageUrl { get; init; }
}

public class HomeViewModel
{
    public SiteSettings Settings { get; init; } = null!;
    public List<Product> Featured { get; init; } = new();
    public List<CategoryTile> Categories { get; init; } = new();
    public int TotalProducts { get; init; }
    public ContactForm Contact { get; init; } = new();
}

public class ContactForm
{
    [Required(ErrorMessage = "Ad soyad gerekli."), StringLength(120)]
    public string FullName { get; set; } = "";
    [Required(ErrorMessage = "E-posta gerekli."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(160)]
    public string Email { get; set; } = "";
    [StringLength(30)]
    public string? Phone { get; set; }
    [Required(ErrorMessage = "Konu seçin."), StringLength(60)]
    public string Subject { get; set; } = "";
    [Required(ErrorMessage = "Mesajınızı yazın."), StringLength(3000, MinimumLength = 10, ErrorMessage = "Mesaj en az 10 karakter olmalı.")]
    public string Message { get; set; } = "";
    /// <summary>Bot tuzağı: gerçek kullanıcılar bu alanı görmez.</summary>
    public string? Website { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "KVKK Aydınlatma Metni'ni okuduğunuzu onaylamanız gerekir.")]
    public bool AcceptKvkk { get; set; }

    public static readonly string[] Subjects = ["Toptan Sipariş", "İş Birliği", "Ürün Bilgisi", "Diğer"];
}

public class CatalogViewModel
{
    public List<Product> Products { get; init; } = new();
    public List<CategoryTile> Categories { get; init; } = new();
    public int AllCount { get; init; }
    public string? Query { get; init; }
    public string? CategorySlug { get; init; }
    public string Sort { get; init; } = "yeni";
    public bool InStockOnly { get; init; }
    public int Page { get; init; } = 1;
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public int LowStock { get; init; }

    public string Url(int? page = null, string? category = "__keep", string? sort = null)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(Query)) q.Add("q=" + Uri.EscapeDataString(Query));
        var cat = category == "__keep" ? CategorySlug : category;
        if (!string.IsNullOrWhiteSpace(cat)) q.Add("kategori=" + Uri.EscapeDataString(cat));
        var srt = sort ?? Sort;
        if (srt != "yeni") q.Add("siralama=" + srt);
        if (InStockOnly) q.Add("stok=1");
        var p = page ?? 1;
        if (p > 1) q.Add("sayfa=" + p);
        return "/urunler" + (q.Count > 0 ? "?" + string.Join("&", q) : "");
    }
}

public class ProductDetailViewModel
{
    public Product Product { get; init; } = null!;
    public List<Product> Related { get; init; } = new();
    public int InCartQuantity { get; init; }
    public Dictionary<string, int> InCartByColor { get; init; } = new();
    public int LowStock { get; init; }
}

public class CheckoutForm
{
    [Required(ErrorMessage = "Ad soyad gerekli."), StringLength(120)]
    public string FullName { get; set; } = "";
    [Required(ErrorMessage = "E-posta gerekli."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(160)]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Telefon gerekli."), RegularExpression(@"^[0-9+()\s-]{10,20}$", ErrorMessage = "Geçerli bir telefon numarası girin."), StringLength(30)]
    public string Phone { get; set; } = "";
    [StringLength(160)] public string? CompanyName { get; set; }
    [StringLength(30)] public string? TaxNumber { get; set; }
    [Required(ErrorMessage = "Teslimat adresi gerekli."), StringLength(400, MinimumLength = 10, ErrorMessage = "Adres en az 10 karakter olmalı.")]
    public string AddressLine { get; set; } = "";
    [Required(ErrorMessage = "İl gerekli."), StringLength(60)] public string City { get; set; } = "";
    [Required(ErrorMessage = "İlçe gerekli."), StringLength(60)] public string District { get; set; } = "";
    [RegularExpression(@"^\d{5}$", ErrorMessage = "Posta kodu 5 haneli olmalı.")]
    public string? PostalCode { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public bool SaveAddress { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Satış koşullarını onaylamanız gerekir.")]
    public bool AcceptTerms { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "KVKK Aydınlatma Metni'ni okuduğunuzu onaylamanız gerekir.")]
    public bool AcceptKvkk { get; set; }
    [Required] public string IdempotencyKey { get; set; } = "";
    public int? AddressId { get; set; }
}

public class CheckoutViewModel
{
    public CheckoutForm Form { get; set; } = new();
    public CartView Cart { get; set; } = new();
    public decimal Shipping { get; set; }
    public decimal Total => Cart.Subtotal + Shipping;
    public List<Address> Addresses { get; set; } = new();
    public string BankInfo { get; set; } = "";
}

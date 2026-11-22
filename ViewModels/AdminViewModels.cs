using System.ComponentModel.DataAnnotations;
using PraffLeather.Models;

namespace PraffLeather.ViewModels;

public class DashboardViewModel
{
    public decimal TotalSales { get; init; }
    public decimal PaidSales { get; init; }
    public int TotalOrders { get; init; }
    public int PendingOrders { get; init; }
    public int TotalCustomers { get; init; }
    public int TotalProducts { get; init; }
    public int ActiveProducts { get; init; }
    public int UnreadMessages { get; init; }
    public int LowStockThreshold { get; init; }
    public List<Product> LowStock { get; init; } = new();
    /// <summary>Kritik stok seviyesinin altındaki ürünler/renkler.</summary>
    public List<(Product Product, string? Variant, int Stock)> Critical { get; init; } = new();
    public int UnverifiedEmails { get; init; }
    public List<Order> RecentOrders { get; init; } = new();
    public List<(DateTime Day, decimal Total, int Count)> Daily { get; init; } = new();

    public (decimal Total, int Count) Today { get; init; }
    public (decimal Total, int Count) Week { get; init; }
    public (decimal Total, int Count) Month { get; init; }
    public (decimal Total, int Count) PrevMonth { get; init; }
    public decimal AverageOrder { get; init; }
    public (decimal Total, int Count) AwaitingPayment { get; init; }
    public Dictionary<OrderStatus, int> StatusCounts { get; init; } = new();
    public int PendingApprovals { get; init; }
    public List<User> PendingCustomers { get; init; } = new();
    public int NewCustomersMonth { get; init; }
    public int ApprovedCustomers { get; init; }
    public List<(string Name, string Sku, int Qty, decimal Revenue)> TopProducts { get; init; } = new();
    public decimal InventoryValue { get; init; }
    public int TotalStockUnits { get; init; }
    public int OutOfStock { get; init; }
    public List<ContactMessage> RecentMessages { get; init; } = new();
    public List<Note> PinnedNotes { get; init; } = new();
}

public class ProductForm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Ürün adı gerekli."), StringLength(160)] public string Name { get; set; } = "";
    [StringLength(180), RegularExpression("^[a-z0-9-]*$", ErrorMessage = "Adres yalnızca küçük harf, rakam ve tire içerebilir.")]
    public string? Slug { get; set; }
    [Required(ErrorMessage = "Ürün kodu gerekli."), StringLength(40), RegularExpression("^[A-Za-z0-9._-]+$", ErrorMessage = "Ürün kodu yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.")]
    public string Sku { get; set; } = "";
    [StringLength(80)] public string? Subtitle { get; set; }
    [StringLength(500)] public string? ShortDescription { get; set; }
    [StringLength(8000)] public string? Description { get; set; }
    [StringLength(4000)] public string? Features { get; set; }
    [StringLength(4000)] public string? Details { get; set; }
    /// <summary>Metin olarak alınır; "1250,50" ve "1250.50" kabul edilir (kültürden bağımsız).</summary>
    [Required(ErrorMessage = "Fiyat gerekli.")]
    public string? Price { get; set; }

    public static decimal? ParsePrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim().Replace("₺", "").Replace(" ", "");
        if (t.Contains(',')) t = t.Replace(".", "").Replace(',', '.');
        return decimal.TryParse(t, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
    }
    [Range(0, 10_000_000, ErrorMessage = "Stok negatif olamaz.")] public int StockQuantity { get; set; }
    public int? OriginalStock { get; set; }
    [StringLength(48)] public string? Barcode { get; set; }
    [Range(0, 1_000_000, ErrorMessage = "Kritik stok seviyesi negatif olamaz.")] public int CriticalStockLevel { get; set; } = 5;
    [Required, Range(1, 100_000, ErrorMessage = "Minimum sipariş en az 1 olmalı.")] public int MinOrderQuantity { get; set; } = 1;
    [Range(1, 100_000, ErrorMessage = "Koli içi adet en az 1 olmalı.")] public int? PackSize { get; set; }
    [StringLength(30)] public string? Badge { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Kategori seçin.")] public int CategoryId { get; set; }
    public List<ColorRow> Colors { get; set; } = new();
    public List<IFormFile>? NewImages { get; set; }
    public string? StockReason { get; set; }
}

/// <summary>Renk / varyant satırı: her rengin kendi stoğu ve barkodu olabilir.</summary>
public class ColorRow
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Hex { get; set; }
    public string? Barcode { get; set; }
    public int Stock { get; set; }
    public int? OriginalStock { get; set; }
}

public class ProductEditViewModel
{
    public ProductForm Form { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<ProductImage> Images { get; set; } = new();
    public int OrderCount { get; set; }
    public List<StockMovement> Movements { get; set; } = new();
    public string? ProductUrl { get; set; }
}

public class BarcodeSettingsForm
{
    public string Format { get; set; } = "code128";
    [StringLength(6)] public string? Prefix { get; set; }
    public string LabelSize { get; set; } = "50x30";
    // İşaretsiz onay kutuları gönderilmediği için varsayılanlar false olmalı.
    public bool ShowName { get; set; }
    public bool ShowSku { get; set; }
    public bool ShowPrice { get; set; }
    public bool ShowQr { get; set; }
}

public class BarcodeRow
{
    public int ProductId { get; init; }
    public int? VariantId { get; init; }
    public string Name { get; init; } = "";
    public string? VariantName { get; init; }
    public string Sku { get; init; } = "";
    public string? Barcode { get; init; }
    public int Stock { get; init; }
    public bool IsActive { get; init; }
}

public class LabelItem
{
    public string Name { get; init; } = "";
    public string? VariantName { get; init; }
    public string Sku { get; init; } = "";
    public string? Barcode { get; init; }
    public decimal Price { get; init; }
    public string? BarcodeSvg { get; init; }
    public string? QrSvg { get; init; }
}

public class LabelPageViewModel
{
    public List<LabelItem> Labels { get; init; } = new();
    public string Size { get; init; } = "50x30";
    public bool ShowName { get; init; }
    public bool ShowSku { get; init; }
    public bool ShowPrice { get; init; }
    public bool ShowQr { get; init; }
    public int Skipped { get; init; }
}

public class ContentPageForm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Başlık gerekli."), StringLength(160)] public string Title { get; set; } = "";
    [StringLength(160), RegularExpression("^[a-z0-9-]*$", ErrorMessage = "Adres yalnızca küçük harf, rakam ve tire içerebilir.")]
    public string? Slug { get; set; }
    [StringLength(200_000, ErrorMessage = "İçerik çok uzun.")] public string? Content { get; set; }
    public bool ShowInFooter { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; }
}

public class ComposeEmailForm
{
    /// <summary>"tek" = tek alıcı, "onayli" = tüm onaylı ve doğrulanmış müşteriler.</summary>
    public string Mode { get; set; } = "tek";
    [StringLength(160), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")] public string? To { get; set; }
    public int? UserId { get; set; }
    [Required(ErrorMessage = "Konu gerekli."), StringLength(160)] public string Subject { get; set; } = "";
    [Required(ErrorMessage = "Mesaj gerekli."), StringLength(10_000)] public string Message { get; set; } = "";
    [StringLength(60)] public string? ButtonText { get; set; }
    [StringLength(300)] public string? ButtonUrl { get; set; }
}

public class AdminCreateForm
{
    [Required(ErrorMessage = "Ad soyad gerekli."), StringLength(120)] public string FullName { get; set; } = "";
    [Required(ErrorMessage = "E-posta gerekli."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(160)] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Şifre gerekli.")] public string Password { get; set; } = "";
    [Required(ErrorMessage = "Mevcut şifreniz gerekli.")] public string CurrentPassword { get; set; } = "";
}

public class CategoryForm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Kategori adı gerekli."), StringLength(120)] public string Name { get; set; } = "";
    [StringLength(140), RegularExpression("^[a-z0-9-]*$", ErrorMessage = "Adres yalnızca küçük harf, rakam ve tire içerebilir.")]
    public string? Slug { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public IFormFile? Image { get; set; }
    public bool RemoveImage { get; set; }
    public string? ImageUrl { get; set; }
}

public class CategoriesViewModel
{
    public List<(Category Category, int ProductCount)> Rows { get; set; } = new();
    public CategoryForm Form { get; set; } = new();
}

public class StockAdjustForm
{
    public int ProductId { get; set; }
    public int? VariantId { get; set; }
    public string Mode { get; set; } = "increase";
    [Range(0, 10_000_000, ErrorMessage = "Geçersiz miktar.")] public int Amount { get; set; }
    [StringLength(300)] public string? Reason { get; set; }
}

public class SettingsForm
{
    public Dictionary<string, string?> Values { get; set; } = new();
    public IFormFile? LogoFile { get; set; }
    public IFormFile? HeroFile { get; set; }
}

public class AdminPasswordForm
{
    [Required(ErrorMessage = "Mevcut şifre gerekli.")] public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessage = "Yeni şifre gerekli.")] public string NewPassword { get; set; } = "";
    [Required(ErrorMessage = "Şifre tekrarı gerekli."), Compare(nameof(NewPassword), ErrorMessage = "Yeni şifreler eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = "";
}

public class AdminCustomerForm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Ad gerekli."), StringLength(60)] public string FirstName { get; set; } = "";
    [Required(ErrorMessage = "Soyad gerekli."), StringLength(60)] public string LastName { get; set; } = "";
    [Required(ErrorMessage = "E-posta gerekli."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(160)]
    public string Email { get; set; } = "";
    [RegularExpression(@"^[0-9+()\s-]{10,20}$", ErrorMessage = "Geçerli bir telefon numarası girin.")] public string? Phone { get; set; }
    [StringLength(160)] public string? CompanyName { get; set; }
    [StringLength(80)] public string? TaxOffice { get; set; }
    [RegularExpression(@"^\d{10,11}$", ErrorMessage = "Vergi no 10, T.C. kimlik no 11 haneli olmalı.")] public string? TaxNumber { get; set; }
    [StringLength(60)] public string? City { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsApproved { get; set; } = true;
    [StringLength(4000)] public string? AdminNotes { get; set; }
    /// <summary>Boş bırakılırsa şifre değişmez (yeni müşteride zorunlu).</summary>
    public string? NewPassword { get; set; }
}

public class AdminCustomerViewModel
{
    public AdminCustomerForm Form { get; set; } = new();
    public User? User { get; set; }
    public List<Address> Addresses { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
    public decimal Spent { get; set; }
}

public class NoteForm
{
    public int Id { get; set; }
    [StringLength(160)] public string? Title { get; set; }
    [Required(ErrorMessage = "Not boş olamaz."), StringLength(4000)] public string Content { get; set; } = "";
    public bool IsPinned { get; set; }
    public string Color { get; set; } = "gold";
    public static readonly string[] Colors = ["gold", "green", "red", "blue", "gray"];
}

public class AdminProfileForm
{
    [Required(ErrorMessage = "Ad soyad gerekli."), StringLength(120)] public string FullName { get; set; } = "";
    [Required(ErrorMessage = "E-posta gerekli."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(160)] public string Email { get; set; } = "";
    [Required(ErrorMessage = "Değişiklik için mevcut şifrenizi girin.")] public string CurrentPassword { get; set; } = "";
}

public class Paged<T>
{
    public List<T> Items { get; init; } = new();
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int Total { get; init; }
}

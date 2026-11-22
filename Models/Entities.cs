using System.ComponentModel.DataAnnotations;

namespace PraffLeather.Models;

public class Category
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [Required, MaxLength(140)] public string Slug { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
    [MaxLength(300)] public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Product> Products { get; set; } = new();
}

public class Product
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = "";
    [Required, MaxLength(180)] public string Slug { get; set; } = "";
    [Required, MaxLength(40)] public string Sku { get; set; } = "";
    [MaxLength(80)] public string? Subtitle { get; set; }
    [MaxLength(500)] public string? ShortDescription { get; set; }
    [MaxLength(8000)] public string? Description { get; set; }
    /// <summary>Ürün özellikleri: her satır bir madde.</summary>
    [MaxLength(4000)] public string? Features { get; set; }
    /// <summary>Ürün detayları: her satır "Etiket: Değer".</summary>
    [MaxLength(4000)] public string? Details { get; set; }
    public decimal Price { get; set; }
    /// <summary>Toplam stok. Ürünün renk/varyantı varsa varyant stoklarının toplamıdır (StockService günceller).</summary>
    public int StockQuantity { get; set; }
    /// <summary>Üretici barkodu veya firma içi kod (PL000123 / 2xxxxxxxxxxxx). Boş olabilir; doluysa benzersizdir.</summary>
    [MaxLength(64)] public string? Barcode { get; set; }
    /// <summary>Bu seviyenin altına düşünce panoda "kritik stok" olarak listelenir.</summary>
    public int CriticalStockLevel { get; set; } = 5;
    public int MinOrderQuantity { get; set; } = 1;
    public int? PackSize { get; set; }
    [MaxLength(30)] public string? Badge { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime StockUpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ProductImage> Images { get; set; } = new();
    public List<ProductColor> Colors { get; set; } = new();

    public string? MainImageUrl => Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).FirstOrDefault()?.Url;
}

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    [Required, MaxLength(300)] public string Url { get; set; } = "";
    [MaxLength(200)] public string? AltText { get; set; }
    public int SortOrder { get; set; }
}

public class ProductColor
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    [Required, MaxLength(40)] public string Name { get; set; } = "";
    [Required, MaxLength(9)] public string HexCode { get; set; } = "#000000";
    public int SortOrder { get; set; }
    /// <summary>Varyanta özel barkod (boş olabilir; doluysa benzersiz).</summary>
    [MaxLength(64)] public string? Barcode { get; set; }
    /// <summary>Varyant stoğu. Renkli ürünlerde stok renk bazında tutulur.</summary>
    public int StockQuantity { get; set; }
}

public class User
{
    public int Id { get; set; }
    [Required, MaxLength(60)] public string FirstName { get; set; } = "";
    [Required, MaxLength(60)] public string LastName { get; set; } = "";
    [Required, MaxLength(160)] public string Email { get; set; } = "";
    [MaxLength(30)] public string? Phone { get; set; }
    [MaxLength(160)] public string? CompanyName { get; set; }
    [MaxLength(30)] public string? TaxNumber { get; set; }
    [MaxLength(80)] public string? TaxOffice { get; set; }
    [MaxLength(60)] public string? City { get; set; }
    [Required] public string PasswordHash { get; set; } = "";
    /// <summary>Şifre değişince yenilenir; eski oturum çerezlerini geçersiz kılar.</summary>
    [Required, MaxLength(40)] public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    /// <summary>Toptan müşteri onayı: onaylanmayan hesap ürünleri ve fiyatları göremez.</summary>
    public bool IsApproved { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [MaxLength(160)] public string? ApprovedBy { get; set; }
    /// <summary>Yalnızca yöneticilerin gördüğü müşteri notu.</summary>
    [MaxLength(4000)] public string? AdminNotes { get; set; }
    public DateTime? LastLoginAt { get; set; }
    /// <summary>Kayıtta e-postaya gönderilen 6 haneli kodla doğrulanır.</summary>
    public bool EmailConfirmed { get; set; }
    public DateTime? EmailConfirmedAt { get; set; }
    public List<Address> Addresses { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class Admin
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Email { get; set; } = "";
    [Required, MaxLength(120)] public string FullName { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    [Required, MaxLength(40)] public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

public class Address
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    [Required, MaxLength(60)] public string Title { get; set; } = "";
    [Required, MaxLength(120)] public string FullName { get; set; } = "";
    [Required, MaxLength(30)] public string Phone { get; set; } = "";
    [Required, MaxLength(60)] public string City { get; set; } = "";
    [Required, MaxLength(60)] public string District { get; set; } = "";
    [MaxLength(10)] public string? PostalCode { get; set; }
    [Required, MaxLength(400)] public string AddressLine { get; set; } = "";
    public bool IsDefault { get; set; }
}

public class Cart
{
    public int Id { get; set; }
    /// <summary>Misafir sepetleri için çerezdeki anahtar.</summary>
    [Required, MaxLength(40)] public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<CartItem> Items { get; set; } = new();
}

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart? Cart { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    /// <summary>Renk seçimi yoksa boş metin.</summary>
    [MaxLength(40)] public string ColorName { get; set; } = "";
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5
}

public enum PaymentStatus
{
    Awaiting = 0,
    Paid = 1,
    Refunded = 2
}

public class Order
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string OrderNumber { get; set; } = "";
    public int? UserId { get; set; }
    public User? User { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Awaiting;
    [Required, MaxLength(60)] public string PaymentMethod { get; set; } = "";
    [Required, MaxLength(120)] public string FullName { get; set; } = "";
    [Required, MaxLength(160)] public string Email { get; set; } = "";
    [Required, MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(160)] public string? CompanyName { get; set; }
    [MaxLength(30)] public string? TaxNumber { get; set; }
    [Required, MaxLength(400)] public string AddressLine { get; set; } = "";
    [Required, MaxLength(60)] public string City { get; set; } = "";
    [Required, MaxLength(60)] public string District { get; set; } = "";
    [MaxLength(10)] public string? PostalCode { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    [MaxLength(1000)] public string? AdminNote { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    /// <summary>Çift gönderimi engellemek için ödeme formuna verilen tek kullanımlık anahtar.</summary>
    [Required, MaxLength(40)] public string IdempotencyKey { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<OrderItem> Items { get; set; } = new();
    public List<OrderStatusHistory> StatusHistory { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    /// <summary>Ürün silinirse null olur; sipariş geçmişi anlık görüntüden okunur.</summary>
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    [Required, MaxLength(160)] public string ProductName { get; set; } = "";
    [Required, MaxLength(40)] public string Sku { get; set; } = "";
    [MaxLength(40)] public string ColorName { get; set; } = "";
    [MaxLength(300)] public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderStatusHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public OrderStatus Status { get; set; }
    [MaxLength(160)] public string? ChangedBy { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum StockMovementType
{
    Baslangic = 0,
    Giris = 1,
    Cikis = 2,
    Sayim = 3,
    Siparis = 4,
    Duzeltme = 5,
    SiparisIptal = 6
}

/// <summary>Her stok değişikliğinin kaydı. Yalnızca StockService yazar.</summary>
public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    /// <summary>Renk/varyant hareketi ise ProductColor.Id (FK yok: varyant silinse de kayıt kalır).</summary>
    public int? VariantId { get; set; }
    [MaxLength(40)] public string? VariantName { get; set; }
    public StockMovementType Type { get; set; }
    /// <summary>İşaretli değişim: giriş +, çıkış −.</summary>
    public int Quantity { get; set; }
    public int StockBefore { get; set; }
    public int StockAfter { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
    [MaxLength(160)] public string? CreatedBy { get; set; }
    public int? OrderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>KVKK, Hakkımızda gibi yönetimden düzenlenen sayfalar.</summary>
public class ContentPage
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; } = "";
    [Required, MaxLength(160)] public string Slug { get; set; } = "";
    /// <summary>Sunucuda temizlenmiş (sanitize) HTML.</summary>
    public string Content { get; set; } = "";
    public bool ShowInFooter { get; set; } = true;
    public int FooterOrder { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Sistem sayfaları (ör. KVKK) silinemez, yalnızca düzenlenebilir.</summary>
    public bool IsSystem { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>E-posta doğrulama kodu (6 hane). Kodun kendisi değil özeti saklanır.</summary>
public class EmailVerificationCode
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    [Required, MaxLength(100)] public string CodeHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Gönderilen (veya gönderilemeyen) her e-postanın kaydı.</summary>
public class EmailLog
{
    public int Id { get; set; }
    [Required, MaxLength(160)] public string ToEmail { get; set; } = "";
    [Required, MaxLength(200)] public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>Doğrulama, Sipariş, Yönetici, Bildirim, Şifre, Test…</summary>
    [MaxLength(40)] public string Category { get; set; } = "";
    public bool Success { get; set; }
    [MaxLength(500)] public string? Error { get; set; }
    [MaxLength(160)] public string? SentBy { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SiteSetting
{
    [Key, MaxLength(60)] public string Key { get; set; } = "";
    [MaxLength(2000)] public string? Value { get; set; }
}

public class ContactMessage
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string FullName { get; set; } = "";
    [Required, MaxLength(160)] public string Email { get; set; } = "";
    [MaxLength(30)] public string? Phone { get; set; }
    [Required, MaxLength(60)] public string Subject { get; set; } = "";
    [Required, MaxLength(3000)] public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Yönetim panelindeki not defteri.</summary>
public class Note
{
    public int Id { get; set; }
    [MaxLength(160)] public string? Title { get; set; }
    [Required, MaxLength(4000)] public string Content { get; set; } = "";
    public bool IsPinned { get; set; }
    public bool IsDone { get; set; }
    [MaxLength(20)] public string Color { get; set; } = "gold";
    [MaxLength(160)] public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PasswordResetToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    /// <summary>Token'ın SHA-256 özeti saklanır, kendisi değil.</summary>
    [Required, MaxLength(100)] public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using Microsoft.EntityFrameworkCore;
using PraffLeather.Models;

namespace PraffLeather.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Türkçe büyük/küçük harf duyarsız sıralama ve arama (ı/İ, ş, ğ, ü, ö, ç).</summary>
    public const string TurkishCollation = "Turkish_CI_AS";

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductColor> ProductColors => Set<ProductColor>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<ContentPage> ContentPages => Set<ContentPage>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Para tutarları SQL Server'da decimal(18,2).
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).UseCollation(TurkishCollation);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.IsActive, x.DisplayOrder });
        });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.Name).UseCollation(TurkishCollation);
            e.Property(x => x.Subtitle).UseCollation(TurkishCollation);
            e.Property(x => x.Sku).UseCollation(TurkishCollation);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Sku).IsUnique();
            e.HasIndex(x => x.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
            e.HasIndex(x => new { x.IsActive, x.CategoryId });
            e.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Product_Stock", "[StockQuantity] >= 0");
                t.HasCheckConstraint("CK_Product_Price", "[Price] >= 0");
                t.HasCheckConstraint("CK_Product_MinOrder", "[MinOrderQuantity] >= 1");
                t.HasCheckConstraint("CK_Product_Critical", "[CriticalStockLevel] >= 0");
            });
            e.Ignore(x => x.MainImageUrl);
        });

        b.Entity<ProductImage>().HasOne(x => x.Product).WithMany(p => p.Images).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProductColor>(e =>
        {
            e.HasOne(x => x.Product).WithMany(p => p.Colors).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
            e.HasIndex(x => new { x.ProductId, x.Name }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("CK_ProductColor_Stock", "[StockQuantity] >= 0"));
        });

        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.IsApproved);
            e.Property(x => x.FirstName).UseCollation(TurkishCollation);
            e.Property(x => x.LastName).UseCollation(TurkishCollation);
            e.Property(x => x.CompanyName).UseCollation(TurkishCollation);
            e.Property(x => x.City).UseCollation(TurkishCollation);
            e.Ignore(x => x.FullName);
        });
        b.Entity<Admin>().HasIndex(x => x.Email).IsUnique();

        b.Entity<Address>().HasOne(x => x.User).WithMany(u => u.Addresses).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Cart>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<CartItem>(e =>
        {
            e.HasIndex(x => new { x.CartId, x.ProductId, x.ColorName }).IsUnique();
            e.HasOne(x => x.Cart).WithMany(c => c.Items).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_CartItem_Qty", "[Quantity] > 0"));
        });

        b.Entity<Order>(e =>
        {
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => x.Status);
            e.Property(x => x.FullName).UseCollation(TurkishCollation);
            e.Property(x => x.CompanyName).UseCollation(TurkishCollation);
            e.HasOne(x => x.User).WithMany(u => u.Orders).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OrderItem>(e =>
        {
            e.HasOne(x => x.Order).WithMany(o => o.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<OrderStatusHistory>().HasOne(x => x.Order).WithMany(o => o.StatusHistory).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<StockMovement>(e =>
        {
            e.HasIndex(x => new { x.ProductId, x.CreatedAt });
            e.HasIndex(x => new { x.CreatedAt, x.Type });
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PasswordResetToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ContentPage>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.IsActive, x.ShowInFooter, x.FooterOrder });
            e.Property(x => x.Title).UseCollation(TurkishCollation);
        });

        b.Entity<EmailVerificationCode>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EmailLog>(e =>
        {
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.UserId);
        });
    }
}

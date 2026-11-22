using Microsoft.EntityFrameworkCore;
using PraffLeather.Models;
using PraffLeather.Services;

namespace PraffLeather.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Bekleyen migration'lar uygulanır (veritabanı yoksa oluşturulur). Mevcut veri silinmez.
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0) logger.LogInformation("Bekleyen migration'lar uygulanıyor: {List}", string.Join(", ", pending));
        await db.Database.MigrateAsync();

        await SeedSettingsAsync(db);
        await SeedCatalogAsync(db, logger);
        await ContentPageService.SeedAsync(db);
        await BootstrapAdminAsync(db, scope.ServiceProvider.GetRequiredService<PasswordService>(), config, logger);
    }

    private static async Task SeedSettingsAsync(AppDbContext db)
    {
        var existing = await db.SiteSettings.Select(s => s.Key).ToListAsync();
        foreach (var (key, value) in SettingKeys.Defaults.Where(d => !existing.Contains(d.Key)))
            db.SiteSettings.Add(new SiteSetting { Key = key, Value = value });
        await db.SaveChangesAsync();
    }

    /// <summary>Başlangıç demo verisi: yalnızca hiç kategori ve ürün yoksa eklenir; mevcut veriye dokunmaz.</summary>
    private static async Task SeedCatalogAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Categories.AnyAsync() || await db.Products.AnyAsync()) return;

        var belts = new Category { Name = "Kemerler", Slug = "kemerler", DisplayOrder = 1, Description = "Tam tahıl deriden, el dikişli erkek kemerleri." };
        var wallets = new Category { Name = "Cüzdanlar", Slug = "cuzdanlar", DisplayOrder = 2, Description = "İnce, dayanıklı ve zamansız deri cüzdanlar." };
        var bags = new Category { Name = "Çantalar", Slug = "cantalar", DisplayOrder = 3, Description = "Omuz ve el çantaları; günlük kullanım için deri çantalar.", ImageUrl = "/images/nomad.jpg" };
        var travel = new Category { Name = "Seyahat Aksesuarları", Slug = "seyahat-aksesuarlari", DisplayOrder = 4, Description = "Yolculukta düzen ve şıklık için deri aksesuarlar.", ImageUrl = "/images/voyager.jpg" };
        db.Categories.AddRange(belts, wallets, bags, travel);

        var brown = ("Kahverengi", "#4A2F1D");
        var black = ("Siyah", "#161412");
        var tan = ("Taba", "#8A5A2B");
        const int perColor = 300;

        Product P(string name, string slug, string sku, string subtitle, Category cat, decimal price, int min, int pack, string? badge, bool featured,
            string shortDesc, string desc, string features, string details, (string, string)[] colors, params string[] images) => new()
        {
            Name = name, Slug = slug, Sku = sku, Subtitle = subtitle, Category = cat, Price = price,
            StockQuantity = colors.Length == 0 ? 1000 : perColor * colors.Length, CriticalStockLevel = 20,
            MinOrderQuantity = min, PackSize = pack, Badge = badge, IsFeatured = featured,
            ShortDescription = shortDesc, Description = desc, Features = features, Details = details,
            Colors = colors.Select((c, i) => new ProductColor { Name = c.Item1, HexCode = c.Item2, SortOrder = i, StockQuantity = perColor }).ToList(),
            Images = images.Select((u, i) => new ProductImage { Url = u, AltText = name, SortOrder = i }).ToList()
        };

        var products = new[]
        {
            P("Praff Atlas Deri Kemer", "praff-atlas-deri-kemer", "PRF-001", "Deri Kemer", belts, 350m, 10, 20, "Çok Satan", true,
              "Tam tahıl dana derisinden, kenarları elde boyanmış klasik erkek kemeri.",
              "Praff Atlas, tek parça tam tahıl dana derisinden kesilir; kenarları elde zımparalanıp boyanır. Mat nikel toka, takım elbiseden kot pantolona kadar her kombine uyum sağlar. Zamanla kazandığı doğal patina, ürünü her yıl biraz daha karakterli hale getirir.",
              "Tam tahıl dana derisi\nElde boyanmış kenarlar\nMat nikel, değiştirilebilir toka\n3,5 cm kemer genişliği\n85–115 cm arası beden seçenekleri",
              "Malzeme: Dana derisi (tam tahıl)\nGenişlik: 3,5 cm\nKalınlık: 3,8 mm\nToka: Mat nikel alaşım\nMenşei: Türkiye",
              [brown, black, tan]),
            P("Praff Orion Deri Cüzdan", "praff-orion-deri-cuzdan", "PRF-002", "Deri Cüzdan", wallets, 290m, 10, 25, "Yeni", true,
              "İnce yapılı, 8 kart bölmeli, el dikişli erkek cüzdanı.",
              "Praff Orion, cepte hacim yapmayan ince yapısıyla günlük kullanım için tasarlandı. Bitkisel tabaklanmış deri, mumlu iple el dikişiyle birleştirilir. İç yüzeyde kartların kaymasını önleyen sıkı bölmeler bulunur.",
              "Bitkisel tabaklanmış deri\n8 kart bölmesi, 2 gizli göz\nMumlu iple el dikişi\nİnce profil (kapalı 1,2 cm)",
              "Malzeme: Bitkisel tabaklı dana derisi\nÖlçü: 11 × 9 × 1,2 cm\nKart kapasitesi: 8\nMenşei: Türkiye",
              [brown, black]),
            P("Praff Nomad Deri Omuz Çantası", "praff-nomad-deri-omuz-cantasi", "PRF-003", "Omuz Çantası", bags, 1250m, 10, 5, null, true,
              "Gerçek deriden, el işçiliğiyle üretilen omuz çantası. Günlük kullanım için dayanıklı ve şık.",
              "Praff Nomad, şehirde ve yolda gün boyu taşınmak için tasarlandı. Kalın gövde derisi şeklini korur; ayarlanabilir askısı omuzda ve çaprazda rahat kullanılır. Ön kapakta PRAFF LEATHER metal amblemi bulunur.",
              "Yağlı nubuk dana derisi\nAyarlanabilir deri askı\nMıknatıslı ön kapak\nİç fermuarlı cep ve telefon bölmesi\nPRAFF LEATHER metal amblem",
              "Malzeme: Yağlı nubuk dana derisi\nÖlçü: 26 × 18 × 7 cm\nAstar: Pamuklu kanvas\nAksesuar: Antik nikel\nMenşei: Türkiye",
              [brown, black], "/images/nomad.jpg", "/images/model.jpg"),
            P("Praff Elegance Deri El Çantası", "praff-elegance-deri-el-cantasi", "PRF-004", "El Çantası", bags, 1450m, 10, 5, "Yeni", true,
              "Kabartma desenli deriden, zarif ve kompakt erkek el çantası (clutch).",
              "Praff Elegance, kabartma desenli dana derisinden üretilen kompakt bir el çantasıdır. Telefon, cüzdan, pasaport ve küçük aksesuarlar için düzenli bölmeler sunar. Yan kayışı sayesinde elde güvenle taşınır.",
              "Kabartma desenli dana derisi\nYan taşıma kayışı\nÇift bölmeli iç yapı\nPRAFF LEATHER metal amblem",
              "Malzeme: Kabartmalı dana derisi\nÖlçü: 28 × 17 × 6 cm\nKapama: Mıknatıslı kapak + fermuar\nMenşei: Türkiye",
              [brown], "/images/marble.jpg", "/images/banner.jpg"),
            P("Praff Voyager Deri Seyahat Aksesuarı", "praff-voyager-deri-seyahat-aksesuari", "PRF-005", "Seyahat Aksesuarı", travel, 420m, 10, 10, null, true,
              "Pasaport, kart ve seyahat belgeleri için deri organizer.",
              "Praff Voyager, seyahat belgelerinizi tek yerde toplar. Pasaport, biniş kartı, kartlar ve nakit için ayrı bölmeleri vardır. Yumuşak dokulu deri zamanla güzelleşir.",
              "Yumuşak dana derisi\nPasaport ve biniş kartı bölmesi\n6 kart yuvası\nKalem tutucu",
              "Malzeme: Dana derisi\nÖlçü: 22 × 12 × 2 cm\nMenşei: Türkiye",
              [brown, black], "/images/voyager.jpg")
        };
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        foreach (var p in products)
        {
            if (p.Colors.Count == 0)
                db.StockMovements.Add(new StockMovement { ProductId = p.Id, Type = StockMovementType.Baslangic, Quantity = p.StockQuantity, StockBefore = 0, StockAfter = p.StockQuantity, Note = "Başlangıç stoğu", CreatedBy = "Sistem" });
            else
                foreach (var c in p.Colors)
                    db.StockMovements.Add(new StockMovement { ProductId = p.Id, VariantId = c.Id, VariantName = c.Name, Type = StockMovementType.Baslangic, Quantity = c.StockQuantity, StockBefore = 0, StockAfter = c.StockQuantity, Note = "Başlangıç stoğu", CreatedBy = "Sistem" });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Başlangıç kategori ve ürün verisi eklendi.");
    }

    /// <summary>
    /// İlk yönetici yalnızca PRAFF_ADMIN_EMAIL + PRAFF_ADMIN_PASSWORD ortam değişkenleri verildiğinde
    /// ve bu e-postayla bir yönetici yoksa oluşturulur. Mevcut yöneticinin şifresi asla sıfırlanmaz.
    /// </summary>
    private static async Task BootstrapAdminAsync(AppDbContext db, PasswordService passwords, IConfiguration config, ILogger logger)
    {
        var email = config["PRAFF_ADMIN_EMAIL"]?.Trim().ToLowerInvariant();
        var password = config["PRAFF_ADMIN_PASSWORD"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            if (!await db.Admins.AnyAsync())
                logger.LogWarning("Hiç yönetici hesabı yok. PRAFF_ADMIN_EMAIL ve PRAFF_ADMIN_PASSWORD ortam değişkenleriyle uygulamayı bir kez başlatın (README).");
            return;
        }
        if (await db.Admins.AnyAsync(a => a.Email == email)) return;
        var err = Controllers.Admin.AdminAccountController.AdminStrength(password);
        if (err != null)
        {
            logger.LogError("Yönetici oluşturulmadı: {Error}", err);
            return;
        }
        db.Admins.Add(new Admin { Email = email, FullName = config["PRAFF_ADMIN_NAME"] ?? "Yönetici", PasswordHash = passwords.Hash(password) });
        await db.SaveChangesAsync();
        logger.LogInformation("Yönetici hesabı oluşturuldu: {Email}. Ortam değişkenlerini şimdi kaldırabilirsiniz.", email);
    }
}

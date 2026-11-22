using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PraffLeather.Data;
using PraffLeather.Models;

namespace PraffLeather.Services;

public static class SettingKeys
{
    public const string SiteTitle = "SiteTitle";
    public const string LogoUrl = "LogoUrl";
    public const string ContactEmail = "ContactEmail";
    public const string Phone = "Phone";
    public const string WhatsApp = "WhatsApp";
    public const string InstagramUrl = "InstagramUrl";
    public const string InstagramHandle = "InstagramHandle";
    public const string YoutubeUrl = "YoutubeUrl";
    public const string LinkedinUrl = "LinkedinUrl";
    public const string TiktokUrl = "TiktokUrl";
    public const string Address = "Address";
    public const string WorkingHours = "WorkingHours";
    public const string HeroEyebrow = "HeroEyebrow";
    public const string HeroTitle = "HeroTitle";
    public const string HeroSubtitle = "HeroSubtitle";
    public const string HeroImage = "HeroImage";
    public const string ShippingCost = "ShippingCost";
    public const string FreeShippingThreshold = "FreeShippingThreshold";
    public const string LowStockThreshold = "LowStockThreshold";
    public const string BankInfo = "BankInfo";
    /// <summary>Sitenin genel adresi (QR kodları ve e-posta bağlantıları için), ör. https://www.praffleather.com</summary>
    public const string SiteUrl = "SiteUrl";
    public const string BarcodeFormat = "BarcodeFormat";
    public const string BarcodePrefix = "BarcodePrefix";
    public const string LabelSize = "LabelSize";
    public const string LabelShowName = "LabelShowName";
    public const string LabelShowSku = "LabelShowSku";
    public const string LabelShowPrice = "LabelShowPrice";
    public const string LabelShowQr = "LabelShowQr";
    public const string CookieBanner = "CookieBanner";
    /// <summary>Yeni başvuru / sipariş bildirimlerinin gideceği yönetici e-postası.</summary>
    public const string AdminNotifyEmail = "AdminNotifyEmail";
    public const string RequireEmailVerification = "RequireEmailVerification";

    /// <summary>Site Ayarları ana formundaki anahtarlar (diğerleri kendi sayfalarında kaydedilir).</summary>
    public static readonly string[] GeneralKeys =
    [
        SiteTitle, LogoUrl, ContactEmail, Phone, WhatsApp, InstagramUrl, InstagramHandle, YoutubeUrl, LinkedinUrl, TiktokUrl,
        Address, WorkingHours, HeroEyebrow, HeroTitle, HeroSubtitle, HeroImage, ShippingCost, FreeShippingThreshold,
        LowStockThreshold, BankInfo, SiteUrl, CookieBanner
    ];

    public static readonly Dictionary<string, string> Defaults = new()
    {
        [SiteUrl] = "https://prafftoptan.com",
        [BarcodeFormat] = "code128",
        [BarcodePrefix] = "PL",
        [LabelSize] = "50x30",
        [LabelShowName] = "1",
        [LabelShowSku] = "1",
        [LabelShowPrice] = "0",
        [LabelShowQr] = "0",
        [CookieBanner] = "1",
        [AdminNotifyEmail] = "",
        [RequireEmailVerification] = "1",
        [SiteTitle] = "PRAFF LEATHER",
        [LogoUrl] = "/images/brand/praff-logo-champagne.png",
        [ContactEmail] = "",
        [Phone] = "",
        [WhatsApp] = "",
        [InstagramUrl] = "",
        [InstagramHandle] = "",
        [YoutubeUrl] = "",
        [LinkedinUrl] = "",
        [TiktokUrl] = "",
        [Address] = "",
        [WorkingHours] = "Hafta içi 09:00 – 18:00",
        [HeroEyebrow] = "PRAFF LEATHER — Handcrafted for Men",
        [HeroTitle] = "Kaliteyi Toptan Sunuyoruz",
        [HeroSubtitle] = "Deri cüzdan, kemer ve çanta çeşitlerinde güvenilir tedarik.",
        [HeroImage] = "/images/hero.jpg",
        [ShippingCost] = "0",
        [FreeShippingThreshold] = "0",
        [LowStockThreshold] = "50",
        [BankInfo] = ""
    };
}

public class SiteSettings(IReadOnlyDictionary<string, string> values)
{
    public string this[string key] => values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : SettingKeys.Defaults.GetValueOrDefault(key, "");
    public string Raw(string key) => values.TryGetValue(key, out var v) ? v ?? "" : "";
    public bool Has(string key) => !string.IsNullOrWhiteSpace(Raw(key));

    public decimal Decimal(string key)
    {
        var raw = this[key].Replace(',', '.');
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) && d >= 0 ? d : 0;
    }

    public int Int(string key, int fallback) => int.TryParse(this[key], out var i) && i >= 0 ? i : fallback;
    public bool Flag(string key) => this[key] == "1";
}

public class SiteSettingsService(AppDbContext db, IMemoryCache cache)
{
    private const string CacheKey = "site-settings";

    public async Task<SiteSettings> GetAsync()
    {
        if (cache.TryGetValue(CacheKey, out SiteSettings? cached) && cached != null) return cached;
        var dict = await db.SiteSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value ?? "");
        var settings = new SiteSettings(dict);
        cache.Set(CacheKey, settings, TimeSpan.FromMinutes(10));
        return settings;
    }

    public async Task SaveAsync(IDictionary<string, string?> values)
    {
        foreach (var (key, value) in values)
        {
            if (!SettingKeys.Defaults.ContainsKey(key)) continue;
            var row = await db.SiteSettings.FindAsync(key);
            var clean = value?.Trim();
            if (row == null) db.SiteSettings.Add(new SiteSetting { Key = key, Value = clean });
            else row.Value = clean;
        }
        await db.SaveChangesAsync();
        cache.Remove(CacheKey);
    }

    /// <summary>Shipping is computed only on the server from settings.</summary>
    public static decimal ShippingFor(SiteSettings s, decimal subtotal)
    {
        var cost = s.Decimal(SettingKeys.ShippingCost);
        var free = s.Decimal(SettingKeys.FreeShippingThreshold);
        if (subtotal <= 0) return 0;
        if (free > 0 && subtotal >= free) return 0;
        return cost;
    }
}

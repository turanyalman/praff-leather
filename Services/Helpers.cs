using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using PraffLeather.Models;

namespace PraffLeather.Services;

public static class Fmt
{
    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    public static readonly TimeZoneInfo TrZone = FindZone();

    static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        }
        return TimeZoneInfo.Utc;
    }

    public static string Money(decimal value) => "₺" + value.ToString("N2", Tr);
    public static string Number(int value) => value.ToString("N0", Tr);
    public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TrZone);
    public static string Date(DateTime utc) => Local(utc).ToString("dd.MM.yyyy", Tr);
    public static string DateTimeText(DateTime utc) => Local(utc).ToString("dd.MM.yyyy HH:mm", Tr);

    public static string StatusText(OrderStatus s) => s switch
    {
        OrderStatus.Pending => "Beklemede",
        OrderStatus.Confirmed => "Onaylandı",
        OrderStatus.Processing => "Hazırlanıyor",
        OrderStatus.Shipped => "Kargoya Verildi",
        OrderStatus.Delivered => "Teslim Edildi",
        OrderStatus.Cancelled => "İptal Edildi",
        _ => s.ToString()
    };

    public static string StatusCss(OrderStatus s) => s switch
    {
        OrderStatus.Pending => "b-wait",
        OrderStatus.Confirmed or OrderStatus.Processing => "b-gold",
        OrderStatus.Shipped or OrderStatus.Delivered => "b-ok",
        OrderStatus.Cancelled => "b-off",
        _ => ""
    };

    public static string PaymentText(PaymentStatus s) => s switch
    {
        PaymentStatus.Awaiting => "Ödeme Bekleniyor",
        PaymentStatus.Paid => "Ödendi",
        PaymentStatus.Refunded => "İade Edildi",
        _ => s.ToString()
    };

    public static string MovementText(StockMovementType t) => t switch
    {
        StockMovementType.Baslangic => "Başlangıç",
        StockMovementType.Giris => "Giriş",
        StockMovementType.Cikis => "Çıkış",
        StockMovementType.Sayim => "Sayım",
        StockMovementType.Siparis => "Sipariş",
        StockMovementType.Duzeltme => "Düzeltme",
        StockMovementType.SiparisIptal => "Sipariş İptali",
        _ => t.ToString()
    };

    public static string MovementCss(StockMovementType t) => t switch
    {
        StockMovementType.Giris or StockMovementType.SiparisIptal or StockMovementType.Baslangic => "b-ok",
        StockMovementType.Cikis or StockMovementType.Siparis => "b-wait",
        StockMovementType.Sayim => "b-gold",
        _ => "b-off"
    };

    /// <summary>CSV hücresi: ayraç ";" (Türkçe Excel), tırnak kaçışı ve formül enjeksiyonuna karşı önek.</summary>
    public static string Csv(object? value)
    {
        var s = value switch
        {
            null => "",
            DateTime d => DateTimeText(d),
            decimal m => m.ToString("0.00", Tr),
            _ => value.ToString() ?? ""
        };
        if (s.Length > 0 && "=+-@\t\r".Contains(s[0]) && !(value is int or decimal or long)) s = "'" + s;
        return s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    public static byte[] CsvFile(IEnumerable<string> header, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", header.Select(Csv)));
        foreach (var r in rows) sb.AppendLine(string.Join(";", r.Select(Csv)));
        // UTF-8 BOM: Excel Türkçe karakterleri doğru göstersin.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public static string StockText(int stock, int lowThreshold = 50) =>
        stock <= 0 ? "Stokta yok" : stock <= lowThreshold ? $"Son {Number(stock)} adet" : $"{Number(stock)} adet stokta";

    public static string Masked(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    public static IEnumerable<string> Lines(string? text) =>
        (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

    public static string WhatsAppLink(string? number, string? text = null)
    {
        var digits = new string((number ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return "";
        if (digits.StartsWith("0")) digits = "9" + digits;
        var url = "https://wa.me/" + digits;
        return string.IsNullOrEmpty(text) ? url : url + "?text=" + Uri.EscapeDataString(text);
    }
}

public static class Slug
{
    public static string Create(string text)
    {
        var map = new Dictionary<char, string>
        {
            ['ç'] = "c", ['Ç'] = "c", ['ğ'] = "g", ['Ğ'] = "g", ['ı'] = "i", ['I'] = "i", ['İ'] = "i",
            ['ö'] = "o", ['Ö'] = "o", ['ş'] = "s", ['Ş'] = "s", ['ü'] = "u", ['Ü'] = "u", ['â'] = "a", ['î'] = "i", ['û'] = "u"
        };
        var sb = new StringBuilder();
        foreach (var ch in text.Trim())
            sb.Append(map.TryGetValue(ch, out var r) ? r : char.ToLowerInvariant(ch).ToString());
        var s = Regex.Replace(sb.ToString(), "[^a-z0-9]+", "-").Trim('-');
        return s.Length == 0 ? "urun" : s.Length > 120 ? s[..120].Trim('-') : s;
    }
}

/// <summary>ASP.NET Core Identity'nin PBKDF2 (HMAC-SHA512, 100k iterasyon) şifre özetleyicisini kullanır.</summary>
public class PasswordService
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object Subject = new();

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public bool Verify(string hash, string password)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        try
        {
            return _hasher.VerifyHashedPassword(Subject, hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException) { return false; }
    }

    /// <summary>En az 8 karakter, harf ve rakam içermeli.</summary>
    public static string? StrengthError(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return "Şifre en az 8 karakter olmalıdır.";
        if (password.Length > 128) return "Şifre en fazla 128 karakter olabilir.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "Şifre en az bir harf ve bir rakam içermelidir.";
        return null;
    }
}

public static class TokenHelper
{
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    public static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

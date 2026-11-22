using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using QRCoder;

namespace PraffLeather.Services;

/// <summary>Code 128 (B) ve EAN-13 çizgi barkodlarını modül dizisine çevirir ve SVG üretir. Harici kütüphane kullanmaz.</summary>
public static class BarcodeEncoder
{
    // Code 128 desenleri: her karakter 3 çubuk + 3 boşluk genişliği (toplam 11 modül). 106 = Stop (13 modül).
    public static readonly string[] Code128Patterns =
    [
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112"
    ];

    private const int StartB = 104, Stop = 106;

    public static bool IsCode128Text(string text) => text.Length is > 0 and <= 48 && text.All(c => c >= 32 && c <= 126);

    /// <summary>Code 128 alt küme B ile kodlar. true = siyah modül.</summary>
    public static bool[] Code128Modules(string text)
    {
        if (!IsCode128Text(text)) throw new ArgumentException("Code 128 için yalnızca standart ASCII karakterler kullanılabilir.", nameof(text));
        var values = new List<int> { StartB };
        values.AddRange(text.Select(c => c - 32));
        var checksum = StartB;
        for (var i = 1; i < values.Count; i++) checksum += values[i] * i;
        values.Add(checksum % 103);
        values.Add(Stop);

        var modules = new List<bool>();
        foreach (var v in values)
        {
            var pattern = Code128Patterns[v];
            for (var i = 0; i < pattern.Length; i++)
                modules.AddRange(Enumerable.Repeat(i % 2 == 0, pattern[i] - '0'));
        }
        return modules.ToArray();
    }

    private static readonly string[] EanL = ["0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011"];
    private static readonly string[] EanG = ["0100111", "0110011", "0011011", "0100001", "0011101", "0111001", "0000101", "0010001", "0001001", "0010111"];
    private static readonly string[] EanR = ["1110010", "1100110", "1101100", "1000010", "1011100", "1001110", "1010000", "1000100", "1001000", "1110100"];
    private static readonly string[] EanParity = ["LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"];

    /// <summary>12 hanenin EAN-13 kontrol basamağı.</summary>
    public static int Ean13CheckDigit(string first12)
    {
        if (first12.Length != 12 || !first12.All(char.IsAsciiDigit)) throw new ArgumentException("12 rakam gerekli.", nameof(first12));
        var sum = 0;
        for (var i = 0; i < 12; i++) sum += (first12[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - sum % 10) % 10;
    }

    public static bool IsValidEan13(string code) =>
        code.Length == 13 && code.All(char.IsAsciiDigit) && Ean13CheckDigit(code[..12]) == code[12] - '0';

    public static bool[] Ean13Modules(string code)
    {
        if (!IsValidEan13(code)) throw new ArgumentException("Geçersiz EAN-13 kodu.", nameof(code));
        var sb = new StringBuilder("101");
        var parity = EanParity[code[0] - '0'];
        for (var i = 1; i <= 6; i++) sb.Append(parity[i - 1] == 'L' ? EanL[code[i] - '0'] : EanG[code[i] - '0']);
        sb.Append("01010");
        for (var i = 7; i <= 12; i++) sb.Append(EanR[code[i] - '0']);
        sb.Append("101");
        return sb.ToString().Select(c => c == '1').ToArray();
    }

    /// <summary>Modül dizisinden ölçeklenebilir SVG (sessiz bölge dahil). Altında kodun düz yazısı yer alır.</summary>
    public static string ToSvg(bool[] modules, string? caption, int quiet = 10, int barHeight = 60)
    {
        var width = modules.Length + quiet * 2;
        var textH = caption == null ? 0 : 14;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {barHeight + textH}\" preserveAspectRatio=\"none\" shape-rendering=\"crispEdges\">");
        sb.Append(CultureInfo.InvariantCulture, $"<rect width=\"{width}\" height=\"{barHeight + textH}\" fill=\"#fff\"/><path fill=\"#000\" d=\"");
        var x = 0;
        while (x < modules.Length)
        {
            if (!modules[x]) { x++; continue; }
            var start = x;
            while (x < modules.Length && modules[x]) x++;
            sb.Append(CultureInfo.InvariantCulture, $"M{start + quiet} 0h{x - start}v{barHeight}h-{x - start}z");
        }
        sb.Append("\"/>");
        if (caption != null)
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{width / 2.0}\" y=\"{barHeight + 11}\" font-family=\"monospace\" font-size=\"11\" text-anchor=\"middle\" fill=\"#000\">{WebUtility.HtmlEncode(caption)}</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>QR kodu SVG olarak üretir (QRCoder ile matris, çizim burada).</summary>
    public static string QrSvg(string text, int quiet = 4)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var m = data.ModuleMatrix;
        var size = m.Count;
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size + quiet * 2} {size + quiet * 2}\" shape-rendering=\"crispEdges\">");
        sb.Append(CultureInfo.InvariantCulture, $"<rect width=\"{size + quiet * 2}\" height=\"{size + quiet * 2}\" fill=\"#fff\"/><path fill=\"#000\" d=\"");
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                if (m[y][x]) sb.Append(CultureInfo.InvariantCulture, $"M{x + quiet} {y + quiet}h1v1h-1z");
        sb.Append("\"/></svg>");
        return sb.ToString();
    }
}

public record ScanMatch(Product Product, ProductColor? Variant, string MatchedBy);

/// <summary>Barkod üretimi, benzersizlik kontrolü ve okutulan koddan ürün bulma.</summary>
public class BarcodeService(AppDbContext db, SiteSettingsService settings)
{
    public const string FormatCode128 = "code128";
    public const string FormatEan13 = "ean13";

    /// <summary>Barkod başka bir üründe veya varyantta kullanılıyor mu?</summary>
    public async Task<bool> IsTakenAsync(string code, int? exceptProductId = null, int? exceptVariantId = null) =>
        await db.Products.AnyAsync(p => p.Barcode == code && p.Id != exceptProductId)
        || await db.ProductColors.AnyAsync(c => c.Barcode == code && c.Id != exceptVariantId);

    /// <summary>Ayarlardaki biçime göre bir sonraki boş kodu üretir (çakışmada sıradakini alır).</summary>
    public async Task<string> NextAsync(HashSet<string>? reserved = null)
    {
        var s = await settings.GetAsync();
        var format = s[SettingKeys.BarcodeFormat];
        var existing = await db.Products.Where(p => p.Barcode != null).Select(p => p.Barcode!)
            .Concat(db.ProductColors.Where(c => c.Barcode != null).Select(c => c.Barcode!)).ToListAsync();
        var used = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        if (reserved != null) used.UnionWith(reserved);

        if (format == FormatEan13)
        {
            // Mağaza içi aralık: "20" + 10 haneli sıra + kontrol basamağı (resmi GS1 barkodu değildir).
            var max = used.Where(c => c.Length == 13 && c.StartsWith("20") && c.All(char.IsAsciiDigit))
                .Select(c => long.Parse(c.Substring(2, 10), CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max();
            for (var n = max + 1; ; n++)
            {
                var first12 = "20" + n.ToString("D10", CultureInfo.InvariantCulture);
                var code = first12 + BarcodeEncoder.Ean13CheckDigit(first12);
                if (!used.Contains(code)) return code;
            }
        }

        var prefix = CleanPrefix(s[SettingKeys.BarcodePrefix]);
        var maxSeq = used.Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && c.Length > prefix.Length && c[prefix.Length..].All(char.IsAsciiDigit))
            .Select(c => long.TryParse(c[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : 0).DefaultIfEmpty(0).Max();
        for (var n = maxSeq + 1; ; n++)
        {
            var code = prefix + n.ToString("D6", CultureInfo.InvariantCulture);
            if (!used.Contains(code)) return code;
        }
    }

    public static string CleanPrefix(string? prefix)
    {
        var p = new string((prefix ?? "").Trim().ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        return p.Length == 0 ? "PL" : p.Length > 6 ? p[..6] : p;
    }

    /// <summary>Elle girilen/okutulan barkodu doğrular. Null = geçerli.</summary>
    public static string? Validate(string? code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        if (code.Length > 48) return "Barkod en fazla 48 karakter olabilir.";
        if (code.Any(char.IsWhiteSpace)) return "Barkod boşluk içeremez.";
        if (!BarcodeEncoder.IsCode128Text(code)) return "Barkod yalnızca harf, rakam ve standart işaretler içerebilir (Türkçe karakter olmadan).";
        return null;
    }

    /// <summary>Barkodu çizer: 13 haneli geçerli EAN-13 ise EAN-13, değilse Code 128.</summary>
    public static string RenderSvg(string code) =>
        BarcodeEncoder.IsValidEan13(code)
            ? BarcodeEncoder.ToSvg(BarcodeEncoder.Ean13Modules(code), code)
            : BarcodeEncoder.ToSvg(BarcodeEncoder.Code128Modules(code), code);

    /// <summary>Sitenin genel adresi: Site Ayarları &gt; Site Adresi, yoksa isteğin adresi.</summary>
    public async Task<string> BaseUrlAsync(HttpRequest request)
    {
        var s = await settings.GetAsync();
        var url = s.Raw(SettingKeys.SiteUrl).Trim().TrimEnd('/');
        return string.IsNullOrEmpty(url) ? $"{request.Scheme}://{request.Host}" : url;
    }

    /// <summary>
    /// Okutulan metinden ürünü bulur: varyant barkodu, ürün barkodu, stok kodu (SKU) veya QR içindeki /urun/{slug} adresi.
    /// </summary>
    public async Task<ScanMatch?> FindAsync(string? scanned)
    {
        var text = (scanned ?? "").Trim();
        if (text.Length == 0 || text.Length > 300) return null;

        var slug = SlugFromUrl(text);
        if (slug != null)
        {
            var bySlug = await LoadProduct(db.Products.Where(p => p.Slug == slug));
            if (bySlug != null) return new ScanMatch(bySlug, null, "QR");
        }

        var variant = await db.ProductColors.AsNoTracking().FirstOrDefaultAsync(c => c.Barcode == text);
        if (variant != null)
        {
            var vp = await LoadProduct(db.Products.Where(p => p.Id == variant.ProductId));
            if (vp != null) return new ScanMatch(vp, vp.Colors.First(c => c.Id == variant.Id), "Barkod");
        }

        var byBarcode = await LoadProduct(db.Products.Where(p => p.Barcode == text));
        if (byBarcode != null) return new ScanMatch(byBarcode, null, "Barkod");

        var upper = text.ToUpperInvariant();
        var bySku = await LoadProduct(db.Products.Where(p => p.Sku == upper));
        return bySku != null ? new ScanMatch(bySku, null, "Stok kodu") : null;
    }

    private static Task<Product?> LoadProduct(IQueryable<Product> q) =>
        q.AsNoTracking().Include(p => p.Images).Include(p => p.Colors).AsSplitQuery().FirstOrDefaultAsync();

    /// <summary>"https://alanadi/urun/praff-atlas?x=1" → "praff-atlas".</summary>
    public static string? SlugFromUrl(string text)
    {
        var i = text.IndexOf("/urun/", StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var rest = text[(i + 6)..];
        var end = rest.IndexOfAny(['?', '#', '/']);
        if (end >= 0) rest = rest[..end];
        rest = Uri.UnescapeDataString(rest).Trim().ToLowerInvariant();
        return rest.Length is > 0 and <= 180 && rest.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-') ? rest : null;
    }
}

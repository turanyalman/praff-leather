using PraffLeather.Models;
using PraffLeather.Services;
using ZXing;
using ZXing.Common;
using ZXing.OneD;

namespace PraffLeather.Tests;

public class BarcodeTests
{
    /// <summary>Üretilen modülleri tek bir tarama satırına çevirip ZXing ile geri okur (gerçek okuyucu benzetimi).</summary>
    private static string? Decode(OneDReader reader, bool[] modules)
    {
        const int quiet = 12;
        var row = new BitArray(modules.Length + quiet * 2);
        for (var i = 0; i < modules.Length; i++) if (modules[i]) row[i + quiet] = true;
        try { return reader.decodeRow(0, row, new Dictionary<DecodeHintType, object>())?.Text; }
        catch (ReaderException) { return null; }
    }

    [Fact]
    public void Code128_patterns_are_valid_widths()
    {
        Assert.Equal(107, BarcodeEncoder.Code128Patterns.Length);
        for (var i = 0; i < 106; i++) Assert.Equal(11, BarcodeEncoder.Code128Patterns[i].Sum(c => c - '0'));
        Assert.Equal(13, BarcodeEncoder.Code128Patterns[106].Sum(c => c - '0'));
        Assert.Equal(107, BarcodeEncoder.Code128Patterns.Distinct().Count());
    }

    [Theory]
    [InlineData("PL000123")]
    [InlineData("PL000001")]
    [InlineData("PRF-001")]
    [InlineData("ABC xyz 0189!")]
    [InlineData("2000000000015")]
    public void Code128_is_decoded_back_by_a_real_reader(string text)
    {
        var modules = BarcodeEncoder.Code128Modules(text);
        Assert.Equal(text, Decode(new Code128Reader(), modules));
    }

    [Theory]
    [InlineData("200000000001")]
    [InlineData("200000012345")]
    [InlineData("869000000001")]
    public void Ean13_check_digit_and_encoding_round_trip(string first12)
    {
        var code = first12 + BarcodeEncoder.Ean13CheckDigit(first12);
        Assert.True(BarcodeEncoder.IsValidEan13(code));
        Assert.Equal(code, Decode(new EAN13Reader(), BarcodeEncoder.Ean13Modules(code)));
    }

    [Theory]
    [InlineData("590123412345", 7)]   // GS1 örnek kodu: 5901234123457
    [InlineData("400638133393", 1)]   // 4006381333931
    public void Ean13_check_digit_matches_known_codes(string first12, int expected) =>
        Assert.Equal(expected, BarcodeEncoder.Ean13CheckDigit(first12));

    [Fact]
    public void Ean13_rejects_wrong_check_digit() => Assert.False(BarcodeEncoder.IsValidEan13("5901234123458"));

    [Fact]
    public void Code128_rejects_turkish_characters() => Assert.Throws<ArgumentException>(() => BarcodeEncoder.Code128Modules("ÇANTA"));

    [Fact]
    public void Svg_contains_caption_and_is_well_formed()
    {
        var svg = BarcodeService.RenderSvg("PL000123");
        Assert.StartsWith("<svg", svg);
        Assert.Contains("PL000123", svg);
        System.Xml.Linq.XDocument.Parse(svg);
        System.Xml.Linq.XDocument.Parse(BarcodeEncoder.QrSvg("https://www.ornek.com/urun/praff-atlas-deri-kemer"));
    }

    [Theory]
    [InlineData("https://www.praffleather.com/urun/praff-atlas-deri-kemer", "praff-atlas-deri-kemer")]
    [InlineData("http://localhost:5080/urun/praff-orion?utm=qr", "praff-orion")]
    [InlineData("PL000123", null)]
    [InlineData("https://x.com/urun/<script>", null)]
    public void Qr_url_is_parsed_to_slug(string text, string? slug) => Assert.Equal(slug, BarcodeService.SlugFromUrl(text));

    [Theory]
    [InlineData("pl", "PL")]
    [InlineData(" pr-f ", "PRF")]
    [InlineData("", "PL")]
    [InlineData("ABCDEFGH", "ABCDEF")]
    public void Prefix_is_cleaned(string input, string expected) => Assert.Equal(expected, BarcodeService.CleanPrefix(input));

    [Theory]
    [InlineData("PL000123", true)]
    [InlineData("8690000000017", true)]
    [InlineData("PL 0001", false)]
    [InlineData("ÇANTA1", false)]
    public void Manual_barcode_validation(string code, bool valid) => Assert.Equal(valid, BarcodeService.Validate(code) == null);
}

public class OrderRuleTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled, false)]   // kargodaki sipariş iptal edilemez
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled, false)] // teslim edilen iptal edilemez
    [InlineData(OrderStatus.Shipped, OrderStatus.Pending, false)]     // geriye dönüş yok
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending, false)]   // iptal yeniden açılmaz
    [InlineData(OrderStatus.Confirmed, OrderStatus.Pending, false)]
    public void Status_transitions(OrderStatus from, OrderStatus to, bool allowed) => Assert.Equal(allowed, OrderService.CanTransition(from, to));

    [Fact]
    public void Customer_can_cancel_only_pending()
    {
        Assert.True(OrderService.CustomerCanCancel(new Order { Status = OrderStatus.Pending }));
        Assert.False(OrderService.CustomerCanCancel(new Order { Status = OrderStatus.Confirmed }));
    }
}

public class ContentTests
{
    [Fact]
    public void Sanitizer_removes_scripts_and_event_handlers()
    {
        var html = ContentPageService.Sanitize("<h2 onclick=\"x()\">Başlık</h2><script>alert(1)</script><p style=\"color:red\">a <a href=\"javascript:alert(1)\">b</a> <a href=\"https://x.com\" target=\"_blank\">c</a></p><img src=x onerror=alert(1)>");
        Assert.DoesNotContain("script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", html);
        Assert.DoesNotContain("onerror", html);
        Assert.DoesNotContain("javascript:", html);
        Assert.DoesNotContain("style=", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("<h2>Başlık</h2>", html);
        Assert.Contains("rel=\"noopener noreferrer\"", html);
    }

    [Theory]
    [InlineData("KVKK Aydınlatma Metni", "kvkk-aydinlatma-metni")]
    [InlineData("Çerez Politikası", "cerez-politikasi")]
    [InlineData("İade ve Değişim Koşulları", "iade-ve-degisim-kosullari")]
    [InlineData("Satış ve Teslimat Koşulları", "satis-ve-teslimat-kosullari")]
    [InlineData("Hakkımızda", "hakkimizda")]
    public void Turkish_slugs(string title, string slug) => Assert.Equal(slug, Slug.Create(title));

    [Fact]
    public void Csv_escapes_and_blocks_formula_injection()
    {
        Assert.Equal("\"a;b\"", Fmt.Csv("a;b"));
        Assert.Equal("\"say \"\"merhaba\"\"\"", Fmt.Csv("say \"merhaba\""));
        Assert.Equal("'=HYPERLINK(1)", Fmt.Csv("=HYPERLINK(1)"));
        Assert.Equal("-5", Fmt.Csv(-5));
    }

    [Fact]
    public void Plain_text_email_is_encoded()
    {
        var html = EmailService.TextToHtml("Merhaba <b>{ad}</b>\nikinci satır\n\nyeni paragraf");
        Assert.Contains("&lt;b&gt;", html);
        Assert.Contains("<br>", html);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "<p ").Count);
    }
}

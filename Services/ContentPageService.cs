using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PraffLeather.Data;
using PraffLeather.Models;

namespace PraffLeather.Services;

public record FooterLink(string Title, string Slug);

/// <summary>Yasal metinler ve bilgi sayfaları: HTML temizleme, footer bağlantıları (önbellekli).</summary>
public class ContentPageService(AppDbContext db, IMemoryCache cache)
{
    public const string KvkkSlug = "kvkk-aydinlatma-metni";
    public const string CookieSlug = "cerez-politikasi";
    public const string SalesSlug = "satis-ve-teslimat-kosullari";
    public const string ReturnsSlug = "iade-ve-degisim-kosullari";
    public const string PrivacySlug = "gizlilik-politikasi";
    public const string TermsSlug = "kullanim-kosullari";
    private const string FooterKey = "footer-pages";

    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var s = new HtmlSanitizer();
        s.AllowedTags.Clear();
        foreach (var t in new[] { "p", "br", "b", "strong", "i", "em", "u", "s", "h2", "h3", "h4", "ul", "ol", "li", "a", "blockquote", "hr", "span", "div" })
            s.AllowedTags.Add(t);
        s.AllowedAttributes.Clear();
        s.AllowedAttributes.Add("href");
        s.AllowedAttributes.Add("target");
        s.AllowedAttributes.Add("rel");
        s.AllowedCssProperties.Clear(); // satır içi stil yok
        s.AllowedSchemes.Clear();
        foreach (var sc in new[] { "http", "https", "mailto", "tel" }) s.AllowedSchemes.Add(sc);
        s.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Html.Dom.IHtmlAnchorElement a)
            {
                if (a.GetAttribute("target") == "_blank") a.SetAttribute("rel", "noopener noreferrer");
                else a.RemoveAttribute("target");
            }
        };
        return s;
    }

    /// <summary>XSS'e karşı yalnızca izinli etiketleri bırakır (script, stil, olay öznitelikleri silinir).</summary>
    public static string Sanitize(string? html) => Sanitizer.Sanitize(html ?? "").Trim();

    public async Task<List<FooterLink>> FooterAsync()
    {
        if (cache.TryGetValue(FooterKey, out List<FooterLink>? cached) && cached != null) return cached;
        var list = await db.ContentPages.AsNoTracking().Where(p => p.IsActive && p.ShowInFooter)
            .OrderBy(p => p.FooterOrder).ThenBy(p => p.Title).Select(p => new FooterLink(p.Title, p.Slug)).ToListAsync();
        cache.Set(FooterKey, list, TimeSpan.FromMinutes(10));
        return list;
    }

    public void Invalidate() => cache.Remove(FooterKey);

    public Task<ContentPage?> FindActiveAsync(string slug) =>
        db.ContentPages.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);

    /// <summary>Varsayılan sayfalar: yalnızca eksik olanlar eklenir (mevcut içeriğe dokunulmaz).</summary>
    public static async Task SeedAsync(AppDbContext db)
    {
        const string note = "<p><em>Bu metin admin panelinden düzenlenecektir.</em></p>";
        var defaults = new (string Title, string Slug, bool System, string Html)[]
        {
            ("KVKK Aydınlatma Metni", KvkkSlug, true, note +
                "<h2>Veri Sorumlusu</h2><p>[FİRMA ÜNVANI], [ADRES], [E-POSTA]. Bu metin 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamında hazırlanmıştır.</p>" +
                "<h2>İşlenen Veriler</h2><p>Kimlik, iletişim, adres, müşteri işlemi ve işlem güvenliği verileri.</p>" +
                "<h2>İşleme Amaçları ve Hukuki Sebep</h2><p>Üyelik, sipariş, teslimat, faturalandırma ve yasal yükümlülükler. [HUKUKİ SEBEPLER]</p>" +
                "<h2>Aktarım</h2><p>[AKTARILAN ALICI GRUPLARI VE AMAÇLARI]</p>" +
                "<h2>Haklarınız</h2><p>Kanunun 11. maddesi kapsamındaki haklarınızı [E-POSTA] üzerinden kullanabilirsiniz.</p>"),
            ("Gizlilik Politikası", PrivacySlug, false, note +
                "<h2>Toplanan Bilgiler</h2><p>Ad soyad, e-posta, telefon, firma ve vergi bilgileri, teslimat adresi, sipariş bilgileri ve oturum için gerekli çerezler.</p>" +
                "<h2>Kullanım Amaçları</h2><p>Üyelik ve siparişlerin yönetimi, teslimat, müşteri desteği ve yasal yükümlülüklerin yerine getirilmesi.</p>" +
                "<h2>Paylaşım</h2><p>Veriler yalnızca teslimat, ödeme ve yasal zorunluluklar kapsamında [KARGO / ÖDEME SAĞLAYICILARI] ile paylaşılır.</p>"),
            ("Çerez Politikası", CookieSlug, false, note +
                "<p>Sitemiz yalnızca oturum, sepet ve güvenlik (CSRF) için zorunlu çerezler kullanır. Reklam veya izleme çerezi kullanılmaz.</p>"),
            ("Hakkımızda", "hakkimizda", false, note),
            ("Satış ve Teslimat Koşulları", SalesSlug, false, note +
                "<h2>Taraflar</h2><p>SATICI: [FİRMA ÜNVANI], [ADRES], [VERGİ DAİRESİ / VERGİ NO]. ALICI: sipariş sırasında bilgileri girilen müşteri.</p>" +
                "<h2>Ürün, Fiyat ve Ödeme</h2><p>Ürün adı, adedi, birim fiyatı ve toplam tutar sipariş özetinde yer alır. Ödeme yöntemi: havale/EFT.</p>" +
                "<h2>Teslimat</h2><p>[TESLİMAT SÜRESİ VE KARGO ŞARTLARI]</p>"),
            ("İade ve Değişim Koşulları", ReturnsSlug, false, note +
                "<h2>Genel</h2><p>Teslim aldığınız ürünleri kontrol etmenizi, hasarlı veya hatalı ürünleri teslim anında bize bildirmenizi rica ederiz.</p>" +
                "<h2>Süre</h2><p>[GÜN SAYISI] gün içinde, kullanılmamış ve orijinal ambalajında olan ürünler için.</p>"),
            ("Kullanım Koşulları", TermsSlug, false, note +
                "<h2>Toptan Satış</h2><p>Siparişler, ürün sayfalarında belirtilen minimum sipariş miktarı ve stok durumuna tabidir.</p>" +
                "<h2>Fikri Mülkiyet</h2><p>Sitedeki marka, logo, görsel ve metinler PRAFF LEATHER'a aittir; izinsiz kullanılamaz.</p>"),
            ("Sıkça Sorulan Sorular", "sikca-sorulan-sorular", false,
                "<h3>Perakende satış yapıyor musunuz?</h3><p>Hayır. PRAFF LEATHER yalnızca toptan satış yapar. Her ürünün sayfasında minimum sipariş adedi belirtilir.</p>" +
                "<h3>Minimum sipariş adedi nasıl hesaplanır?</h3><p>Minimum adet ürün bazındadır; aynı ürünün farklı renklerini toplayarak minimum adede ulaşabilirsiniz.</p>" +
                "<h3>Ödemeyi nasıl yapabilirim?</h3><p>Şu an ödeme havale/EFT ile alınmaktadır. Siparişiniz “Ödeme Bekleniyor” durumunda oluşturulur, ödeme onayından sonra hazırlanır.</p>" +
                "<h3>Siparişimi nasıl takip ederim?</h3><p>Hesabım › Siparişlerim sayfasından siparişinizin güncel durumunu görebilirsiniz.</p>")
        };
        var existing = await db.ContentPages.Select(p => p.Slug).ToListAsync();
        var order = 1;
        foreach (var d in defaults)
        {
            if (!existing.Contains(d.Slug))
                db.ContentPages.Add(new ContentPage { Title = d.Title, Slug = d.Slug, IsSystem = d.System, Content = Sanitize(d.Html), ShowInFooter = true, FooterOrder = order, IsActive = true });
            order++;
        }
        await db.SaveChangesAsync();
    }
}

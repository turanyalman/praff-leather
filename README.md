# PRAFF LEATHER

Deri ürünleri satan bir marka için geliştirilmiş, **toptan satış (B2B) odaklı** e-ticaret platformu.
ASP.NET Core 10 MVC, Entity Framework Core 10 ve SQL Server ile yazıldı; mağaza ve yönetim paneli aynı projede, ayrı oturumlarla çalışır.

![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET%20Core-MVC-5C2D91)
![EF Core](https://img.shields.io/badge/EF%20Core-10-6DB33F)
![SQL Server](https://img.shields.io/badge/SQL%20Server-CC2927)
![Tests](https://img.shields.io/badge/testler-44%20ba%C5%9Far%C4%B1l%C4%B1-brightgreen)

## Proje Hakkında

PRAFF LEATHER, deri kemer, cüzdan, çanta ve seyahat aksesuarları üreten bir markanın bayilere ve kurumsal müşterilere toptan satış yaptığı web sitesidir.

Perakende bir mağazadan farklı olarak:

- Müşteriler kayıt olduktan sonra **yönetici onayı** ile alışveriş yapabilir.
- Her ürünün **minimum sipariş adedi** ve **paket adedi** vardır; sepet bu kurallara göre doğrulanır.
- Ödeme **havale/EFT** ile alınır; sipariş "Ödeme Bekleniyor" durumunda oluşur ve yönetici onayıyla ilerler.
- Stok, **renk bazında** tutulur ve her hareket geçmişe kaydedilir.

Proje gerçek bir müşteri için geliştirildi ve canlıda çalışıyor. Geliştirme, test, yapılandırma ve paylaşımlı hostinge yayınlama süreçlerinin tamamı üzerinde çalıştım.

## Canlı Site

🔗 **[prafftoptan.com](https://prafftoptan.com)**

## Ekran Görüntüleri

<!--
Ekran görüntülerini docs/screenshots/ klasörüne ekleyip aşağıdaki satırların başındaki ve sonundaki yorum işaretlerini kaldırın.

| Ana sayfa | Ürün detayı |
|---|---|
| ![Ana sayfa](docs/screenshots/anasayfa.png) | ![Ürün detayı](docs/screenshots/urun-detay.png) |

| Yönetim paneli | Barkodla stok işlemleri |
|---|---|
| ![Yönetim paneli](docs/screenshots/admin-panel.png) | ![Barkod](docs/screenshots/barkod-stok.png) |
-->

*Ekran görüntüleri yakında eklenecek.*

## Özellikler

### Mağaza

- Kategori ve ürün listeleme, ürün detayında renk seçimi ve çoklu görsel
- Toptan satış kuralları: minimum sipariş adedi, paket adedi, renkler toplamına göre kontrol
- Sepet, adres defteri ve havale/EFT ile sipariş
- Müşteri hesabı: sipariş geçmişi, sipariş iptali, profil ve şifre değiştirme
- Kayıtta e-posta doğrulama kodu, şifremi unuttum / şifre sıfırlama
- Yönetici onaylı müşteri hesapları
- İletişim formu, yönetimden düzenlenen yasal sayfalar (KVKK, mesafeli satış sözleşmesi, SSS)
- `sitemap.xml` ve `robots.txt`

### Yönetim Paneli

- **Gösterge paneli:** aylık satış, müşteri ve sipariş özetleri, kritik stok uyarıları
- **Ürün ve kategori yönetimi:** görsel yükleme ve sıralama, renk varyantları, aktif/pasif durumu
- **Stok yönetimi:** renk bazında stok, hareket geçmişi (giriş, çıkış, sayım, sipariş), CSV dışa aktarma
- **Barkod ve QR:** EAN-13 / Code128 barkod üretimi, yazdırılabilir etiketler, telefon kamerasıyla barkod okutarak stok girişi/çıkışı ve sayım
- **Sipariş yönetimi:** durum ve ödeme takibi, sipariş notları, CSV dışa aktarma
- **Müşteri yönetimi:** onay, aktif/pasif, adresler, e-posta doğrulama, CSV dışa aktarma
- **E-posta merkezi:** müşterilere toplu/tekil e-posta, gönderim kayıtları, SMTP test gönderimi
- **İçerik ve ayarlar:** yasal sayfa düzenleyici, site ayarları, iletişim mesajları, yönetici notları, çoklu yönetici

### Güvenlik

- Müşteri ve yönetici için **ayrı oturum çerezleri** (yönetici çerezi yalnızca `/admin` yolunda, `SameSite=Strict`)
- Şifreler **PBKDF2** (HMAC-SHA512, 100.000 iterasyon) ile saklanır; şifre değişince eski oturumlar geçersiz olur (security stamp)
- Tüm formlarda **CSRF** koruması
- Giriş ve form gönderimlerinde **hız sınırı** (rate limiting)
- **Content-Security-Policy**, `X-Frame-Options`, `Referrer-Policy`, HSTS güvenlik başlıkları
- Görsel yüklemede uzantı + **dosya imzası (magic bytes)** kontrolü
- Yönetimden girilen HTML içerikler **HtmlSanitizer** ile temizlenir
- Gizli bilgiler (veritabanı, SMTP) kod deposunda tutulmaz

## Kullanılan Teknolojiler

### Backend
- C#
- .NET 10
- ASP.NET Core MVC
- Entity Framework Core 10
- Microsoft SQL Server

### Kütüphaneler ve Araçlar
- Microsoft.EntityFrameworkCore.SqlServer 10.0.15
- Microsoft.EntityFrameworkCore.Design 10.0.15
- MailKit 5.0.0
- HtmlSanitizer 11.0.26
- QRCoder 1.9.0
- html5-qrcode (tarayıcıda kamera ile barkod okuma)
- dotnet-ef 10.0.15

### Test Teknolojileri
- xUnit 2.9.3
- xunit.runner.visualstudio 3.1.4
- Microsoft.NET.Test.Sdk 17.14.1
- coverlet.collector 6.0.4
- ZXing.Net 0.16.11 (üretilen barkodların gerçekten okunabildiğini test etmek için)

### Geliştirme Araçları
- Visual Studio
- Git
- GitHub
- Claude Code

## Mimari

Proje, ASP.NET Core MVC mimarisi temel alınarak yapılandırılmıştır.

```text
praff-leather/
├── Controllers/          # Mağaza controller'ları
│   └── Admin/            # Yönetim paneli controller'ları
├── Data/
│   ├── AppDbContext.cs
│   ├── DbInitializer.cs  # Migration + başlangıç verisi + ilk yönetici
│   └── Migrations/
├── Models/               # Entity sınıfları
├── Services/             # İş mantığı (sepet, sipariş, stok, barkod, e-posta...)
├── ViewModels/
├── Views/
├── wwwroot/              # CSS, JavaScript, görseller
├── deploy/               # Veritabanı kurulum SQL betiği
├── tests/
│   └── PraffLeather.Tests/
├── Program.cs
├── appsettings.json
├── appsettings.Example.json
└── PraffLeather.csproj
```

### Katmanların Görevleri

- **Controllers:** HTTP isteklerini karşılar ve uygulama akışını yönetir.
- **Models:** Uygulamanın veri modellerini tanımlar.
- **Views:** Kullanıcı arayüzünü oluşturur.
- **Data:** Veritabanı bağlamı, migration'lar ve başlangıç verisi.
- **Services:** İş mantığını barındırır; controller'lar ince tutulur.
- **ViewModels:** Görünümlere aktarılacak ve formlardan gelen verileri düzenler.
- **wwwroot:** CSS, JavaScript, görseller ve statik dosyaları barındırır.
- **tests:** Otomatik testleri barındırır.

## Kurulum

### Gereksinimler

- .NET 10 SDK
- SQL Server veya SQL Server LocalDB (Visual Studio ile birlikte gelir)
- Git

### 1. Repoyu klonlayın

```bash
git clone https://github.com/turanyalman/praff-leather.git
cd praff-leather
```

### 2. Veritabanı ayarı

`appsettings.json` dosyası varsayılan olarak **LocalDB**'ye bağlanır; Windows'ta Visual Studio kuruluysa hiçbir şey değiştirmeniz gerekmez.

Farklı bir SQL Server kullanacaksanız `appsettings.Example.json` dosyasındaki örneğe bakın. Gerçek şifreleri `appsettings.json` içine değil, Git'e eklenmeyen `appsettings.Development.json` veya `appsettings.Production.json` dosyasına yazın.

Tablolar ve örnek ürünler uygulama ilk açıldığında **otomatik** oluşturulur; ayrıca migration komutu çalıştırmanız gerekmez.

### 3. İlk yönetici hesabıyla çalıştırın

Yönetici hesabı koda gömülü değildir. İlk açılışta ortam değişkenleriyle oluşturulur:

**PowerShell**
```powershell
$env:PRAFF_ADMIN_EMAIL = "admin@ornek.com"
$env:PRAFF_ADMIN_PASSWORD = "OrnekSifre2026"
dotnet run
```

**Bash**
```bash
PRAFF_ADMIN_EMAIL=admin@ornek.com PRAFF_ADMIN_PASSWORD=OrnekSifre2026 dotnet run
```

Şifre en az 10 karakter olmalı; büyük harf, küçük harf ve rakam içermelidir. Hesap oluştuktan sonra ortam değişkenlerine gerek kalmaz.

Uygulama `http://localhost:5273` adresinde açılır. Yönetim paneline aynı giriş sayfasından (`/hesap/giris`) yönetici bilgileriyle girilir.

### 4. E-posta (isteğe bağlı)

SMTP ayarları boşsa e-posta gönderilmez; doğrulama kodları ve şifre sıfırlama bağlantıları uygulama günlüğüne yazılır. Gerçek gönderim için `Smtp` bölümünü doldurun (örnek: `appsettings.Example.json`).

### 5. Testleri çalıştırın

```bash
dotnet test
```

## Karşılaştığım Zorluklar ve Çözümler

**1. Canlı sunucuda 500.30 hatası**
Proje yerelde sorunsuz çalışırken paylaşımlı hostinge yüklediğimde site açılmadı ve IIS yalnızca "500.30 – ASP.NET Core app failed to start" hatası verdi. Küçük bir tanı uygulaması yazarak sorunun barındırma firmasının uygulama havuzunun **32-bit** çalışmasından kaynaklandığını buldum; `win-x64` için derlenen paket 32-bit süreçte yüklenemiyordu. Yayın profilini **çalışma zamanı belirtmeyen (AnyCPU), framework-dependent** pakete çevirerek sorunu çözdüm. Ayrıca uygulama açılışta çökerse boş hata sayfası yerine nedeni (şifreleri gizleyerek) gösteren bir acil durum modu ekledim.

**2. Doğru şifreyle reddedilen SMTP girişi**
E-posta sunucusu doğru kullanıcı adı ve şifreye rağmen `535 Authentication failed` döndürüyordu. MailKit'in sunucunun ilan ettiği CRAM-MD5 yöntemini seçtiğini, sunucunun ise bu yöntemi düzgün desteklemediğini fark ettim. Bu yöntemleri devre dışı bırakıp `LOGIN/PLAIN` kullanmaya zorlayarak sorunu çözdüm.

**3. Toptan satış kuralları ve renk bazlı stok**
Minimum sipariş adedinin tek renk için değil **renklerin toplamı** için geçerli olması, stoğun ise her renk için ayrı tutulması gerekiyordu. Sepet, sipariş ve stok servislerini bu kurallara göre ayırdım ve her stok değişikliğini önceki/sonraki değerleriyle hareket geçmişine kaydettim.

**4. Barkodla stok yönetimi**
Depoda ürün sayımını hızlandırmak için telefon kamerasıyla barkod okutarak stok girişi/çıkışı yapılabilen bir ekran geliştirdim. Ürettiğim barkodların gerçekten okunabildiğinden emin olmak için ZXing.Net ile **üretilen barkodu geri okuyan** testler yazdım.

**5. Kodu GitHub'a güvenli şekilde taşımak**
Canlı projede veritabanı bilgileri, e-posta ayarları ve şifreleme anahtarları bulunuyordu. Paylaşmadan önce projenin temiz bir kopyasını çıkardım; gizli ayar dosyalarını, yüklenen dosyaları ve anahtarları `.gitignore` ile dışarıda bıraktım ve yapılandırma için `appsettings.Example.json` şablonu hazırladım.

## Yapay Zekâ Kullanımı

Projenin geliştirme sürecinde Claude Code yapay zekâ destekli geliştirme aracı olarak kullanılmıştır.

Yapay zekâdan kod geliştirme, hata analizi, proje organizasyonu, teknik araştırma ve geliştirme süreçlerinde destek alınmıştır.

Ancak amaç yalnızca kod üretmek değil, oluşturulan kodların çalışma mantığını anlamak, incelemek, test etmek ve gerektiğinde düzenlemektir.

Yapay zekâ destekli yazılım geliştirme araçlarının modern yazılım geliştirme süreçlerine entegrasyonu da bu projenin önemli çalışma alanlarından biridir.

## Geliştirici

**Turan Yalman**

GitHub: [@turanyalman](https://github.com/turanyalman)

Bu proje, backend geliştirme, C#, .NET ve yazılım mimarisi alanlarında kendimi geliştirme sürecimin bir parçasıdır.

---

*Proje geliştirme ve iyileştirme çalışmaları devam etmektedir.*

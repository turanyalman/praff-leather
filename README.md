# PRAFF LEATHER

Modern web teknolojileri kullanılarak geliştirilen, ASP.NET Core MVC tabanlı web uygulaması.

## Proje Hakkında

PRAFF LEATHER, deri ürünleri alanında faaliyet gösteren bir marka için geliştirilen web projesidir.

Projenin geliştirme sürecinde modern web mimarisi, veritabanı yönetimi, backend geliştirme, kullanıcı arayüzü ve uygulama güvenliği konularına odaklanılmıştır.

Bu proje kapsamında C# ve ASP.NET Core ekosistemini kullanarak gerçek bir web uygulamasının geliştirme, test, yapılandırma ve yayınlama süreçleri üzerinde çalışılmıştır.

## Canlı Site

Canlı site: [Buraya web sitesi adresini ekleyin]

## Ekran Görüntüleri

Projenin arayüzüne ait ekran görüntüleri bu bölümde paylaşılacaktır.

- Ana sayfa
- Ürün sayfaları
- Yönetim paneli
- Diğer uygulama ekranları

## Özellikler

Projenin mevcut sürümünde yer alan özellikler:

- ASP.NET Core MVC tabanlı web uygulaması
- Controller – Model – View mimarisi
- Entity Framework Core ile veritabanı işlemleri
- Microsoft SQL Server entegrasyonu
- Servis katmanı ile iş mantığının ayrıştırılması
- ViewModel kullanımı
- E-posta işlemleri için MailKit entegrasyonu
- QR kod oluşturma desteği
- HTML içeriklerinin temizlenmesi için HtmlSanitizer
- Otomatik test altyapısı

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
- dotnet-ef 10.0.15

### Test Teknolojileri
- xUnit 2.9.3
- xunit.runner.visualstudio 3.1.4
- Microsoft.NET.Test.Sdk 17.14.1
- coverlet.collector 6.0.4
- ZXing.Net 0.16.11

### Geliştirme Araçları
- Visual Studio
- Git
- GitHub
- Claude Code

## Mimari

Proje, ASP.NET Core MVC mimarisi temel alınarak yapılandırılmıştır.

Kod organizasyonu:

```text
PraffLeather/
│
├── Controllers/
├── Data/
├── Models/
├── Services/
├── ViewModels/
├── Views/
├── wwwroot/
├── Properties/
│
├── tests/
├── Program.cs
├── appsettings.json
└── PraffLeather.csproj
```

### Katmanların Görevleri

- **Controllers:** HTTP isteklerini karşılar ve uygulama akışını yönetir.
- **Models:** Uygulamanın veri modellerini tanımlar.
- **Views:** Kullanıcı arayüzünü oluşturur.
- **Data:** Veritabanı işlemlerini yönetir.
- **Services:** Uygulamanın iş mantığını ve servis işlemlerini barındırır.
- **ViewModels:** Görünümlere aktarılacak verileri düzenler.
- **wwwroot:** CSS, JavaScript, görseller ve statik dosyaları barındırır.
- **tests:** Otomatik testleri barındırır.

## Kurulum

Projeyi yerel bilgisayarınızda çalıştırmak için aşağıdaki adımları izleyebilirsiniz.

### Gereksinimler

- .NET 10 SDK
- Microsoft SQL Server
- Git
- Visual Studio veya Visual Studio Code

### 1. Repoyu klonlayın

```bash
git clone https://github.com/turanyalman/praff-leather.git
```

### 2. Proje dizinine girin

```bash
cd praff-leather
```

### 3. Bağımlılıkları yükleyin

```bash
dotnet restore
```

### 4. Veritabanı yapılandırmasını yapın

Kendi SQL Server bağlantı bilgilerinizi güvenli yapılandırma üzerinden tanımlayın.

Gerçek bağlantı bilgilerini GitHub üzerinde paylaşmayın.

### 5. Uygulamayı çalıştırın

```bash
dotnet run
```

Uygulama, terminalde belirtilen yerel adres üzerinden erişilebilir olacaktır.

### 6. Testleri çalıştırın

```bash
dotnet test
```

## Karşılaştığım Zorluklar ve Çözümler

Bu proje üzerinde çalışırken yalnızca arayüz geliştirmeye değil, backend tarafındaki sistemin nasıl çalıştığını anlamaya da odaklandım.

Çalışma sürecinde üzerinde durduğum konular:

- ASP.NET Core MVC mimarisini uygulama içerisinde kullanmak.
- Entity Framework Core ile veritabanı işlemlerini yönetmek.
- Controller, Model, View ve Service yapılarını birbirinden ayırmak.
- Uygulamanın yayınlama ve sunucu yapılandırma süreçlerini anlamak.
- Uygulama güvenliği ve hassas yapılandırma bilgilerinin korunmasına dikkat etmek.
- Test edilebilir ve sürdürülebilir bir proje yapısı oluşturmak.

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

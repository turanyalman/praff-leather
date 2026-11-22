namespace PraffLeather.Services;

/// <summary>
/// Güvenli görsel yükleme: yalnızca JPG/PNG/WEBP, en fazla 5 MB, dosya imzası (magic bytes) kontrolü,
/// rastgele dosya adı. Dosyalar wwwroot/uploads/{klasör} altına yazılır.
/// </summary>
public class ImageUploadService(IWebHostEnvironment env)
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedFolders = ["products", "categories", "site"];

    public async Task<(string? Url, string? Error)> SaveAsync(IFormFile? file, string folder)
    {
        if (file == null || file.Length == 0) return (null, "Dosya seçilmedi.");
        if (!AllowedFolders.Contains(folder)) return (null, "Geçersiz klasör.");
        if (file.Length > MaxBytes) return (null, "Görsel en fazla 5 MB olabilir.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) return (null, "Yalnızca JPG, PNG veya WEBP yükleyebilirsiniz.");

        var header = new byte[12];
        await using (var s = file.OpenReadStream())
        {
            var read = await s.ReadAsync(header);
            if (read < 12) return (null, "Geçersiz görsel dosyası.");
        }
        var detected = Detect(header);
        if (detected == null) return (null, "Dosya içeriği geçerli bir JPG, PNG veya WEBP görseli değil.");

        var dir = Path.Combine(env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(dir);
        var name = $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}{detected}";
        var path = Path.Combine(dir, name);
        await using (var fs = new FileStream(path, FileMode.CreateNew))
            await file.CopyToAsync(fs);
        return ($"/uploads/{folder}/{name}", null);
    }

    /// <summary>Yalnızca bu servisin yüklediği dosyaları siler.</summary>
    public void TryDelete(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/")) return;
        var full = Path.GetFullPath(Path.Combine(env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads"));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;
        try { if (File.Exists(full)) File.Delete(full); } catch { /* yok say */ }
    }

    private static string? Detect(byte[] h)
    {
        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return ".jpg";
        if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) return ".png";
        if (h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50) return ".webp";
        return null;
    }
}

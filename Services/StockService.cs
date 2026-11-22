using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;

namespace PraffLeather.Services;

public record StockResult(bool Success, string Message, int Before = 0, int After = 0, int? MovementId = null)
{
    public static StockResult Fail(string m) => new(false, m);
    public OpResult ToOp() => new(Success, Message);
}

public static class DbTransactions
{
    /// <summary>
    /// SQL Server bağlantı dayanıklılığı (EnableRetryOnFailure) açıkken elle transaction ancak
    /// yürütme stratejisi içinde açılabilir. Zaten bir transaction varsa ona katılır.
    /// <paramref name="commitIf"/> false dönerse geri alınır ve değişiklik izleyici temizlenir.
    /// </summary>
    public static async Task<T> InTransactionAsync<T>(this AppDbContext db, Func<Task<T>> work, Func<T, bool>? commitIf = null)
    {
        if (db.Database.CurrentTransaction != null) return await work();
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var result = await work();
                if (commitIf == null || commitIf(result)) await tx.CommitAsync();
                else { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
                return result;
            }
            catch
            {
                await tx.RollbackAsync();
                db.ChangeTracker.Clear();
                throw;
            }
        });
    }
}

/// <summary>
/// Stok değişikliklerinin TEK giriş noktası. Her değişiklik koşullu UPDATE ile atomik yapılır (stok asla negatife düşmez)
/// ve bir <see cref="StockMovement"/> kaydı oluşturur. Renkli (varyantlı) ürünlerde varyant stoğu ve ürün toplamı birlikte güncellenir.
/// </summary>
public class StockService(AppDbContext db)
{
    public const int MaxQuantity = 10_000_000;

    /// <summary>Stoğu işaretli miktar kadar değiştirir (+ giriş, − çıkış).</summary>
    public Task<StockResult> ChangeAsync(int productId, int? variantId, StockMovementType type, int delta, string? note, string? by, int? orderId = null)
    {
        if (delta == 0) return Task.FromResult(StockResult.Fail("Miktar sıfırdan farklı olmalıdır."));
        if (Math.Abs(delta) > MaxQuantity) return Task.FromResult(StockResult.Fail("Geçersiz miktar."));
        return db.InTransactionAsync(() => ApplyAsync(productId, variantId, type, delta, null, note, by, orderId), r => r.Success);
    }

    /// <summary>Stoğu verilen değere eşitler (sayım, elle düzeltme).</summary>
    public Task<StockResult> SetAsync(int productId, int? variantId, StockMovementType type, int value, string? note, string? by)
    {
        if (value < 0 || value > MaxQuantity) return Task.FromResult(StockResult.Fail("Geçersiz miktar."));
        return db.InTransactionAsync(() => ApplyAsync(productId, variantId, type, null, value, note, by, null), r => r.Success);
    }

    /// <summary>Bir hareketi tersine çevirir (barkod ekranındaki "Geri Al").</summary>
    public Task<StockResult> UndoAsync(int movementId, string? by)
    {
        return db.InTransactionAsync(async () =>
        {
            var m = await db.StockMovements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == movementId);
            if (m == null) return StockResult.Fail("Geri alınacak işlem bulunamadı.");
            if (m.Type is StockMovementType.Siparis or StockMovementType.SiparisIptal) return StockResult.Fail("Sipariş hareketleri buradan geri alınamaz.");
            if (m.Quantity == 0) return StockResult.Fail("Bu işlem stoğu değiştirmemiş.");
            return await ApplyAsync(m.ProductId, m.VariantId, StockMovementType.Duzeltme, -m.Quantity, null, $"Geri alındı (#{m.Id})", by, null);
        }, r => r.Success);
    }

    private async Task<StockResult> ApplyAsync(int productId, int? variantId, StockMovementType type, int? delta, int? setTo, string? note, string? by, int? orderId)
    {
        var now = DateTime.UtcNow;
        string? variantName = null;
        int before;

        if (variantId != null)
        {
            var v = await db.ProductColors.AsNoTracking().Where(c => c.Id == variantId && c.ProductId == productId)
                .Select(c => new { c.Name, c.StockQuantity }).FirstOrDefaultAsync();
            if (v == null) return StockResult.Fail("Ürün rengi/varyantı bulunamadı.");
            variantName = v.Name;
            before = v.StockQuantity;
        }
        else
        {
            var p = await db.Products.AsNoTracking().Where(x => x.Id == productId).Select(x => (int?)x.StockQuantity).FirstOrDefaultAsync();
            if (p == null) return StockResult.Fail("Ürün bulunamadı.");
            before = p.Value;
        }

        var change = delta ?? setTo!.Value - before;
        if (change == 0)
            return new StockResult(true, $"Stok zaten {Fmt.Number(before)} adet.", before, before);
        if (before + change < 0)
            return new StockResult(false, $"Stok yetersiz. Mevcut stok: {Fmt.Number(before)} adet.", before, before);

        int affected;
        if (variantId != null)
        {
            // Koşul: değişimden sonra negatif olmasın ve "sabitle" işleminde arada başka değişiklik olmasın.
            affected = await db.ProductColors
                .Where(c => c.Id == variantId && c.StockQuantity + change >= 0 && (setTo == null || c.StockQuantity == before))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.StockQuantity, c => c.StockQuantity + change));
            if (affected == 0) return StockResult.Fail("Stok bu sırada değişti veya yetersiz. Lütfen tekrar deneyin.");
            await db.Products.Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + change).SetProperty(p => p.StockUpdatedAt, now));
        }
        else
        {
            affected = await db.Products
                .Where(p => p.Id == productId && p.StockQuantity + change >= 0 && (setTo == null || p.StockQuantity == before))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + change).SetProperty(p => p.StockUpdatedAt, now));
            if (affected == 0) return StockResult.Fail("Stok bu sırada değişti veya yetersiz. Lütfen tekrar deneyin.");
        }

        var after = before + change;
        var movement = new StockMovement
        {
            ProductId = productId,
            VariantId = variantId,
            VariantName = variantName,
            Type = type,
            Quantity = change,
            StockBefore = before,
            StockAfter = after,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 300)],
            CreatedBy = by,
            OrderId = orderId,
            CreatedAt = now
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync();
        return new StockResult(true, $"Stok güncellendi: {Fmt.Number(before)} → {Fmt.Number(after)} adet.", before, after, movement.Id);
    }

    /// <summary>Renkli ürünlerde ürün toplamını varyant stoklarının toplamına eşitler (güvenlik ağı).</summary>
    public async Task SyncTotalAsync(int productId)
    {
        var hasVariants = await db.ProductColors.AnyAsync(c => c.ProductId == productId);
        if (!hasVariants) return;
        var sum = await db.ProductColors.Where(c => c.ProductId == productId).SumAsync(c => c.StockQuantity);
        await db.Products.Where(p => p.Id == productId && p.StockQuantity != sum)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, sum).SetProperty(p => p.StockUpdatedAt, DateTime.UtcNow));
    }
}

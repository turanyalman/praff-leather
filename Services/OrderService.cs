using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;

namespace PraffLeather.Services;

public class CheckoutData
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? CompanyName { get; set; }
    public string? TaxNumber { get; set; }
    public string AddressLine { get; set; } = "";
    public string City { get; set; } = "";
    public string District { get; set; } = "";
    public string? PostalCode { get; set; }
    public string? Notes { get; set; }
}

public record PlaceOrderResult(bool Success, string Message, Order? Order);

public static class PaymentMethods
{
    /// <summary>
    /// Şu an yalnızca manuel (havale/EFT) ödeme var. Sipariş "Ödeme Bekleniyor" durumunda oluşturulur;
    /// ödeme, yönetici panelinden elle "Ödendi" olarak işaretlenir. İleride sanal POS sağlayıcısı
    /// IPaymentProvider benzeri bir arayüzle buraya eklenebilir.
    /// </summary>
    public const string BankTransfer = "Havale / EFT (manuel onay)";
}

public class OrderService(AppDbContext db, StockService stock, SiteSettingsService settings, EmailService email, ILogger<OrderService> log)
{
    /// <summary>
    /// İzin verilen durum geçişleri. Kargoya verilen/teslim edilen sipariş iptal edilemez (iade ayrı süreçtir);
    /// iptal edilen sipariş yeniden açılamaz; durum geriye alınamaz.
    /// </summary>
    public static bool CanTransition(OrderStatus from, OrderStatus to) => from switch
    {
        OrderStatus.Pending => to is OrderStatus.Confirmed or OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Cancelled,
        OrderStatus.Confirmed => to is OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Cancelled,
        OrderStatus.Processing => to is OrderStatus.Shipped or OrderStatus.Cancelled,
        OrderStatus.Shipped => to is OrderStatus.Delivered,
        _ => false
    };

    public static IEnumerable<OrderStatus> NextStatuses(OrderStatus from) =>
        Enum.GetValues<OrderStatus>().Where(s => CanTransition(from, s));

    /// <summary>Müşteri yalnızca "Beklemede" siparişini kendisi iptal edebilir.</summary>
    public static bool CustomerCanCancel(Order o) => o.Status == OrderStatus.Pending;

    public async Task<PlaceOrderResult> PlaceOrderAsync(int userId, int cartId, CheckoutData data, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 40)
            return new(false, "Geçersiz form. Lütfen sayfayı yenileyip tekrar deneyin.", null);

        // Aynı form iki kez gönderildiyse ilk siparişi döndür.
        var existing = await db.Orders.FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey);
        if (existing != null)
            return existing.UserId == userId ? new(true, "Sipariş zaten oluşturuldu.", existing) : new(false, "Geçersiz istek.", null);

        var s = await settings.GetAsync();
        PlaceOrderResult result;
        try
        {
            result = await db.InTransactionAsync(() => PlaceInTransactionAsync(userId, cartId, data, idempotencyKey, s), r => r.Success);
        }
        catch (DbUpdateException ex)
        {
            log.LogWarning(ex, "Sipariş oluşturulamadı");
            db.ChangeTracker.Clear();
            var dup = await db.Orders.FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey && o.UserId == userId);
            return dup != null ? new(true, "Sipariş zaten oluşturuldu.", dup) : new(false, "Sipariş oluşturulamadı. Lütfen tekrar deneyin.", null);
        }

        if (result.Success && result.Order != null)
        {
            try { await email.NotifyOrderPlacedAsync(result.Order); }
            catch (Exception ex) { log.LogWarning(ex, "Sipariş e-postası gönderilemedi"); }
        }
        return result;
    }

    private async Task<PlaceOrderResult> PlaceInTransactionAsync(int userId, int cartId, CheckoutData data, string idempotencyKey, SiteSettings s)
    {
        {
            var items = await db.CartItems.Where(i => i.CartId == cartId)
                .Include(i => i.Product!).ThenInclude(p => p.Images)
                .Include(i => i.Product!).ThenInclude(p => p.Category)
                .Include(i => i.Product!).ThenInclude(p => p.Colors)
                .OrderBy(i => i.AddedAt).ThenBy(i => i.Id)
                .AsSplitQuery()
                .ToListAsync();
            if (items.Count == 0) return new(false, "Sepetiniz boş.", null);

            // Fiyat ve stok her zaman veritabanından okunur; tarayıcıdan gelen hiçbir tutara güvenilmez.
            foreach (var g in items.GroupBy(i => i.ProductId))
            {
                var p = g.First().Product!;
                var total = g.Sum(i => i.Quantity);
                if (!p.IsActive || !p.Category!.IsActive) return new(false, $"{p.Name} artık satışta değil. Lütfen sepetinizi güncelleyin.", null);
                if (total < p.MinOrderQuantity) return new(false, $"{p.Name} için minimum sipariş {Fmt.Number(p.MinOrderQuantity)} adettir.", null);
                if (p.Colors.Count == 0)
                {
                    if (total > p.StockQuantity) return new(false, $"{p.Name} için stok yetersiz (mevcut {Fmt.Number(p.StockQuantity)} adet).", null);
                }
                else foreach (var line in g)
                {
                    var v = p.Colors.FirstOrDefault(c => c.Name == line.ColorName);
                    if (v == null) return new(false, $"{p.Name} için seçilen renk ({line.ColorName}) artık yok. Lütfen sepetinizi güncelleyin.", null);
                    if (line.Quantity > v.StockQuantity) return new(false, $"{p.Name} — {v.Name} için stok yetersiz (mevcut {Fmt.Number(v.StockQuantity)} adet).", null);
                }
            }

            var order = new Order
            {
                OrderNumber = "TMP-" + Guid.NewGuid().ToString("N")[..12],
                UserId = userId,
                Status = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Awaiting,
                PaymentMethod = PaymentMethods.BankTransfer,
                FullName = data.FullName.Trim(),
                Email = data.Email.Trim(),
                Phone = data.Phone.Trim(),
                CompanyName = Clean(data.CompanyName),
                TaxNumber = Clean(data.TaxNumber),
                AddressLine = data.AddressLine.Trim(),
                City = data.City.Trim(),
                District = data.District.Trim(),
                PostalCode = Clean(data.PostalCode),
                Notes = Clean(data.Notes),
                IdempotencyKey = idempotencyKey,
                Items = items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product!.Name,
                    Sku = i.Product.Sku,
                    ColorName = i.ColorName,
                    ImageUrl = i.Product.MainImageUrl,
                    UnitPrice = i.Product.Price,
                    Quantity = i.Quantity,
                    LineTotal = i.Product.Price * i.Quantity
                }).ToList()
            };
            order.Subtotal = order.Items.Sum(i => i.LineTotal);
            order.ShippingCost = SiteSettingsService.ShippingFor(s, order.Subtotal);
            order.Total = order.Subtotal + order.ShippingCost;
            order.StatusHistory.Add(new OrderStatusHistory { Status = OrderStatus.Pending, ChangedBy = "Müşteri", Note = "Sipariş oluşturuldu." });
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            order.OrderNumber = $"PL-{order.Id:D6}";
            await db.SaveChangesAsync();

            // Stok düşümü: koşullu UPDATE ile, eşzamanlı siparişlerde bile negatife düşmez. Renkli ürünlerde renk stoğundan düşer.
            foreach (var line in items)
            {
                var p = line.Product!;
                var variant = p.Colors.FirstOrDefault(c => c.Name == line.ColorName);
                var r = await stock.ChangeAsync(p.Id, variant?.Id, StockMovementType.Siparis, -line.Quantity, $"Sipariş {order.OrderNumber}", "Sistem", order.Id);
                if (!r.Success)
                    return new(false, $"{p.Name}{(variant == null ? "" : " — " + variant.Name)}: {r.Message}", null);
            }

            await db.CartItems.Where(i => i.CartId == cartId).ExecuteDeleteAsync();
            return new(true, "Siparişiniz alındı.", order);
        }
    }

    public async Task<OpResult> UpdateStatusAsync(int orderId, OrderStatus newStatus, string? note, string changedBy, bool notifyCustomer = true)
    {
        if (!Enum.IsDefined(newStatus)) return OpResult.Fail("Geçersiz durum.");
        Order? changed = null;
        var result = await db.InTransactionAsync(async () =>
        {
            var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return OpResult.Fail("Sipariş bulunamadı.");
            if (order.Status == newStatus) return OpResult.Fail("Sipariş zaten bu durumda.");
            if (order.Status == OrderStatus.Cancelled) return OpResult.Fail("İptal edilmiş sipariş yeniden açılamaz. Gerekirse yeni sipariş oluşturulmalıdır.");
            if (!CanTransition(order.Status, newStatus))
                return OpResult.Fail(order.Status is OrderStatus.Shipped or OrderStatus.Delivered && newStatus == OrderStatus.Cancelled
                    ? "Kargoya verilmiş veya teslim edilmiş sipariş iptal edilemez. İade işlemini ayrıca yapın."
                    : $"\"{Fmt.StatusText(order.Status)}\" durumundan \"{Fmt.StatusText(newStatus)}\" durumuna geçilemez (durum geriye alınamaz).");

            var extra = "";
            if (newStatus == OrderStatus.Cancelled)
            {
                // İptalde stok iade edilir. Ürün veya renk silinmişse o satır atlanır (not düşülür).
                foreach (var line in order.Items.Where(i => i.ProductId != null))
                {
                    var p = await db.Products.AsNoTracking().Include(x => x.Colors).FirstOrDefaultAsync(x => x.Id == line.ProductId);
                    if (p == null) continue;
                    var variant = p.Colors.FirstOrDefault(c => c.Name == line.ColorName);
                    if (p.Colors.Count > 0 && variant == null) { extra += $" {p.Name} — {line.ColorName} rengi silindiği için stoğa iade edilemedi."; continue; }
                    var r = await stock.ChangeAsync(p.Id, variant?.Id, StockMovementType.SiparisIptal, line.Quantity, $"Sipariş {order.OrderNumber} iptali", changedBy, order.Id);
                    if (!r.Success) return r.ToOp();
                }
                if (order.PaymentStatus == PaymentStatus.Paid)
                {
                    order.PaymentStatus = PaymentStatus.Refunded;
                    extra += " Ödeme durumu \"İade Edildi\" olarak işaretlendi; iadeyi bankadan yapmayı unutmayın.";
                }
            }

            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;
            db.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = order.Id, Status = newStatus, ChangedBy = changedBy, Note = Clean(note) });
            await db.SaveChangesAsync();
            changed = order;
            return OpResult.Ok($"Sipariş durumu \"{Fmt.StatusText(newStatus)}\" olarak güncellendi.{extra}");
        }, r => r.Success);

        if (result.Success && changed != null && notifyCustomer)
        {
            try { await email.NotifyOrderStatusAsync(changed, note); }
            catch (Exception ex) { log.LogWarning(ex, "Durum e-postası gönderilemedi"); }
        }
        return result;
    }

    /// <summary>Müşterinin kendi "Beklemede" siparişini iptal etmesi.</summary>
    public async Task<OpResult> CancelByCustomerAsync(int userId, string orderNumber)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderNumber == orderNumber && o.UserId == userId);
        if (order == null) return OpResult.Fail("Sipariş bulunamadı.");
        if (!CustomerCanCancel(order)) return OpResult.Fail("Bu sipariş artık iptal edilemez. Lütfen bizimle iletişime geçin.");
        var r = await UpdateStatusAsync(order.Id, OrderStatus.Cancelled, "Müşteri tarafından iptal edildi.", "Müşteri", notifyCustomer: true);
        if (r.Success)
        {
            try { await email.NotifyAdminOrderCancelledByCustomerAsync(order); }
            catch (Exception ex) { log.LogWarning(ex, "İptal bildirimi gönderilemedi"); }
            return OpResult.Ok("Siparişiniz iptal edildi.");
        }
        return r;
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

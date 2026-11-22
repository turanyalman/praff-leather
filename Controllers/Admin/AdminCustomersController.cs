using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PraffLeather.Data;
using PraffLeather.Models;
using PraffLeather.Services;
using PraffLeather.ViewModels;

namespace PraffLeather.Controllers.Admin;

public class AdminCustomersController(AppDbContext db, PasswordService passwords, EmailService email, ILogger<AdminCustomersController> log) : AdminBaseController
{
    public record CustomerRow(User User, int Orders, decimal Spent);

    [HttpGet("/admin/musteriler.csv")]
    public async Task<IActionResult> Csv()
    {
        var list = await db.Users.AsNoTracking().OrderBy(u => u.CompanyName).ThenBy(u => u.LastName)
            .Select(u => new
            {
                u.CompanyName, u.FirstName, u.LastName, u.Email, u.EmailConfirmed, u.Phone, u.TaxOffice, u.TaxNumber, u.City, u.IsApproved, u.IsActive, u.CreatedAt, u.LastLoginAt,
                Orders = u.Orders.Count(),
                Spent = u.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => (decimal?)o.Total) ?? 0
            }).ToListAsync();
        var bytes = Fmt.CsvFile(
            ["Firma", "Ad", "Soyad", "E-posta", "E-posta Doğrulandı", "Telefon", "Vergi Dairesi", "Vergi No", "İl", "Onaylı", "Aktif", "Kayıt", "Son Giriş", "Sipariş", "Toplam Alışveriş"],
            list.Select(u => new object?[] { u.CompanyName, u.FirstName, u.LastName, u.Email, u.EmailConfirmed ? "Evet" : "Hayır", u.Phone, u.TaxOffice, u.TaxNumber, u.City,
                u.IsApproved ? "Evet" : "Hayır", u.IsActive ? "Evet" : "Hayır", u.CreatedAt, u.LastLoginAt, u.Orders, u.Spent }));
        return File(bytes, "text/csv; charset=utf-8", $"musteriler-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    /// <summary>Müşterinin e-postasını yönetici elle doğrular (SMTP yokken veya telefonla teyit edildiğinde).</summary>
    [HttpPost("/admin/musteriler/{id:int}/eposta-dogrula")]
    public async Task<IActionResult> ConfirmEmail(int id)
    {
        var u = await db.Users.FindAsync(id);
        if (u == null) return NotFound();
        u.EmailConfirmed = true;
        u.EmailConfirmedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Ok($"{u.Email} doğrulandı olarak işaretlendi.");
        return Redirect($"/admin/musteriler/{id}");
    }

    [HttpPost("/admin/musteriler/{id:int}/kod-gonder")]
    public async Task<IActionResult> SendCode(int id)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();
        var r = await email.SendVerificationCodeAsync(u);
        if (r.Success) Ok($"Doğrulama kodu {u.Email} adresine gönderildi."); else Err($"Kod gönderilemedi: {r.Error}");
        return Redirect($"/admin/musteriler/{id}");
    }

    [HttpGet("/admin/musteriler")]
    public async Task<IActionResult> Index(string? q, string? durum, int sayfa = 1)
    {
        ViewData["Title"] = "Müşteriler";
        ViewData["Nav"] = "customers";
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(u => EF.Functions.Like(u.FirstName + " " + u.LastName, like) || EF.Functions.Like(u.Email, like) || EF.Functions.Like(u.Phone ?? "", like)
                || EF.Functions.Like(u.CompanyName ?? "", like) || EF.Functions.Like(u.TaxNumber ?? "", like) || EF.Functions.Like(u.City ?? "", like));
        }
        query = durum switch
        {
            "onay" => query.Where(u => !u.IsApproved && u.IsActive),
            "onayli" => query.Where(u => u.IsApproved),
            "aktif" => query.Where(u => u.IsActive),
            "pasif" => query.Where(u => !u.IsActive),
            _ => query
        };
        const int size = 30;
        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        sayfa = Math.Clamp(sayfa, 1, pages);
        // Şifre özeti gibi hassas alanlar seçilmez.
        var rows = await query.OrderBy(u => u.IsApproved).ThenByDescending(u => u.CreatedAt).Skip((sayfa - 1) * size).Take(size)
            .Select(u => new
            {
                U = new User { Id = u.Id, FirstName = u.FirstName, LastName = u.LastName, Email = u.Email, Phone = u.Phone, CompanyName = u.CompanyName, TaxNumber = u.TaxNumber, TaxOffice = u.TaxOffice, City = u.City, CreatedAt = u.CreatedAt, IsActive = u.IsActive, IsApproved = u.IsApproved, LastLoginAt = u.LastLoginAt, EmailConfirmed = u.EmailConfirmed },
                Orders = u.Orders.Count(),
                Spent = u.Orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => (decimal?)o.Total) ?? 0
            }).ToListAsync();
        ViewData["q"] = q; ViewData["durum"] = durum;
        ViewData["PendingCount"] = await db.Users.CountAsync(u => !u.IsApproved && u.IsActive);
        return View(new Paged<CustomerRow> { Items = rows.Select(r => new CustomerRow(r.U, r.Orders, r.Spent)).ToList(), Page = sayfa, TotalPages = pages, Total = total });
    }

    [HttpGet("/admin/musteriler/yeni")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Yeni Müşteri";
        ViewData["Nav"] = "customers";
        return View("Detail", new AdminCustomerViewModel { Form = new AdminCustomerForm { IsActive = true, IsApproved = true } });
    }

    [HttpGet("/admin/musteriler/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var vm = await LoadAsync(id);
        if (vm == null) return NotFound();
        var u = vm.User!;
        vm.Form = new AdminCustomerForm
        {
            Id = u.Id, FirstName = u.FirstName, LastName = u.LastName, Email = u.Email, Phone = u.Phone, CompanyName = u.CompanyName,
            TaxOffice = u.TaxOffice, TaxNumber = u.TaxNumber, City = u.City, IsActive = u.IsActive, IsApproved = u.IsApproved, AdminNotes = u.AdminNotes
        };
        return View(vm);
    }

    private async Task<AdminCustomerViewModel?> LoadAsync(int id)
    {
        var u = await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new User
            {
                Id = x.Id, FirstName = x.FirstName, LastName = x.LastName, Email = x.Email, Phone = x.Phone, CompanyName = x.CompanyName, TaxNumber = x.TaxNumber,
                TaxOffice = x.TaxOffice, City = x.City, CreatedAt = x.CreatedAt, IsActive = x.IsActive, IsApproved = x.IsApproved, ApprovedAt = x.ApprovedAt,
                ApprovedBy = x.ApprovedBy, LastLoginAt = x.LastLoginAt, AdminNotes = x.AdminNotes, EmailConfirmed = x.EmailConfirmed, EmailConfirmedAt = x.EmailConfirmedAt
            }).FirstOrDefaultAsync();
        if (u == null) return null;
        ViewData["Title"] = u.FullName;
        ViewData["Nav"] = "customers";
        var orders = await db.Orders.AsNoTracking().Where(o => o.UserId == id).Include(o => o.Items).OrderByDescending(o => o.CreatedAt).ToListAsync();
        return new AdminCustomerViewModel
        {
            User = u,
            Addresses = await db.Addresses.AsNoTracking().Where(a => a.UserId == id).OrderByDescending(a => a.IsDefault).ToListAsync(),
            Orders = orders,
            Spent = orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.Total)
        };
    }

    [HttpPost("/admin/musteriler/kaydet")]
    public async Task<IActionResult> Save([Bind(Prefix = "Form")] AdminCustomerForm form)
    {
        var isNew = form.Id == 0;
        var emailNorm = form.Email?.Trim().ToLowerInvariant() ?? "";
        if (await db.Users.AnyAsync(u => u.Email == emailNorm && u.Id != form.Id))
            ModelState.AddModelError("Form.Email", "Bu e-posta başka bir müşteride kayıtlı.");
        if (isNew && string.IsNullOrEmpty(form.NewPassword))
            ModelState.AddModelError("Form.NewPassword", "Yeni müşteri için şifre belirleyin.");
        if (!string.IsNullOrEmpty(form.NewPassword) && PasswordService.StrengthError(form.NewPassword) is { } pe)
            ModelState.AddModelError("Form.NewPassword", pe);

        if (!ModelState.IsValid)
        {
            form.NewPassword = null;
            var vm = isNew ? new AdminCustomerViewModel() : await LoadAsync(form.Id);
            if (vm == null) return NotFound();
            vm.Form = form;
            ViewData["Title"] = isNew ? "Yeni Müşteri" : vm.User!.FullName;
            ViewData["Nav"] = "customers";
            return View("Detail", vm);
        }

        User u;
        if (isNew)
        {
            // Yöneticinin eklediği müşterinin e-postası doğrulanmış sayılır.
            u = new User { CreatedAt = DateTime.UtcNow, EmailConfirmed = true, EmailConfirmedAt = DateTime.UtcNow };
            db.Users.Add(u);
        }
        else
        {
            var found = await db.Users.FirstOrDefaultAsync(x => x.Id == form.Id);
            if (found == null) return NotFound();
            u = found;
        }

        var wasApproved = !isNew && u.IsApproved;
        var emailChanged = !isNew && u.Email != emailNorm;
        u.FirstName = form.FirstName.Trim();
        u.LastName = form.LastName.Trim();
        u.Email = emailNorm;
        u.Phone = Clean(form.Phone);
        u.CompanyName = Clean(form.CompanyName);
        u.TaxOffice = Clean(form.TaxOffice);
        u.TaxNumber = Clean(form.TaxNumber);
        u.City = Clean(form.City);
        u.AdminNotes = Clean(form.AdminNotes);
        var deactivated = u.IsActive && !form.IsActive;
        u.IsActive = form.IsActive;
        if (form.IsApproved && !u.IsApproved) { u.ApprovedAt = DateTime.UtcNow; u.ApprovedBy = AdminEmail; }
        if (!form.IsApproved) { u.ApprovedAt = null; u.ApprovedBy = null; }
        u.IsApproved = form.IsApproved;

        var pwChanged = !string.IsNullOrEmpty(form.NewPassword);
        if (pwChanged) u.PasswordHash = passwords.Hash(form.NewPassword!);
        // Şifre, e-posta değişince veya hesap pasife alınınca açık oturumlar kapanır.
        if (pwChanged || emailChanged || deactivated) u.SecurityStamp = Guid.NewGuid().ToString("N");

        await db.SaveChangesAsync();
        if (!isNew && !wasApproved && u.IsApproved) await NotifyApprovedAsync(u);
        Ok(isNew ? "Müşteri oluşturuldu." : "Müşteri bilgileri kaydedildi." + (pwChanged ? " Şifre güncellendi; müşterinin açık oturumları kapatıldı." : ""));
        return Redirect($"/admin/musteriler/{u.Id}");
    }

    [HttpPost("/admin/musteriler/{id:int}/onay")]
    public async Task<IActionResult> Approve(int id, bool approve, string? returnUrl)
    {
        var u = await db.Users.FindAsync(id);
        if (u == null) return NotFound();
        if (approve && !u.IsApproved)
        {
            u.IsApproved = true; u.IsActive = true; u.ApprovedAt = DateTime.UtcNow; u.ApprovedBy = AdminEmail;
            await db.SaveChangesAsync();
            await NotifyApprovedAsync(u);
            Ok($"{u.CompanyName ?? u.FullName} onaylandı; artık ürün ve fiyatları görebilir.");
        }
        else if (!approve)
        {
            // Reddet: onay kaldırılır ve hesap pasife alınır.
            u.IsApproved = false; u.ApprovedAt = null; u.ApprovedBy = null; u.IsActive = false;
            u.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
            Ok($"{u.CompanyName ?? u.FullName} başvurusu reddedildi; hesap pasife alındı.");
        }
        return Redirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith("/admin") ? returnUrl : $"/admin/musteriler/{id}");
    }

    [HttpPost("/admin/musteriler/{id:int}/durum")]
    public async Task<IActionResult> Toggle(int id)
    {
        var u = await db.Users.FindAsync(id);
        if (u == null) return NotFound();
        u.IsActive = !u.IsActive;
        u.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        Ok(u.IsActive ? $"{u.FullName} hesabı aktifleştirildi." : $"{u.FullName} hesabı pasife alındı; giriş yapamaz.");
        return Redirect($"/admin/musteriler/{id}");
    }

    [HttpPost("/admin/musteriler/{id:int}/sil")]
    public async Task<IActionResult> Delete(int id)
    {
        var u = await db.Users.FindAsync(id);
        if (u == null) return NotFound();
        if (await db.Orders.AnyAsync(o => o.UserId == id))
        {
            Err("Siparişi olan müşteri silinemez (sipariş geçmişi korunur). Bunun yerine hesabı pasife alın.");
            return Redirect($"/admin/musteriler/{id}");
        }
        db.Users.Remove(u);
        await db.SaveChangesAsync();
        Ok($"{u.FullName} silindi.");
        return Redirect("/admin/musteriler");
    }

    // ---------------- Adresler
    [HttpGet("/admin/musteriler/{id:int}/adres/{addressId:int}")]
    public async Task<IActionResult> EditAddress(int id, int addressId)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();
        AddressForm form;
        if (addressId == 0) form = new AddressForm { FullName = u.FullName, Phone = u.Phone ?? "", City = u.City ?? "" };
        else
        {
            var a = await db.Addresses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == addressId && x.UserId == id);
            if (a == null) return NotFound();
            form = new AddressForm { Id = a.Id, Title = a.Title, FullName = a.FullName, Phone = a.Phone, City = a.City, District = a.District, PostalCode = a.PostalCode, AddressLine = a.AddressLine, IsDefault = a.IsDefault };
        }
        ViewData["Title"] = "Adres";
        ViewData["Nav"] = "customers";
        ViewData["Customer"] = u;
        return View("Address", form);
    }

    [HttpPost("/admin/musteriler/{id:int}/adres")]
    public async Task<IActionResult> SaveAddress(int id, AddressForm form)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (u == null) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Adres"; ViewData["Nav"] = "customers"; ViewData["Customer"] = u;
            return View("Address", form);
        }
        Address a;
        if (form.Id == 0) { a = new Address { UserId = id }; db.Addresses.Add(a); }
        else
        {
            var found = await db.Addresses.FirstOrDefaultAsync(x => x.Id == form.Id && x.UserId == id);
            if (found == null) return NotFound();
            a = found;
        }
        a.Title = form.Title.Trim(); a.FullName = form.FullName.Trim(); a.Phone = form.Phone.Trim(); a.City = form.City.Trim();
        a.District = form.District.Trim(); a.PostalCode = Clean(form.PostalCode); a.AddressLine = form.AddressLine.Trim();
        var hasDefault = await db.Addresses.AnyAsync(x => x.UserId == id && x.IsDefault && x.Id != a.Id);
        if (form.IsDefault || !hasDefault)
        {
            await db.Addresses.Where(x => x.UserId == id && x.Id != a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false));
            a.IsDefault = true;
        }
        else a.IsDefault = false;
        await db.SaveChangesAsync();
        Ok("Adres kaydedildi.");
        return Redirect($"/admin/musteriler/{id}#adresler");
    }

    [HttpPost("/admin/musteriler/{id:int}/adres/{addressId:int}/sil")]
    public async Task<IActionResult> DeleteAddress(int id, int addressId)
    {
        var n = await db.Addresses.Where(x => x.Id == addressId && x.UserId == id).ExecuteDeleteAsync();
        if (n == 0) return NotFound();
        Ok("Adres silindi.");
        return Redirect($"/admin/musteriler/{id}#adresler");
    }

    private async Task NotifyApprovedAsync(User u)
    {
        try { await email.NotifyApprovedAsync(u, AdminEmail); }
        catch (Exception ex) { log.LogWarning(ex, "Onay e-postası gönderilemedi"); }
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

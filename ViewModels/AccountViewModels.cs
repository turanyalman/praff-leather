using System.ComponentModel.DataAnnotations;
using PraffLeather.Models;

namespace PraffLeather.ViewModels;

public class LoginForm
{
    [Required(ErrorMessage = "E-posta gerekli."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(160)]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Şifre gerekli."), StringLength(128)]
    public string Password { get; set; } = "";
    public bool RememberMe { get; set; } = true;
    public string? ReturnUrl { get; set; }
}

public class RegisterForm
{
    [Required(ErrorMessage = "Ad gerekli."), StringLength(60)]
    public string FirstName { get; set; } = "";
    [Required(ErrorMessage = "Soyad gerekli."), StringLength(60)]
    public string LastName { get; set; } = "";
    [Required(ErrorMessage = "E-posta gerekli."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(160)]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Telefon gerekli."), RegularExpression(@"^[0-9+()\s-]{10,20}$", ErrorMessage = "Geçerli bir telefon numarası girin.")]
    public string Phone { get; set; } = "";
    [Required(ErrorMessage = "Firma adı gerekli."), StringLength(160)] public string? CompanyName { get; set; }
    [Required(ErrorMessage = "Vergi dairesi gerekli."), StringLength(80)] public string? TaxOffice { get; set; }
    [Required(ErrorMessage = "Vergi / T.C. kimlik numarası gerekli."), RegularExpression(@"^\d{10,11}$", ErrorMessage = "Vergi no 10, T.C. kimlik no 11 haneli olmalı.")]
    public string? TaxNumber { get; set; }
    [Required(ErrorMessage = "İl gerekli."), StringLength(60)] public string? City { get; set; }
    [Required(ErrorMessage = "Şifre gerekli.")]
    public string Password { get; set; } = "";
    [Required(ErrorMessage = "Şifre tekrarı gerekli."), Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = "";
    [Range(typeof(bool), "true", "true", ErrorMessage = "Kullanım koşullarını ve gizlilik politikasını kabul etmelisiniz.")]
    public bool AcceptTerms { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "KVKK Aydınlatma Metni'ni okuduğunuzu onaylamanız gerekir.")]
    public bool AcceptKvkk { get; set; }
    public string? ReturnUrl { get; set; }
}

public class VerifyEmailForm
{
    [Required(ErrorMessage = "Doğrulama kodunu girin."), RegularExpression(@"^\s*\d{3}\s?\d{3}\s*$", ErrorMessage = "Kod 6 haneli olmalıdır.")]
    public string Code { get; set; } = "";
}

public class ForgotForm
{
    [Required(ErrorMessage = "E-posta gerekli."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string Email { get; set; } = "";
}

public class ResetForm
{
    [Required] public string Token { get; set; } = "";
    [Required(ErrorMessage = "Yeni şifre gerekli.")] public string Password { get; set; } = "";
    [Required(ErrorMessage = "Şifre tekrarı gerekli."), Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = "";
}

public class ChangePasswordForm
{
    [Required(ErrorMessage = "Mevcut şifre gerekli.")] public string CurrentPassword { get; set; } = "";
    [Required(ErrorMessage = "Yeni şifre gerekli.")] public string NewPassword { get; set; } = "";
    [Required(ErrorMessage = "Şifre tekrarı gerekli."), Compare(nameof(NewPassword), ErrorMessage = "Yeni şifreler eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = "";
}

public class ProfileForm
{
    [Required(ErrorMessage = "Ad gerekli."), StringLength(60)] public string FirstName { get; set; } = "";
    [Required(ErrorMessage = "Soyad gerekli."), StringLength(60)] public string LastName { get; set; } = "";
    [Required(ErrorMessage = "Telefon gerekli."), RegularExpression(@"^[0-9+()\s-]{10,20}$", ErrorMessage = "Geçerli bir telefon numarası girin.")]
    public string Phone { get; set; } = "";
    [StringLength(160)] public string? CompanyName { get; set; }
    [StringLength(30)] public string? TaxNumber { get; set; }
    [StringLength(80)] public string? TaxOffice { get; set; }
    [StringLength(60)] public string? City { get; set; }
    public string Email { get; set; } = "";
}

public class AddressForm
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Adres başlığı gerekli."), StringLength(60)] public string Title { get; set; } = "";
    [Required(ErrorMessage = "Ad soyad gerekli."), StringLength(120)] public string FullName { get; set; } = "";
    [Required(ErrorMessage = "Telefon gerekli."), RegularExpression(@"^[0-9+()\s-]{10,20}$", ErrorMessage = "Geçerli bir telefon numarası girin.")]
    public string Phone { get; set; } = "";
    [Required(ErrorMessage = "İl gerekli."), StringLength(60)] public string City { get; set; } = "";
    [Required(ErrorMessage = "İlçe gerekli."), StringLength(60)] public string District { get; set; } = "";
    [RegularExpression(@"^\d{5}$", ErrorMessage = "Posta kodu 5 haneli olmalı.")] public string? PostalCode { get; set; }
    [Required(ErrorMessage = "Adres gerekli."), StringLength(400, MinimumLength = 10, ErrorMessage = "Adres en az 10 karakter olmalı.")]
    public string AddressLine { get; set; } = "";
    public bool IsDefault { get; set; }
}

public class AccountDashboardViewModel
{
    public User User { get; init; } = null!;
    public int OrderCount { get; init; }
    public int ItemCount { get; init; }
    public decimal TotalSpent { get; init; }
    public List<Order> RecentOrders { get; init; } = new();
}

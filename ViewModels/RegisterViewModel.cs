using System.ComponentModel.DataAnnotations;

namespace StudyConnect.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100)]
    [Display(Name = "Họ tên")]
    public string HoTen { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(150)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu cần ít nhất 6 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string MatKhau { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
    [DataType(DataType.Password)]
    [Compare(nameof(MatKhau), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    [Display(Name = "Xác nhận mật khẩu")]
    public string XacNhanMatKhau { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? SoDienThoai { get; set; }

    [Required]
    [Display(Name = "Vai trò")]
    public string VaiTro { get; set; } = AccountRoles.SinhVien;
}

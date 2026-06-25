using System.ComponentModel.DataAnnotations;

namespace StudyConnect.ViewModels;

public class TaiKhoanEditViewModel
{
    public int MaTaiKhoan { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(100)]
    [Display(Name = "Họ tên")]
    public string HoTen { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(150)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Số điện thoại")]
    public string? SoDienThoai { get; set; }

    [Required]
    [Display(Name = "Vai trò")]
    public string VaiTro { get; set; } = AccountRoles.SinhVien;

    [Required]
    [Display(Name = "Trạng thái")]
    public string TrangThai { get; set; } = "Hoạt động";

    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới cần ít nhất 6 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string? MatKhauMoi { get; set; }
}

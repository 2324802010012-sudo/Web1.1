using System.ComponentModel.DataAnnotations;

namespace StudyConnect.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email hoặc số điện thoại.")]
    [Display(Name = "Email hoặc số điện thoại")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string MatKhau { get; set; } = string.Empty;
}

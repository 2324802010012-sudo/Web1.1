using System.ComponentModel.DataAnnotations;

namespace StudyConnect.ViewModels;

public class SinhVienHoSoViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mã số sinh viên.")]
    [StringLength(50)]
    public string Mssv { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập chuyên ngành.")]
    [StringLength(100)]
    public string ChuyenNganh { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Lop { get; set; }

    [Range(0, 4, ErrorMessage = "GPA phải nằm trong khoảng 0 đến 4.")]
    public decimal? Gpa { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập kỹ năng hiện có.")]
    public string KyNang { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mục tiêu học.")]
    public string MucTieuHoc { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn ít nhất một lịch rảnh.")]
    public List<string> LichRanhDaChon { get; set; } = [];

    public string? ReturnUrl { get; set; }
}

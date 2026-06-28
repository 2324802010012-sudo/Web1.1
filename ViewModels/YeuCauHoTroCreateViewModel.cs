using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace StudyConnect.ViewModels;

public class YeuCauHoTroCreateViewModel
{
    public int? MentorId { get; set; }

    public string? MentorDaChon { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn lĩnh vực cần hỗ trợ.")]
    public int MaLinhVuc { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập chủ đề hoặc công nghệ.")]
    [StringLength(200)]
    public string ChuDeCongNghe { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề yêu cầu.")]
    [StringLength(200)]
    public string TieuDe { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng mô tả chi tiết vấn đề.")]
    public string MoTaChiTiet { get; set; } = string.Empty;

    public List<string> PhanCanHoTro { get; set; } = [];

    [StringLength(500)]
    public string? DaThuNhungGi { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn mức độ cần hỗ trợ.")]
    [StringLength(50)]
    public string MucDoCanHoTro { get; set; } = "Trung bình";

    public IEnumerable<SelectListItem> LinhVucOptions { get; set; } = [];

    public IEnumerable<SelectListItem> MucDoOptions { get; set; } = [];

    public List<MentorSuggestionViewModel> MentorGoiY { get; set; } = [];
}

public class MentorSuggestionViewModel
{
    public int MaHuongDan { get; set; }

    public string HoTen { get; set; } = string.Empty;

    public string ChuyenMon { get; set; } = string.Empty;

    public decimal DiemUyTin { get; set; }

    public decimal DiemDanhGia { get; set; }

    public int SoLuotDanhGia { get; set; }

    public decimal DiemPhuHop { get; set; }

    public decimal ChuyenMonScore { get; set; }

    public decimal LichRanhScore { get; set; }

    public decimal DiemUyTinScore { get; set; }

    public decimal DanhGiaScore { get; set; }

    public decimal TuongDongScore { get; set; }

    public int TaiHienTai { get; set; }

    public List<string> LyDoGoiY { get; set; } = [];

    public List<string> LichRanhChung { get; set; } = [];
}

public class SharedScheduleSlotViewModel
{
    public string Label { get; set; } = string.Empty;

    public DateTime NgayHoc { get; set; }

    public TimeSpan GioBatDau { get; set; }

    public TimeSpan GioKetThuc { get; set; }
}


using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace StudyConnect.ViewModels;

public class MentorApplicationViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn lĩnh vực muốn hướng dẫn.")]
    public int MaLinhVuc { get; set; }

    [Range(0, 10, ErrorMessage = "Điểm môn phải từ 0 đến 10.")]
    public decimal? DiemMon { get; set; }

    [StringLength(255)]
    public string? MinhChung { get; set; }

    public IFormFile? BangChungFile { get; set; }

    public string? BangChungDaTai { get; set; }

    [Required(ErrorMessage = "Vui lòng mô tả kinh nghiệm hoặc lý do muốn làm mentor.")]
    public string LyDo { get; set; } = string.Empty;

    public List<string> LichRanhDaChon { get; set; } = [];

    public IEnumerable<SelectListItem> LinhVucOptions { get; set; } = [];

    public string? TrangThaiHienTai { get; set; }
}

public class MentorAvailabilityViewModel
{
    public List<string> LichRanhDaChon { get; set; } = [];

    public int? ThuTuyChinh { get; set; }

    public TimeSpan? GioBatDauTuyChinh { get; set; }

    public TimeSpan? GioKetThucTuyChinh { get; set; }
}

public class DashboardNotificationViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string Tone { get; set; } = "blue";
}

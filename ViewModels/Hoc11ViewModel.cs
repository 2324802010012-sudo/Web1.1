using Microsoft.AspNetCore.Mvc.Rendering;

namespace StudyConnect.ViewModels;

public class Hoc11ViewModel
{
    public int? SelectedLinhVucId { get; set; }

    public string? Keyword { get; set; }

    public int MentorCount { get; set; }

    public int CompletedSessionCount { get; set; }

    public decimal AverageRating { get; set; }

    public List<SelectListItem> LinhVucOptions { get; set; } = [];

    public List<string> PopularTags { get; set; } = [];

    public List<Hoc11MentorCardViewModel> MentorGoiY { get; set; } = [];
}

public class Hoc11MentorCardViewModel
{
    public int MaHuongDan { get; set; }

    public string HoTen { get; set; } = string.Empty;

    public string? AnhDaiDien { get; set; }

    public string LoaiNguoiHuongDan { get; set; } = "Mentor";

    public decimal DiemDanhGia { get; set; }

    public int SoLuotDanhGia { get; set; }

    public decimal DiemUyTin { get; set; }

    public int SoBuoiHoTro { get; set; }

    public int TyLePhuHop { get; set; }

    public string TrangThaiLichRanh { get; set; } = "Chưa cập nhật lịch rảnh";

    public string MoTaKinhNghiem { get; set; } = "Mentor đã được duyệt và sẵn sàng hỗ trợ học 1-1.";

    public List<string> ChuyenMon { get; set; } = [];
}

public class MentorDetailViewModel : Hoc11MentorCardViewModel
{
    public string Email { get; set; } = string.Empty;

    public string? SoDienThoai { get; set; }

    public string? ChuyenNganh { get; set; }

    public string? KyNang { get; set; }

    public string? MucTieuChiaSe { get; set; }

    public List<string> LichRanh { get; set; } = [];

    public List<string> CauLacBo { get; set; } = [];

    public List<string> LichSuHoTro { get; set; } = [];

    public List<MentorReviewViewModel> DanhGiaGanDay { get; set; } = [];
}

public class MentorReviewViewModel
{
    public string SinhVien { get; set; } = string.Empty;

    public int SoSao { get; set; }

    public string? NhanXet { get; set; }

    public DateTime? NgayDanhGia { get; set; }

    public string? LinhVuc { get; set; }
}

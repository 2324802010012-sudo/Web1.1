using System.ComponentModel.DataAnnotations;

namespace StudyConnect.ViewModels;

public class ScheduleSessionViewModel
{
    public int MaGhepNoi { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày học.")]
    [DataType(DataType.Date)]
    public DateTime NgayHoc { get; set; } = DateTime.Today.AddDays(1);

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu.")]
    [DataType(DataType.Time)]
    public TimeSpan GioBatDau { get; set; } = new(19, 0, 0);

    [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc.")]
    [DataType(DataType.Time)]
    public TimeSpan GioKetThuc { get; set; } = new(20, 30, 0);

    [StringLength(50)]
    public string HinhThuc { get; set; } = "Online";

    [StringLength(200)]
    public string? DiaDiem { get; set; }

    [StringLength(255)]
    public string? LinkOnline { get; set; }

    public List<string> SelectedSlots { get; set; } = [];
}

public class BaoCaoBuoiHocViewModel
{
    public int MaLichHoc { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nội dung đã học.")]
    public string NoiDungDaHoc { get; set; } = string.Empty;

    public string? BaiTap { get; set; }

    [StringLength(50)]
    public string MucDoTiepThu { get; set; } = "Khá";

    public string? NhanXet { get; set; }
}

public class DanhGiaMentorViewModel
{
    public int MaGhepNoi { get; set; }

    [Range(1, 5, ErrorMessage = "Số sao phải từ 1 đến 5.")]
    public int SoSao { get; set; } = 5;

    public string? NhanXet { get; set; }
}

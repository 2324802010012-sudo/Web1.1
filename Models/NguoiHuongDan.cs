using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("NguoiHuongDan")]
[Index("MaTaiKhoan", Name = "UQ__NguoiHuo__AD7C652812A12A54", IsUnique = true)]
public partial class NguoiHuongDan
{
    [Key]
    public int MaHuongDan { get; set; }

    public int MaTaiKhoan { get; set; }

    [StringLength(50)]
    public string LoaiNguoiHuongDan { get; set; } = null!;

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? DiemUyTin { get; set; }

    [Column(TypeName = "decimal(3, 2)")]
    public decimal? DiemDanhGia { get; set; }

    public int? SoLuotDanhGia { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    [InverseProperty("MaHuongDanNavigation")]
    public virtual ICollection<ChuyenMonNguoiHuongDan> ChuyenMonNguoiHuongDans { get; set; } = new List<ChuyenMonNguoiHuongDan>();

    [InverseProperty("MaHuongDanNavigation")]
    public virtual ICollection<DanhGiaHuongDan> DanhGiaHuongDans { get; set; } = new List<DanhGiaHuongDan>();

    [InverseProperty("MaHuongDanNavigation")]
    public virtual ICollection<GhepNoiHocTap> GhepNoiHocTaps { get; set; } = new List<GhepNoiHocTap>();

    [ForeignKey("MaTaiKhoan")]
    [InverseProperty("NguoiHuongDan")]
    public virtual TaiKhoan MaTaiKhoanNavigation { get; set; } = null!;

    [InverseProperty("MaHuongDanNavigation")]
    public virtual ICollection<XepHangMentor> XepHangMentors { get; set; } = new List<XepHangMentor>();
}

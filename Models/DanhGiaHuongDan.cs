using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("DanhGiaHuongDan")]
public partial class DanhGiaHuongDan
{
    [Key]
    public int MaDanhGia { get; set; }

    public int MaHuongDan { get; set; }

    public int MaSinhVien { get; set; }

    public int MaGhepNoi { get; set; }

    public int? MaLichHoc { get; set; }

    public int? SoSao { get; set; }

    public string? NhanXet { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayDanhGia { get; set; }

    [ForeignKey("MaGhepNoi")]
    [InverseProperty("DanhGiaHuongDans")]
    public virtual GhepNoiHocTap MaGhepNoiNavigation { get; set; } = null!;

    [ForeignKey("MaHuongDan")]
    [InverseProperty("DanhGiaHuongDans")]
    public virtual NguoiHuongDan MaHuongDanNavigation { get; set; } = null!;

    [ForeignKey("MaSinhVien")]
    [InverseProperty("DanhGiaHuongDans")]
    public virtual SinhVien MaSinhVienNavigation { get; set; } = null!;
}

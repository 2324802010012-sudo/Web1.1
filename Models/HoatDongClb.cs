using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("HoatDongCLB")]
public partial class HoatDongClb
{
    [Key]
    public int MaHoatDong { get; set; }

    [Column("MaCLB")]
    public int MaClb { get; set; }

    [StringLength(200)]
    public string TieuDe { get; set; } = null!;

    public string? NoiDung { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGian { get; set; }

    [StringLength(200)]
    public string? DiaDiem { get; set; }

    public int? NguoiDang { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayDang { get; set; }

    [ForeignKey("MaClb")]
    [InverseProperty("HoatDongClbs")]
    public virtual CauLacBo MaClbNavigation { get; set; } = null!;

    [ForeignKey("NguoiDang")]
    [InverseProperty("HoatDongClbs")]
    public virtual TaiKhoan? NguoiDangNavigation { get; set; }
}

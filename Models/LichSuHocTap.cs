using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("LichSuHocTap")]
public partial class LichSuHocTap
{
    [Key]
    public int MaLichSu { get; set; }

    public int MaSinhVien { get; set; }

    public int MaGhepNoi { get; set; }

    public int? SoBuoiDaHoc { get; set; }

    [StringLength(100)]
    public string? TienDo { get; set; }

    public string? KetQuaTongHop { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayCapNhat { get; set; }

    [ForeignKey("MaGhepNoi")]
    [InverseProperty("LichSuHocTaps")]
    public virtual GhepNoiHocTap MaGhepNoiNavigation { get; set; } = null!;

    [ForeignKey("MaSinhVien")]
    [InverseProperty("LichSuHocTaps")]
    public virtual SinhVien MaSinhVienNavigation { get; set; } = null!;
}

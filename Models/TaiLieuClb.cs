using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("TaiLieuCLB")]
public partial class TaiLieuClb
{
    [Key]
    public int MaTaiLieu { get; set; }

    [Column("MaCLB")]
    public int MaClb { get; set; }

    [StringLength(200)]
    public string TieuDe { get; set; } = null!;

    public string? MoTa { get; set; }

    [StringLength(255)]
    public string? TepDinhKem { get; set; }

    public int NguoiDang { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayDang { get; set; }

    [ForeignKey("MaClb")]
    [InverseProperty("TaiLieuClbs")]
    public virtual CauLacBo MaClbNavigation { get; set; } = null!;

    [ForeignKey("NguoiDang")]
    [InverseProperty("TaiLieuClbs")]
    public virtual TaiKhoan NguoiDangNavigation { get; set; } = null!;
}

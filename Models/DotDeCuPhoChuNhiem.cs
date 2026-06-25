using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("DotDeCuPhoChuNhiem")]
public partial class DotDeCuPhoChuNhiem
{
    [Key]
    public int MaDot { get; set; }

    [Column("MaCLB")]
    public int MaClb { get; set; }

    [StringLength(200)]
    public string TenDot { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime ThoiGianBatDau { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime ThoiGianKetThuc { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    [ForeignKey("MaClb")]
    [InverseProperty("DotDeCuPhoChuNhiems")]
    public virtual CauLacBo MaClbNavigation { get; set; } = null!;

    [InverseProperty("MaDotNavigation")]
    public virtual ICollection<PhieuBauPhoChuNhiem> PhieuBauPhoChuNhiems { get; set; } = new List<PhieuBauPhoChuNhiem>();

    [InverseProperty("MaDotNavigation")]
    public virtual ICollection<UngVienPhoChuNhiem> UngVienPhoChuNhiems { get; set; } = new List<UngVienPhoChuNhiem>();
}

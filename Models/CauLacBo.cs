using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("CauLacBo")]
public partial class CauLacBo
{
    [Key]
    [Column("MaCLB")]
    public int MaClb { get; set; }

    [Column("TenCLB")]
    [StringLength(150)]
    public string TenClb { get; set; } = null!;

    public string? MoTa { get; set; }

    public DateOnly? NgayThanhLap { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    [InverseProperty("MaClbNavigation")]
    public virtual ICollection<DotDeCuPhoChuNhiem> DotDeCuPhoChuNhiems { get; set; } = new List<DotDeCuPhoChuNhiem>();

    [InverseProperty("MaClbNavigation")]
    public virtual ICollection<HoatDongClb> HoatDongClbs { get; set; } = new List<HoatDongClb>();

    [InverseProperty("MaClbNavigation")]
    public virtual ICollection<TaiLieuClb> TaiLieuClbs { get; set; } = new List<TaiLieuClb>();

    [InverseProperty("MaClbNavigation")]
    public virtual ICollection<ThanhVienClb> ThanhVienClbs { get; set; } = new List<ThanhVienClb>();
}

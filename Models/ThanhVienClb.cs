using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("ThanhVienCLB")]
[Index("MaClb", "MaSinhVien", Name = "UQ__ThanhVie__74F7AD1F07844DB8", IsUnique = true)]
public partial class ThanhVienClb
{
    [Key]
    public int MaThanhVien { get; set; }

    [Column("MaCLB")]
    public int MaClb { get; set; }

    public int MaSinhVien { get; set; }

    [Column("VaiTroCLB")]
    [StringLength(50)]
    public string? VaiTroClb { get; set; }

    public DateOnly? NgayThamGia { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    [ForeignKey("MaClb")]
    [InverseProperty("ThanhVienClbs")]
    public virtual CauLacBo MaClbNavigation { get; set; } = null!;

    [ForeignKey("MaSinhVien")]
    [InverseProperty("ThanhVienClbs")]
    public virtual SinhVien MaSinhVienNavigation { get; set; } = null!;
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("GiangVien")]
[Index("MaGv", Name = "UQ__GiangVie__2725AEF238F59E51", IsUnique = true)]
[Index("MaTaiKhoan", Name = "UQ__GiangVie__AD7C6528F51E21CF", IsUnique = true)]
public partial class GiangVien
{
    [Key]
    public int MaGiangVien { get; set; }

    public int MaTaiKhoan { get; set; }

    [Column("MaGV")]
    [StringLength(50)]
    public string MaGv { get; set; } = null!;

    [StringLength(100)]
    public string? HocVi { get; set; }

    [StringLength(100)]
    public string? BoMon { get; set; }

    [ForeignKey("MaTaiKhoan")]
    [InverseProperty("GiangVien")]
    public virtual TaiKhoan MaTaiKhoanNavigation { get; set; } = null!;
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("PhieuBauPhoChuNhiem")]
[Index("MaDot", "MaSinhVienBau", Name = "UQ__PhieuBau__A67571EACFD64498", IsUnique = true)]
public partial class PhieuBauPhoChuNhiem
{
    [Key]
    public int MaPhieu { get; set; }

    public int MaDot { get; set; }

    public int MaUngVien { get; set; }

    public int MaSinhVienBau { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ThoiGianBau { get; set; }

    [ForeignKey("MaDot")]
    [InverseProperty("PhieuBauPhoChuNhiems")]
    public virtual DotDeCuPhoChuNhiem MaDotNavigation { get; set; } = null!;

    [ForeignKey("MaSinhVienBau")]
    [InverseProperty("PhieuBauPhoChuNhiems")]
    public virtual SinhVien MaSinhVienBauNavigation { get; set; } = null!;

    [ForeignKey("MaUngVien")]
    [InverseProperty("PhieuBauPhoChuNhiems")]
    public virtual UngVienPhoChuNhiem MaUngVienNavigation { get; set; } = null!;
}

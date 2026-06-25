using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("UngVienPhoChuNhiem")]
public partial class UngVienPhoChuNhiem
{
    [Key]
    public int MaUngVien { get; set; }

    public int MaDot { get; set; }

    public int MaSinhVien { get; set; }

    public int? NguoiDeCu { get; set; }

    public string? LyDoDeCu { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    [ForeignKey("MaDot")]
    [InverseProperty("UngVienPhoChuNhiems")]
    public virtual DotDeCuPhoChuNhiem MaDotNavigation { get; set; } = null!;

    [ForeignKey("MaSinhVien")]
    [InverseProperty("UngVienPhoChuNhiemMaSinhVienNavigations")]
    public virtual SinhVien MaSinhVienNavigation { get; set; } = null!;

    [ForeignKey("NguoiDeCu")]
    [InverseProperty("UngVienPhoChuNhiemNguoiDeCuNavigations")]
    public virtual SinhVien? NguoiDeCuNavigation { get; set; }

    [InverseProperty("MaUngVienNavigation")]
    public virtual ICollection<PhieuBauPhoChuNhiem> PhieuBauPhoChuNhiems { get; set; } = new List<PhieuBauPhoChuNhiem>();
}

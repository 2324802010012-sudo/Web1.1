using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("DangKyHuongDan")]
public partial class DangKyHuongDan
{
    [Key]
    public int MaDangKy { get; set; }

    public int MaSinhVien { get; set; }

    public int MaLinhVuc { get; set; }

    [Column(TypeName = "decimal(4, 2)")]
    public decimal? DiemMon { get; set; }

    [StringLength(255)]
    public string? MinhChung { get; set; }

    public string? LyDo { get; set; }

    [Column("TrangThaiCLB")]
    [StringLength(50)]
    public string? TrangThaiClb { get; set; }

    [StringLength(50)]
    public string? TrangThaiCoVan { get; set; }

    [StringLength(50)]
    public string? TrangThaiDuyet { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayDangKy { get; set; }

    [ForeignKey("MaLinhVuc")]
    [InverseProperty("DangKyHuongDans")]
    public virtual LinhVucHocTap MaLinhVucNavigation { get; set; } = null!;

    [ForeignKey("MaSinhVien")]
    [InverseProperty("DangKyHuongDans")]
    public virtual SinhVien MaSinhVienNavigation { get; set; } = null!;
}

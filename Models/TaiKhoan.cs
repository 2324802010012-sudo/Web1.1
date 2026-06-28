using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("TaiKhoan")]
[Index("Email", Name = "UQ__TaiKhoan__A9D10534431BDD8E", IsUnique = true)]
public partial class TaiKhoan
{
    [Key]
    public int MaTaiKhoan { get; set; }

    [StringLength(100)]
    public string HoTen { get; set; } = null!;

    [StringLength(150)]
    public string Email { get; set; } = null!;

    [Column("MatKhauHash")]
    [StringLength(255)]
    public string MatKhau { get; set; } = null!;

    [StringLength(50)]
    public string VaiTro { get; set; } = null!;

    [StringLength(20)]
    public string? SoDienThoai { get; set; }

    [StringLength(255)]
    public string? AnhDaiDien { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [InverseProperty("MaTaiKhoanNavigation")]
    public virtual GiangVien? GiangVien { get; set; }

    [InverseProperty("NguoiDangNavigation")]
    public virtual ICollection<HoatDongClb> HoatDongClbs { get; set; } = new List<HoatDongClb>();

    [InverseProperty("MaTaiKhoanNavigation")]
    public virtual ICollection<LichRanh> LichRanhs { get; set; } = new List<LichRanh>();

    [InverseProperty("MaTaiKhoanNavigation")]
    public virtual NguoiHuongDan? NguoiHuongDan { get; set; }

    [InverseProperty("MaTaiKhoanNavigation")]
    public virtual SinhVien? SinhVien { get; set; }

    [InverseProperty("NguoiDangNavigation")]
    public virtual ICollection<TaiLieuClb> TaiLieuClbs { get; set; } = new List<TaiLieuClb>();

    [InverseProperty("MaTaiKhoanNavigation")]
    public virtual ICollection<ThongBao> ThongBaos { get; set; } = new List<ThongBao>();
}

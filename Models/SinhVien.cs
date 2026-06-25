using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("SinhVien")]
[Index("Mssv", Name = "UQ__SinhVien__6CB3B7F8EF9F0949", IsUnique = true)]
[Index("MaTaiKhoan", Name = "UQ__SinhVien__AD7C6528DE891D6D", IsUnique = true)]
public partial class SinhVien
{
    [Key]
    public int MaSinhVien { get; set; }

    public int MaTaiKhoan { get; set; }

    [Column("MSSV")]
    [StringLength(50)]
    public string Mssv { get; set; } = null!;

    [StringLength(100)]
    public string? ChuyenNganh { get; set; }

    [StringLength(50)]
    public string? Lop { get; set; }

    [Column("GPA", TypeName = "decimal(3, 2)")]
    public decimal? Gpa { get; set; }

    public string? KyNang { get; set; }

    public string? GioiThieu { get; set; }

    [InverseProperty("MaSinhVienNavigation")]
    public virtual ICollection<DangKyHuongDan> DangKyHuongDans { get; set; } = new List<DangKyHuongDan>();

    [InverseProperty("MaSinhVienNavigation")]
    public virtual ICollection<DanhGiaHuongDan> DanhGiaHuongDans { get; set; } = new List<DanhGiaHuongDan>();

    [InverseProperty("MaSinhVienNavigation")]
    public virtual ICollection<LichSuHocTap> LichSuHocTaps { get; set; } = new List<LichSuHocTap>();

    [ForeignKey("MaTaiKhoan")]
    [InverseProperty("SinhVien")]
    public virtual TaiKhoan MaTaiKhoanNavigation { get; set; } = null!;

    [InverseProperty("MaSinhVienBauNavigation")]
    public virtual ICollection<PhieuBauPhoChuNhiem> PhieuBauPhoChuNhiems { get; set; } = new List<PhieuBauPhoChuNhiem>();

    [InverseProperty("MaSinhVienNavigation")]
    public virtual ICollection<ThanhVienClb> ThanhVienClbs { get; set; } = new List<ThanhVienClb>();

    [InverseProperty("MaSinhVienNavigation")]
    public virtual ICollection<UngVienPhoChuNhiem> UngVienPhoChuNhiemMaSinhVienNavigations { get; set; } = new List<UngVienPhoChuNhiem>();

    [InverseProperty("NguoiDeCuNavigation")]
    public virtual ICollection<UngVienPhoChuNhiem> UngVienPhoChuNhiemNguoiDeCuNavigations { get; set; } = new List<UngVienPhoChuNhiem>();

    [InverseProperty("MaSinhVienNavigation")]
    public virtual ICollection<YeuCauHoTroHocTap> YeuCauHoTroHocTaps { get; set; } = new List<YeuCauHoTroHocTap>();
}

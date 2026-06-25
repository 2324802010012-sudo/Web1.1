using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("LinhVucHocTap")]
public partial class LinhVucHocTap
{
    [Key]
    public int MaLinhVuc { get; set; }

    [StringLength(100)]
    public string TenLinhVuc { get; set; } = null!;

    public string? MoTa { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    [InverseProperty("MaLinhVucNavigation")]
    public virtual ICollection<ChuyenMonNguoiHuongDan> ChuyenMonNguoiHuongDans { get; set; } = new List<ChuyenMonNguoiHuongDan>();

    [InverseProperty("MaLinhVucNavigation")]
    public virtual ICollection<DangKyHuongDan> DangKyHuongDans { get; set; } = new List<DangKyHuongDan>();

    [InverseProperty("MaLinhVucNavigation")]
    public virtual ICollection<YeuCauHoTroHocTap> YeuCauHoTroHocTaps { get; set; } = new List<YeuCauHoTroHocTap>();
}

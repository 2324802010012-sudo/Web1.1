using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("ChuyenMonNguoiHuongDan")]
[Index("MaHuongDan", "MaLinhVuc", Name = "UQ__ChuyenMo__2E5ED508C33C9C59", IsUnique = true)]
public partial class ChuyenMonNguoiHuongDan
{
    [Key]
    public int MaChuyenMon { get; set; }

    public int MaHuongDan { get; set; }

    public int MaLinhVuc { get; set; }

    public int? MucDoThanhThao { get; set; }

    public string? MoTaKinhNghiem { get; set; }

    [ForeignKey("MaHuongDan")]
    [InverseProperty("ChuyenMonNguoiHuongDans")]
    public virtual NguoiHuongDan MaHuongDanNavigation { get; set; } = null!;

    [ForeignKey("MaLinhVuc")]
    [InverseProperty("ChuyenMonNguoiHuongDans")]
    public virtual LinhVucHocTap MaLinhVucNavigation { get; set; } = null!;
}

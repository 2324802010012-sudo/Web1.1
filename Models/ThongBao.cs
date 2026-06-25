using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("ThongBao")]
public partial class ThongBao
{
    [Key]
    public int MaThongBao { get; set; }

    public int MaTaiKhoan { get; set; }

    [StringLength(200)]
    public string TieuDe { get; set; } = null!;

    public string? NoiDung { get; set; }

    [StringLength(50)]
    public string? LoaiThongBao { get; set; }

    public bool? DaDoc { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [ForeignKey("MaTaiKhoan")]
    [InverseProperty("ThongBaos")]
    public virtual TaiKhoan MaTaiKhoanNavigation { get; set; } = null!;
}

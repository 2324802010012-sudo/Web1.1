using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("BaoCaoBuoiHoc")]
[Index("MaLichHoc", Name = "UQ__BaoCaoBu__150EBC20715D0659", IsUnique = true)]
public partial class BaoCaoBuoiHoc
{
    [Key]
    public int MaBaoCao { get; set; }

    public int MaLichHoc { get; set; }

    public string? NoiDungDaHoc { get; set; }

    public string? BaiTap { get; set; }

    [StringLength(50)]
    public string? MucDoTiepThu { get; set; }

    public string? NhanXet { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayBaoCao { get; set; }

    [ForeignKey("MaLichHoc")]
    [InverseProperty("BaoCaoBuoiHoc")]
    public virtual LichHoc MaLichHocNavigation { get; set; } = null!;
}

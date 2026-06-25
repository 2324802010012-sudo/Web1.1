using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("YeuCauHoTroHocTap")]
public partial class YeuCauHoTroHocTap
{
    [Key]
    public int MaYeuCau { get; set; }

    public int MaSinhVien { get; set; }

    public int MaLinhVuc { get; set; }

    public string MoTaVanDe { get; set; } = null!;

    public string? MucTieu { get; set; }

    [StringLength(50)]
    public string? MucDoCanHoTro { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [InverseProperty("MaYeuCauNavigation")]
    public virtual ICollection<GhepNoiHocTap> GhepNoiHocTaps { get; set; } = new List<GhepNoiHocTap>();

    [ForeignKey("MaLinhVuc")]
    [InverseProperty("YeuCauHoTroHocTaps")]
    public virtual LinhVucHocTap MaLinhVucNavigation { get; set; } = null!;

    [ForeignKey("MaSinhVien")]
    [InverseProperty("YeuCauHoTroHocTaps")]
    public virtual SinhVien MaSinhVienNavigation { get; set; } = null!;
}

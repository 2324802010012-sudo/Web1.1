using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("GhepNoiHocTap")]
public partial class GhepNoiHocTap
{
    [Key]
    public int MaGhepNoi { get; set; }

    public int MaYeuCau { get; set; }

    public int MaHuongDan { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal ChuyenMonScore { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal LichRanhScore { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal DiemUyTinScore { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal DanhGiaScore { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal TuongDongScore { get; set; }

    public bool LaMentorChinh { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    [Column(TypeName = "decimal(5, 2)")]
    public decimal? DiemPhuHop { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayGhep { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? NgayMentorPhanHoi { get; set; }

    public string? GhiChuPhanHoi { get; set; }

    [InverseProperty("MaGhepNoiNavigation")]
    public virtual ICollection<DanhGiaHuongDan> DanhGiaHuongDans { get; set; } = new List<DanhGiaHuongDan>();

    [InverseProperty("MaGhepNoiNavigation")]
    public virtual ICollection<LichHoc> LichHocs { get; set; } = new List<LichHoc>();

    [InverseProperty("MaGhepNoiNavigation")]
    public virtual ICollection<LichSuHocTap> LichSuHocTaps { get; set; } = new List<LichSuHocTap>();

    [ForeignKey("MaHuongDan")]
    [InverseProperty("GhepNoiHocTaps")]
    public virtual NguoiHuongDan MaHuongDanNavigation { get; set; } = null!;

    [ForeignKey("MaYeuCau")]
    [InverseProperty("GhepNoiHocTaps")]
    public virtual YeuCauHoTroHocTap MaYeuCauNavigation { get; set; } = null!;
}

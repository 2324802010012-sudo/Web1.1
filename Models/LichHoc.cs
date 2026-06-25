using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("LichHoc")]
public partial class LichHoc
{
    [Key]
    public int MaLichHoc { get; set; }

    public int MaGhepNoi { get; set; }

    public DateOnly NgayHoc { get; set; }

    public TimeOnly GioBatDau { get; set; }

    public TimeOnly GioKetThuc { get; set; }

    [StringLength(50)]
    public string? HinhThuc { get; set; }

    [StringLength(200)]
    public string? DiaDiem { get; set; }

    [StringLength(255)]
    public string? LinkOnline { get; set; }

    [StringLength(50)]
    public string? TrangThai { get; set; }

    [InverseProperty("MaLichHocNavigation")]
    public virtual BaoCaoBuoiHoc? BaoCaoBuoiHoc { get; set; }

    [ForeignKey("MaGhepNoi")]
    [InverseProperty("LichHocs")]
    public virtual GhepNoiHocTap MaGhepNoiNavigation { get; set; } = null!;
}

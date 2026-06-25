using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("XepHangMentor")]
public partial class XepHangMentor
{
    [Key]
    public int MaXepHang { get; set; }

    public int MaHuongDan { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? DiemUyTin { get; set; }

    public int? HangTong { get; set; }

    public int? HangTheoLinhVuc { get; set; }

    [StringLength(20)]
    public string? ThangNam { get; set; }

    [ForeignKey("MaHuongDan")]
    [InverseProperty("XepHangMentors")]
    public virtual NguoiHuongDan MaHuongDanNavigation { get; set; } = null!;
}

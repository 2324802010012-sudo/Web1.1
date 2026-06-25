using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Models;

[Table("LichRanh")]
public partial class LichRanh
{
    [Key]
    public int MaLichRanh { get; set; }

    public int MaTaiKhoan { get; set; }

    public int Thu { get; set; }

    public TimeOnly GioBatDau { get; set; }

    public TimeOnly GioKetThuc { get; set; }

    [ForeignKey("MaTaiKhoan")]
    [InverseProperty("LichRanhs")]
    public virtual TaiKhoan MaTaiKhoanNavigation { get; set; } = null!;
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class SinhVienController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public SinhVienController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var sinhVien = await _context.SinhViens
            .FirstOrDefaultAsync(s => s.MaTaiKhoan == CurrentUserId);
        var sinhVienId = sinhVien?.MaSinhVien ?? 0;

        ViewBag.UserName = CurrentUserName;
        ViewBag.YeuCau = sinhVienId == 0 ? 0 : await _context.YeuCauHoTroHocTaps.CountAsync(y => y.MaSinhVien == sinhVienId);
        ViewBag.GhepNoi = sinhVienId == 0 ? 0 : await _context.GhepNoiHocTaps.CountAsync(g => g.MaYeuCauNavigation.MaSinhVien == sinhVienId);
        ViewBag.LichHoc = sinhVienId == 0 ? 0 : await _context.LichHocs.CountAsync(l => l.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVien == sinhVienId);
        ViewBag.DanhGia = sinhVienId == 0 ? 0 : await _context.DanhGiaHuongDans.CountAsync(d => d.MaSinhVien == sinhVienId);
        ViewBag.CauLacBo = sinhVienId == 0 ? 0 : await _context.ThanhVienClbs.CountAsync(t => t.MaSinhVien == sinhVienId);

        return View();
    }
}

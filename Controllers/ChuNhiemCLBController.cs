using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class ChuNhiemCLBController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public ChuNhiemCLBController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var sinhVienId = await _context.SinhViens
            .Where(s => s.MaTaiKhoan == CurrentUserId)
            .Select(s => s.MaSinhVien)
            .FirstOrDefaultAsync();
        var clbIds = sinhVienId == 0
            ? new List<int>()
            : await _context.ThanhVienClbs
                .Where(t => t.MaSinhVien == sinhVienId)
                .Select(t => t.MaClb)
                .Distinct()
                .ToListAsync();

        ViewBag.UserName = CurrentUserName;
        ViewBag.CauLacBo = clbIds.Count;
        ViewBag.ThanhVien = clbIds.Count == 0 ? 0 : await _context.ThanhVienClbs.CountAsync(t => clbIds.Contains(t.MaClb));
        ViewBag.HoatDong = clbIds.Count == 0 ? 0 : await _context.HoatDongClbs.CountAsync(h => clbIds.Contains(h.MaClb));
        ViewBag.TaiLieu = clbIds.Count == 0 ? 0 : await _context.TaiLieuClbs.CountAsync(t => clbIds.Contains(t.MaClb));
        ViewBag.DotBauCu = clbIds.Count == 0 ? 0 : await _context.DotDeCuPhoChuNhiems.CountAsync(d => clbIds.Contains(d.MaClb));
        ViewBag.CanXacNhanMentor = await _context.DangKyHuongDans.CountAsync(d => d.TrangThaiClb == "Chờ xác nhận");

        return View();
    }
}

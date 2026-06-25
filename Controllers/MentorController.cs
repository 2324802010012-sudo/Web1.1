using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class MentorController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public MentorController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        var mentor = await _context.NguoiHuongDans
            .FirstOrDefaultAsync(m => m.MaTaiKhoan == CurrentUserId);
        var mentorId = mentor?.MaHuongDan ?? 0;

        ViewBag.UserName = CurrentUserName;
        ViewBag.GhepNoi = mentorId == 0 ? 0 : await _context.GhepNoiHocTaps.CountAsync(g => g.MaHuongDan == mentorId);
        ViewBag.LichHoc = mentorId == 0 ? 0 : await _context.LichHocs.CountAsync(l => l.MaGhepNoiNavigation.MaHuongDan == mentorId);
        ViewBag.BaoCao = mentorId == 0 ? 0 : await _context.BaoCaoBuoiHocs.CountAsync(b => b.MaLichHocNavigation.MaGhepNoiNavigation.MaHuongDan == mentorId);
        ViewBag.DanhGia = mentor?.DiemDanhGia ?? 0;
        ViewBag.UyTin = mentor?.DiemUyTin ?? 0;

        return View();
    }
}

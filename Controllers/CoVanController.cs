using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class CoVanController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public CoVanController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var guard = RequireRoles(AccountRoles.CoVan);
        if (guard != null) return guard;

        ViewBag.UserName = CurrentUserName;
        ViewBag.HoSoChoDuyet = await _context.DangKyHuongDans.CountAsync(d => d.TrangThaiCoVan == "Chờ duyệt" || d.TrangThaiDuyet == "Chờ duyệt");
        ViewBag.Mentor = await _context.NguoiHuongDans.CountAsync();
        ViewBag.YeuCau = await _context.YeuCauHoTroHocTaps.CountAsync();
        ViewBag.BaoCao = await _context.BaoCaoBuoiHocs.CountAsync();
        ViewBag.DanhGia = await _context.DanhGiaHuongDans.CountAsync();

        return View();
    }
}

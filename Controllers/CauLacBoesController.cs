using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class CauLacBoController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public CauLacBoController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;

        var clubs = await _context.CauLacBos
            .Include(c => c.ThanhVienClbs)
            .Include(c => c.HoatDongClbs)
            .Include(c => c.TaiLieuClbs)
            .Include(c => c.DotDeCuPhoChuNhiems)
            .OrderBy(c => c.TenClb)
            .ToListAsync();

        ViewBag.TotalMembers = clubs.Sum(c => c.ThanhVienClbs.Count(t => t.TrangThai == null || t.TrangThai == "Hoạt động"));
        ViewBag.TotalActivities = clubs.Sum(c => c.HoatDongClbs.Count);
        ViewBag.TotalDocuments = clubs.Sum(c => c.TaiLieuClbs.Count);
        ViewBag.ActiveClubs = clubs.Count(c => c.TrangThai == null || c.TrangThai == "Hoạt động");

        return View(clubs);
    }

    public async Task<IActionResult> Details(int? id)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;
        if (id == null) return NotFound();

        var club = await _context.CauLacBos
            .Include(c => c.ThanhVienClbs)
                .ThenInclude(t => t.MaSinhVienNavigation)
                    .ThenInclude(s => s.MaTaiKhoanNavigation)
            .Include(c => c.HoatDongClbs)
                .ThenInclude(h => h.NguoiDangNavigation)
            .Include(c => c.TaiLieuClbs)
                .ThenInclude(t => t.NguoiDangNavigation)
            .Include(c => c.DotDeCuPhoChuNhiems)
            .FirstOrDefaultAsync(m => m.MaClb == id);

        return club == null ? NotFound() : View(club);
    }

    public IActionResult Create()
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;

        PopulateStatusOptions();
        return View(new CauLacBo
        {
            NgayThanhLap = DateOnly.FromDateTime(DateTime.Today),
            TrangThai = "Hoạt động"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("TenClb,MoTa,NgayThanhLap,TrangThai")] CauLacBo club)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;

        NormalizeClub(club);
        await ValidateClubAsync(club);

        if (!ModelState.IsValid)
        {
            PopulateStatusOptions(club.TrangThai);
            return View(club);
        }

        _context.CauLacBos.Add(club);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã tạo CLB {club.TenClb}.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;
        if (id == null) return NotFound();

        var club = await _context.CauLacBos.FindAsync(id);
        if (club == null) return NotFound();

        PopulateStatusOptions(club.TrangThai);
        return View(club);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("MaClb,TenClb,MoTa,NgayThanhLap,TrangThai")] CauLacBo club)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;
        if (id != club.MaClb) return NotFound();

        NormalizeClub(club);
        await ValidateClubAsync(club);

        if (!ModelState.IsValid)
        {
            PopulateStatusOptions(club.TrangThai);
            return View(club);
        }

        var existing = await _context.CauLacBos.FindAsync(id);
        if (existing == null) return NotFound();

        existing.TenClb = club.TenClb;
        existing.MoTa = club.MoTa;
        existing.NgayThanhLap = club.NgayThanhLap;
        existing.TrangThai = club.TrangThai;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã cập nhật CLB {existing.TenClb}.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;
        if (id == null) return NotFound();

        var club = await _context.CauLacBos
            .Include(c => c.ThanhVienClbs)
            .Include(c => c.HoatDongClbs)
            .Include(c => c.TaiLieuClbs)
            .Include(c => c.DotDeCuPhoChuNhiems)
            .FirstOrDefaultAsync(m => m.MaClb == id);

        return club == null ? NotFound() : View(club);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;

        var club = await _context.CauLacBos
            .Include(c => c.ThanhVienClbs)
            .Include(c => c.HoatDongClbs)
            .Include(c => c.TaiLieuClbs)
            .Include(c => c.DotDeCuPhoChuNhiems)
            .FirstOrDefaultAsync(c => c.MaClb == id);

        if (club == null) return NotFound();

        var hasRelatedData = club.ThanhVienClbs.Any()
            || club.HoatDongClbs.Any()
            || club.TaiLieuClbs.Any()
            || club.DotDeCuPhoChuNhiems.Any();

        if (hasRelatedData)
        {
            club.TrangThai = "Tạm dừng";
            TempData["SuccessMessage"] = $"CLB {club.TenClb} đã có dữ liệu liên quan nên hệ thống chuyển sang trạng thái Tạm dừng.";
        }
        else
        {
            _context.CauLacBos.Remove(club);
            TempData["SuccessMessage"] = $"Đã xóa CLB {club.TenClb}.";
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateClubAsync(CauLacBo club)
    {
        if (string.IsNullOrWhiteSpace(club.TenClb))
        {
            ModelState.AddModelError(nameof(club.TenClb), "Vui lòng nhập tên CLB.");
        }
        else if (await _context.CauLacBos.AnyAsync(c => c.MaClb != club.MaClb && c.TenClb == club.TenClb))
        {
            ModelState.AddModelError(nameof(club.TenClb), "Tên CLB này đã tồn tại.");
        }

        if (club.TrangThai is not ("Hoạt động" or "Tạm dừng"))
        {
            ModelState.AddModelError(nameof(club.TrangThai), "Trạng thái CLB không hợp lệ.");
        }
    }

    private static void NormalizeClub(CauLacBo club)
    {
        club.TenClb = club.TenClb?.Trim() ?? string.Empty;
        club.MoTa = string.IsNullOrWhiteSpace(club.MoTa) ? null : club.MoTa.Trim();
        club.TrangThai = string.IsNullOrWhiteSpace(club.TrangThai) ? "Hoạt động" : club.TrangThai.Trim();
    }

    private void PopulateStatusOptions(string? selected = null)
    {
        ViewBag.StatusOptions = new[]
        {
            new SelectListItem("Hoạt động", "Hoạt động", selected == "Hoạt động"),
            new SelectListItem("Tạm dừng", "Tạm dừng", selected == "Tạm dừng")
        };
    }
}

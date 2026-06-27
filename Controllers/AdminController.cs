using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class AdminController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;

        var roleStats = await _context.TaiKhoans
            .GroupBy(t => t.VaiTro)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .OrderBy(g => g.Role)
            .ToListAsync();

        ViewBag.UserName = CurrentUserName;
        ViewBag.TongTaiKhoan = await _context.TaiKhoans.CountAsync();
        ViewBag.TaiKhoanHoatDong = await _context.TaiKhoans.CountAsync(t => t.TrangThai == null || t.TrangThai == "Hoạt động");
        ViewBag.TaiKhoanBiKhoa = await _context.TaiKhoans.CountAsync(t => t.TrangThai == "Đã khóa" || t.TrangThai == "Khóa");
        ViewBag.TongVaiTro = roleStats.Count;
        ViewBag.LinhVuc = await _context.LinhVucHocTaps.CountAsync();
        ViewBag.ThongBao = await _context.ThongBaos.CountAsync();
        ViewBag.RoleStats = roleStats.ToDictionary(x => x.Role, x => x.Count);
        ViewBag.RecentAccounts = await _context.TaiKhoans
            .OrderByDescending(t => t.NgayTao)
            .Take(8)
            .ToListAsync();
        ViewBag.ChuNhiemAccounts = await _context.TaiKhoans
            .Where(t => t.VaiTro == AccountRoles.ChuNhiemClb || t.VaiTro == AccountRoles.SinhVien)
            .OrderBy(t => t.HoTen)
            .ToListAsync();
        ViewBag.Clubs = await _context.CauLacBos
            .OrderBy(c => c.TenClb)
            .ToListAsync();
        ViewBag.ClubManagers = await _context.ThanhVienClbs
            .Include(t => t.MaClbNavigation)
            .Include(t => t.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
            .Where(t => t.VaiTroClb == "Chủ nhiệm" && (t.TrangThai == null || (t.TrangThai != "Đã rời" && t.TrangThai != "Đã khóa")))
            .OrderBy(t => t.MaClbNavigation.TenClb)
            .ToListAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignClubManager(int taiKhoanId, int clbId)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;

        var account = await _context.TaiKhoans.FindAsync(taiKhoanId);
        var club = await _context.CauLacBos.FindAsync(clbId);
        if (account == null || club == null) return NotFound();

        var student = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaTaiKhoan == taiKhoanId);
        if (student == null)
        {
            student = new SinhVien
            {
                MaTaiKhoan = taiKhoanId,
                Mssv = $"CNCLB-{taiKhoanId:0000}",
                ChuyenNganh = "Quản lý CLB",
                Lop = "CLB",
                GioiThieu = "Tài khoản được Admin cấp quyền Chủ nhiệm CLB."
            };
            _context.SinhViens.Add(student);
            await _context.SaveChangesAsync();
        }

        var membership = await _context.ThanhVienClbs
            .FirstOrDefaultAsync(t => t.MaClb == clbId && t.MaSinhVien == student.MaSinhVien);

        if (membership == null)
        {
            _context.ThanhVienClbs.Add(new ThanhVienClb
            {
                MaClb = clbId,
                MaSinhVien = student.MaSinhVien,
                VaiTroClb = "Chủ nhiệm",
                NgayThamGia = DateOnly.FromDateTime(DateTime.Today),
                TrangThai = "Hoạt động"
            });
        }
        else
        {
            membership.VaiTroClb = "Chủ nhiệm";
            membership.TrangThai = "Hoạt động";
            membership.NgayThamGia ??= DateOnly.FromDateTime(DateTime.Today);
        }

        account.VaiTro = AccountRoles.ChuNhiemClb;
        account.TrangThai = "Hoạt động";

        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = account.MaTaiKhoan,
            TieuDe = "Bạn được cấp quyền Chủ nhiệm CLB",
            NoiDung = $"Admin đã cấp quyền quản lý nội bộ cho CLB {club.TenClb}.",
            LoaiThongBao = "PhanQuyenCLB",
            DaDoc = false,
            NgayTao = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã cấp quyền Chủ nhiệm {club.TenClb} cho {account.HoTen}.";
        return RedirectToAction(nameof(Index), null, null, "cap-quyen-clb");
    }
}

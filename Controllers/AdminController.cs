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
        var allAccounts = await _context.TaiKhoans.ToListAsync();

        ViewBag.UserName = CurrentUserName;
        ViewBag.TongTaiKhoan = allAccounts.Count;
        ViewBag.TaiKhoanHoatDong = await _context.TaiKhoans.CountAsync(t => t.TrangThai == null || t.TrangThai == "Hoạt động");
        ViewBag.TaiKhoanBiKhoa = await _context.TaiKhoans.CountAsync(t => t.TrangThai == "Bị khóa" || t.TrangThai == "Đã khóa" || t.TrangThai == "Khóa");
        ViewBag.TongVaiTro = roleStats.Count;
        ViewBag.TaiKhoanHoatDong = allAccounts.Count(t => IsActiveStatus(t.TrangThai));
        ViewBag.TaiKhoanBiKhoa = allAccounts.Count(t => IsLockedStatus(t.TrangThai));
        ViewBag.LinhVuc = await _context.LinhVucHocTaps.CountAsync();
        ViewBag.ThongBao = await _context.ThongBaos.CountAsync();
        ViewBag.HoatDongGanDay = await BuildRecentActivitiesAsync();
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

    private static bool IsActiveStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            || status.Equals("Hoạt động", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Ho", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLockedStatus(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && (status.Contains("khóa", StringComparison.OrdinalIgnoreCase)
                || status.Contains("khoa", StringComparison.OrdinalIgnoreCase)
                || status.Contains("khÃ³a", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<AdminActivityViewModel>> BuildRecentActivitiesAsync()
    {
        var activities = new List<AdminActivityViewModel>();

        var accounts = await _context.TaiKhoans
            .OrderByDescending(t => t.NgayTao)
            .Take(12)
            .ToListAsync();

        activities.AddRange(accounts
            .Where(t => t.NgayTao.HasValue)
            .Select(t => new AdminActivityViewModel
            {
                Time = t.NgayTao!.Value,
                ActorName = t.HoTen,
                ActorRole = t.VaiTro,
                Action = "Tạo tài khoản",
                Target = t.Email,
                Description = $"Tài khoản vai trò {AccountRoles.DisplayName(t.VaiTro)} được tạo trên hệ thống.",
                Tone = "blue",
                Url = Url.Action("Details", "TaiKhoans", new { id = t.MaTaiKhoan })
            }));

        var requests = await _context.YeuCauHoTroHocTaps
            .Include(y => y.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
            .Include(y => y.MaLinhVucNavigation)
            .OrderByDescending(y => y.NgayTao)
            .Take(12)
            .ToListAsync();

        activities.AddRange(requests
            .Where(y => y.NgayTao.HasValue)
            .Select(y => new AdminActivityViewModel
            {
                Time = y.NgayTao!.Value,
                ActorName = y.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen,
                ActorRole = AccountRoles.SinhVien,
                Action = "Tạo yêu cầu học 1-1",
                Target = y.MaLinhVucNavigation.TenLinhVuc,
                Description = y.MucTieu ?? y.MoTaVanDe,
                Tone = "green",
                Url = Url.Action("Details", "TaiKhoans", new { id = y.MaSinhVienNavigation.MaTaiKhoan })
            }));

        var matches = await _context.GhepNoiHocTaps
            .Include(g => g.MaYeuCauNavigation)
                .ThenInclude(y => y.MaSinhVienNavigation)
                    .ThenInclude(s => s.MaTaiKhoanNavigation)
            .Include(g => g.MaHuongDanNavigation)
                .ThenInclude(m => m.MaTaiKhoanNavigation)
            .OrderByDescending(g => g.NgayGhep)
            .Take(12)
            .ToListAsync();

        activities.AddRange(matches
            .Where(g => g.NgayGhep.HasValue)
            .Select(g => new AdminActivityViewModel
            {
                Time = g.NgayGhep!.Value,
                ActorName = g.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen,
                ActorRole = AccountRoles.SinhVien,
                Action = "Gửi ghép nối mentor",
                Target = g.MaHuongDanNavigation.MaTaiKhoanNavigation.HoTen,
                Description = $"Trạng thái: {g.TrangThai ?? "Đang xử lý"} - Phù hợp {g.DiemPhuHop?.ToString("0") ?? "--"}%.",
                Tone = "purple",
                Url = Url.Action("Details", "TaiKhoans", new { id = g.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoan })
            }));

        var mentorApplications = await _context.DangKyHuongDans
            .Include(d => d.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
            .Include(d => d.MaLinhVucNavigation)
            .OrderByDescending(d => d.NgayDangKy)
            .Take(12)
            .ToListAsync();

        activities.AddRange(mentorApplications
            .Where(d => d.NgayDangKy.HasValue)
            .Select(d => new AdminActivityViewModel
            {
                Time = d.NgayDangKy!.Value,
                ActorName = d.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen,
                ActorRole = AccountRoles.SinhVien,
                Action = "Đăng ký làm mentor",
                Target = d.MaLinhVucNavigation.TenLinhVuc,
                Description = $"Trạng thái duyệt: {d.TrangThaiDuyet ?? d.TrangThaiCoVan ?? "Chờ duyệt"}.",
                Tone = "orange",
                Url = Url.Action("Details", "TaiKhoans", new { id = d.MaSinhVienNavigation.MaTaiKhoan })
            }));

        var clubDocuments = await _context.TaiLieuClbs
            .Include(t => t.NguoiDangNavigation)
            .Include(t => t.MaClbNavigation)
            .OrderByDescending(t => t.NgayDang)
            .Take(12)
            .ToListAsync();

        activities.AddRange(clubDocuments
            .Where(t => t.NgayDang.HasValue)
            .Select(t => new AdminActivityViewModel
            {
                Time = t.NgayDang!.Value,
                ActorName = t.NguoiDangNavigation.HoTen,
                ActorRole = t.NguoiDangNavigation.VaiTro,
                Action = "Tải tài liệu CLB",
                Target = t.MaClbNavigation.TenClb,
                Description = t.TieuDe,
                Tone = "blue",
                Url = Url.Action("Details", "TaiKhoans", new { id = t.NguoiDang })
            }));

        var notifications = await _context.ThongBaos
            .Include(t => t.MaTaiKhoanNavigation)
            .OrderByDescending(t => t.NgayTao)
            .Take(12)
            .ToListAsync();

        activities.AddRange(notifications
            .Where(t => t.NgayTao.HasValue)
            .Select(t => new AdminActivityViewModel
            {
                Time = t.NgayTao!.Value,
                ActorName = "Hệ thống",
                ActorRole = "System",
                Action = t.TieuDe,
                Target = t.MaTaiKhoanNavigation.HoTen,
                Description = t.NoiDung ?? "Thông báo hệ thống.",
                Tone = "gray",
                Url = Url.Action("Details", "TaiKhoans", new { id = t.MaTaiKhoan })
            }));

        return activities
            .OrderByDescending(a => a.Time)
            .Take(30)
            .ToList();
    }
}

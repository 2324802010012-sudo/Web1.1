using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
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

        var pendingCount = await _context.DangKyHuongDans
            .CountAsync(d => d.TrangThaiCoVan == "Chờ duyệt" || d.TrangThaiDuyet == "Chờ duyệt");
        var pendingApplications = await _context.DangKyHuongDans
            .Include(d => d.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
            .Include(d => d.MaLinhVucNavigation)
            .Where(d => d.TrangThaiCoVan == "Chờ duyệt" || d.TrangThaiDuyet == "Chờ duyệt")
            .OrderByDescending(d => d.NgayDangKy)
            .Take(8)
            .ToListAsync();

        ViewBag.UserName = CurrentUserName;
        ViewBag.HoSoChoDuyet = pendingCount;
        ViewBag.Mentor = await _context.NguoiHuongDans.CountAsync();
        ViewBag.YeuCau = await _context.YeuCauHoTroHocTaps.CountAsync();
        ViewBag.BaoCao = await _context.BaoCaoBuoiHocs.CountAsync();
        ViewBag.DanhGia = await _context.DanhGiaHuongDans.CountAsync();
        ViewBag.PendingMentorApplications = pendingApplications;
        ViewBag.Notifications = pendingApplications.Take(3).Select(d => new DashboardNotificationViewModel
        {
            Title = "Hồ sơ mentor chờ duyệt",
            Message = $"{d.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen} đăng ký làm mentor {d.MaLinhVucNavigation.TenLinhVuc}.",
            Url = Url.Action(nameof(DangKyMentorDetails), "CoVan", new { id = d.MaDangKy }),
            Tone = "orange"
        }).ToList();

        var mentors = await _context.NguoiHuongDans
            .Include(m => m.MaTaiKhoanNavigation)
            .Include(m => m.ChuyenMonNguoiHuongDans)
                .ThenInclude(c => c.MaLinhVucNavigation)
            .OrderByDescending(m => m.DiemUyTin)
            .ThenByDescending(m => m.DiemDanhGia)
            .Take(10)
            .ToListAsync();

        ViewBag.ManagedMentors = mentors.Select(m => new ManagedMentorViewModel
        {
            HoTen = m.MaTaiKhoanNavigation.HoTen,
            Email = m.MaTaiKhoanNavigation.Email,
            ChuyenMon = string.Join(", ", m.ChuyenMonNguoiHuongDans.Select(c => c.MaLinhVucNavigation.TenLinhVuc)),
            DiemDanhGia = m.DiemDanhGia ?? 0,
            DiemUyTin = m.DiemUyTin ?? 0,
            SoLuotDanhGia = m.SoLuotDanhGia ?? 0,
            TrangThai = m.TrangThai ?? "-"
        }).ToList();

        return View();
    }

    public async Task<IActionResult> DangKyMentorDetails(int id)
    {
        var guard = RequireRoles(AccountRoles.CoVan);
        if (guard != null) return guard;

        var application = await _context.DangKyHuongDans
            .Include(d => d.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
                    .ThenInclude(t => t.LichRanhs)
            .Include(d => d.MaLinhVucNavigation)
            .FirstOrDefaultAsync(d => d.MaDangKy == id);

        if (application == null) return NotFound();
        return View(application);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DuyetMentor(int id)
    {
        var guard = RequireRoles(AccountRoles.CoVan);
        if (guard != null) return guard;

        var application = await _context.DangKyHuongDans
            .Include(d => d.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
            .FirstOrDefaultAsync(d => d.MaDangKy == id);
        if (application == null) return NotFound();

        var account = application.MaSinhVienNavigation.MaTaiKhoanNavigation;
        var mentor = await _context.NguoiHuongDans
            .Include(m => m.ChuyenMonNguoiHuongDans)
            .FirstOrDefaultAsync(m => m.MaTaiKhoan == account.MaTaiKhoan);

        if (mentor == null)
        {
            mentor = new NguoiHuongDan
            {
                MaTaiKhoan = account.MaTaiKhoan,
                LoaiNguoiHuongDan = "Sinh viên mentor",
                DiemDanhGia = 0,
                DiemUyTin = 6,
                SoLuotDanhGia = 0,
                TrangThai = "Hoạt động"
            };
            _context.NguoiHuongDans.Add(mentor);
            await _context.SaveChangesAsync();
        }
        else
        {
            mentor.TrangThai = "Hoạt động";
            mentor.LoaiNguoiHuongDan = string.IsNullOrWhiteSpace(mentor.LoaiNguoiHuongDan) ? "Sinh viên mentor" : mentor.LoaiNguoiHuongDan;
            mentor.DiemUyTin ??= 6;
            mentor.DiemDanhGia ??= 0;
            mentor.SoLuotDanhGia ??= 0;
        }

        var specialty = mentor.ChuyenMonNguoiHuongDans.FirstOrDefault(c => c.MaLinhVuc == application.MaLinhVuc);
        if (specialty == null)
        {
            specialty = new ChuyenMonNguoiHuongDan
            {
                MaHuongDan = mentor.MaHuongDan,
                MaLinhVuc = application.MaLinhVuc
            };
            _context.ChuyenMonNguoiHuongDans.Add(specialty);
        }

        specialty.MucDoThanhThao = ScoreToLevel(application.DiemMon);
        specialty.MoTaKinhNghiem = BuildExperienceText(application);

        account.VaiTro = AccountRoles.Mentor;
        application.TrangThaiCoVan = "Đã duyệt";
        application.TrangThaiDuyet = "Đã duyệt";

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã duyệt {account.HoTen} làm mentor. Tài khoản này có thể đăng nhập vào dashboard mentor.";
        return RedirectToAction(nameof(DangKyMentorDetails), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TuChoiMentor(int id)
    {
        var guard = RequireRoles(AccountRoles.CoVan);
        if (guard != null) return guard;

        var application = await _context.DangKyHuongDans
            .Include(d => d.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
            .FirstOrDefaultAsync(d => d.MaDangKy == id);
        if (application == null) return NotFound();

        application.TrangThaiCoVan = "Từ chối";
        application.TrangThaiDuyet = "Từ chối";
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã từ chối đơn đăng ký mentor của {application.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen}.";
        return RedirectToAction(nameof(Index), null, null, "duyet-mentor");
    }

    private static int ScoreToLevel(decimal? score)
    {
        if (!score.HasValue) return 7;
        return Math.Clamp((int)Math.Round(score.Value), 1, 10);
    }

    private static string BuildExperienceText(DangKyHuongDan application)
    {
        var parts = new List<string>();
        if (application.DiemMon.HasValue) parts.Add($"Điểm môn: {application.DiemMon:0.0}/10.");
        if (!string.IsNullOrWhiteSpace(application.MinhChung)) parts.Add($"Minh chứng: {application.MinhChung.Trim()}.");
        if (!string.IsNullOrWhiteSpace(application.LyDo)) parts.Add(application.LyDo.Trim());
        return string.Join(" ", parts);
    }
}

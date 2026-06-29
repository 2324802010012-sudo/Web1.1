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
        var allSchedulesForStats = await _context.LichHocs
            .Include(l => l.BaoCaoBuoiHoc)
            .ToListAsync();
        ViewBag.BuoiHocHoanThanh = allSchedulesForStats.Count(IsCompletedSession);
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
                .ThenInclude(t => t.LichRanhs)
            .Include(m => m.ChuyenMonNguoiHuongDans)
                .ThenInclude(c => c.MaLinhVucNavigation)
            .Include(m => m.GhepNoiHocTaps)
                .ThenInclude(g => g.LichHocs)
                    .ThenInclude(l => l.BaoCaoBuoiHoc)
            .Include(m => m.GhepNoiHocTaps)
                .ThenInclude(g => g.DanhGiaHuongDans)
            .ToListAsync();

        var mentorMetrics = mentors.ToDictionary(m => m.MaHuongDan, CalculateMentorQuality);
        var hasScoreChanges = false;
        foreach (var mentor in mentors)
        {
            var metrics = mentorMetrics[mentor.MaHuongDan];
            if (mentor.SoLuotDanhGia != metrics.ReviewCount
                || mentor.DiemDanhGia != metrics.AverageRating
                || mentor.DiemUyTin != metrics.Reputation)
            {
                mentor.SoLuotDanhGia = metrics.ReviewCount;
                mentor.DiemDanhGia = metrics.AverageRating;
                mentor.DiemUyTin = metrics.Reputation;
                hasScoreChanges = true;
            }
        }

        if (hasScoreChanges)
        {
            await _context.SaveChangesAsync();
        }

        mentors = mentors
            .OrderByDescending(m => mentorMetrics[m.MaHuongDan].Reputation)
            .ThenByDescending(m => mentorMetrics[m.MaHuongDan].AverageRating)
            .ThenByDescending(m => mentorMetrics[m.MaHuongDan].ReviewCount)
            .ThenBy(m => m.MaTaiKhoanNavigation.HoTen)
            .ToList();

        ViewBag.MentorHoatDong = mentors.Count(m => m.TrangThai == null || m.TrangThai == "Hoạt động");
        ViewBag.MentorCanChuY = mentors.Count(m =>
            m.TrangThai != "Hoạt động" ||
            !m.MaTaiKhoanNavigation.LichRanhs.Any() ||
            (m.SoLuotDanhGia.GetValueOrDefault() >= 3 && m.DiemDanhGia.GetValueOrDefault() < 4));

        ViewBag.ManagedMentors = mentors.Select((m, index) =>
        {
            var metrics = mentorMetrics[m.MaHuongDan];
            return new ManagedMentorViewModel
            {
                MaHuongDan = m.MaHuongDan,
                HoTen = m.MaTaiKhoanNavigation.HoTen,
                Email = m.MaTaiKhoanNavigation.Email,
                LoaiNguoiHuongDan = m.LoaiNguoiHuongDan,
                ChuyenMon = string.Join(", ", m.ChuyenMonNguoiHuongDans.Select(c => c.MaLinhVucNavigation.TenLinhVuc)),
                DiemDanhGia = metrics.AverageRating,
                DiemUyTin = metrics.Reputation,
                SoLuotDanhGia = metrics.ReviewCount,
                ThuHang = index + 1,
                SoBuoiHoanThanh = metrics.CompletedSessions,
                SoBaoCao = 0,
                CongThucUyTin = metrics.FormulaText,
                TrangThai = m.TrangThai ?? "Hoạt động",
                SoGhepNoi = m.GhepNoiHocTaps.Count,
                SoBuoiHoc = m.GhepNoiHocTaps.Sum(g => g.LichHocs.Count),
                BaoCaoChoDanhGia = 0,
                SoKhungLichRanh = m.MaTaiKhoanNavigation.LichRanhs.Count
            };
        }).ToList();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMentor(int id, string? trangThai, string? loaiNguoiHuongDan)
    {
        var guard = RequireRoles(AccountRoles.CoVan);
        if (guard != null) return guard;

        var mentor = await _context.NguoiHuongDans
            .Include(m => m.MaTaiKhoanNavigation)
            .FirstOrDefaultAsync(m => m.MaHuongDan == id);

        if (mentor == null) return NotFound();

        var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Hoạt động",
            "Tạm dừng",
            "Cần theo dõi"
        };

        mentor.TrangThai = string.IsNullOrWhiteSpace(trangThai) || !allowedStatuses.Contains(trangThai.Trim())
            ? "Hoạt động"
            : trangThai.Trim();

        if (!string.IsNullOrWhiteSpace(loaiNguoiHuongDan))
        {
            mentor.LoaiNguoiHuongDan = loaiNguoiHuongDan.Trim().Length > 50
                ? loaiNguoiHuongDan.Trim()[..50]
                : loaiNguoiHuongDan.Trim();
        }

        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = mentor.MaTaiKhoan,
            TieuDe = "Cố vấn cập nhật hồ sơ mentor",
            NoiDung = $"Trạng thái mentor của bạn hiện là {mentor.TrangThai}. Điểm uy tín được hệ thống tự tính từ đánh giá, buổi học và số lượt đánh giá.",
            LoaiThongBao = "QuanLyMentor",
            DaDoc = false,
            NgayTao = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã cập nhật mentor {mentor.MaTaiKhoanNavigation.HoTen}.";
        return RedirectToAction(nameof(Index), null, null, "mentor-quan-ly");
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
                MaSinhVien = application.MaSinhVien,
                LoaiNguoiHuongDan = "SinhVien",
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
            mentor.MaSinhVien ??= application.MaSinhVien;
            mentor.LoaiNguoiHuongDan = "SinhVien";
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
        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = account.MaTaiKhoan,
            TieuDe = "Hồ sơ mentor đã được duyệt",
            NoiDung = "Bạn đã trở thành mentor của StudyConnect. Hãy đăng nhập lại để vào dashboard Mentor.",
            LoaiThongBao = "DangKyMentor",
            DaDoc = false,
            NgayTao = DateTime.Now
        });

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
        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = application.MaSinhVienNavigation.MaTaiKhoanNavigation.MaTaiKhoan,
            TieuDe = "Hồ sơ mentor bị từ chối",
            NoiDung = "Cố vấn chưa duyệt hồ sơ mentor của bạn. Bạn có thể cập nhật minh chứng và gửi lại.",
            LoaiThongBao = "DangKyMentor",
            DaDoc = false,
            NgayTao = DateTime.Now
        });
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

    private static MentorQualityMetrics CalculateMentorQuality(NguoiHuongDan mentor)
    {
        var ratings = mentor.GhepNoiHocTaps
            .SelectMany(g => g.DanhGiaHuongDans)
            .Where(d => d.SoSao.HasValue)
            .Select(d => d.SoSao!.Value)
            .ToList();

        var reviewCount = ratings.Count;
        var averageRating = reviewCount == 0 ? 0m : Math.Round((decimal)ratings.Average(), 2);
        var completedSessions = mentor.GhepNoiHocTaps
            .SelectMany(g => g.LichHocs)
            .Count(IsCompletedSession);
        var ratingScore = reviewCount == 0 ? 0m : averageRating / 5m * 7m;
        var sessionScore = Math.Min(completedSessions, 30) / 30m * 2m;
        var reviewVolumeScore = Math.Min(reviewCount, 20) / 20m;
        var reputation = Math.Round(Math.Clamp(ratingScore + sessionScore + reviewVolumeScore, 0m, 10m), 2);

        var formulaText = $"70% đánh giá ({averageRating:0.0}/5), 20% buổi hoàn thành ({completedSessions}/30), 10% số lượt đánh giá ({reviewCount}/20).";
        return new MentorQualityMetrics(reputation, averageRating, reviewCount, completedSessions, 0, formulaText);
    }

    private static bool IsCompletedSession(LichHoc schedule)
    {
        return schedule.TrangThai == "Đã học" || schedule.TrangThai == "Đã hoàn thành" || schedule.BaoCaoBuoiHoc != null;
    }

    private sealed record MentorQualityMetrics(
        decimal Reputation,
        decimal AverageRating,
        int ReviewCount,
        int CompletedSessions,
        int ReportCount,
        string FormulaText);
}

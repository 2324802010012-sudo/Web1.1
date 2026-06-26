using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
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
            .Include(m => m.MaTaiKhoanNavigation)
                .ThenInclude(t => t.LichRanhs)
            .FirstOrDefaultAsync(m => m.MaTaiKhoan == CurrentUserId);
        var mentorId = mentor?.MaHuongDan ?? 0;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var matches = mentorId == 0
            ? new List<GhepNoiHocTap>()
            : await MentorMatchesQuery(mentorId)
                .OrderByDescending(g => g.NgayGhep)
                .Take(8)
                .ToListAsync();

        var sessions = mentorId == 0
            ? new List<LichHoc>()
            : await _context.LichHocs
                .Include(l => l.MaGhepNoiNavigation)
                    .ThenInclude(g => g.MaYeuCauNavigation)
                        .ThenInclude(y => y.MaSinhVienNavigation)
                            .ThenInclude(s => s.MaTaiKhoanNavigation)
                .Include(l => l.MaGhepNoiNavigation)
                    .ThenInclude(g => g.MaYeuCauNavigation)
                        .ThenInclude(y => y.MaLinhVucNavigation)
                .Include(l => l.BaoCaoBuoiHoc)
                .Where(l => l.MaGhepNoiNavigation.MaHuongDan == mentorId)
                .OrderBy(l => l.NgayHoc)
                .ThenBy(l => l.GioBatDau)
                .Take(8)
                .ToListAsync();

        ViewBag.UserName = CurrentUserName;
        ViewBag.GhepNoi = mentorId == 0 ? 0 : await _context.GhepNoiHocTaps.CountAsync(g => g.MaHuongDan == mentorId);
        ViewBag.LichHoc = sessions.Count(l => l.NgayHoc >= today && l.TrangThai != "Đã hoàn thành");
        ViewBag.BaoCao = mentorId == 0 ? 0 : await _context.BaoCaoBuoiHocs.CountAsync(b => b.MaLichHocNavigation.MaGhepNoiNavigation.MaHuongDan == mentorId);
        ViewBag.DanhGia = mentor?.DiemDanhGia ?? 0;
        ViewBag.UyTin = mentor?.DiemUyTin ?? 0;
        ViewBag.GhepNoiList = matches;
        ViewBag.LichHocList = sessions;
        ViewBag.LichRanhList = mentor?.MaTaiKhoanNavigation.LichRanhs.OrderBy(l => l.Thu).ThenBy(l => l.GioBatDau).ToList() ?? new List<LichRanh>();
        ViewBag.Notifications = BuildMentorNotifications(matches, sessions);

        return View();
    }

    public async Task<IActionResult> Details(int id)
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        var match = await CurrentMentorMatchAsync(id);
        if (match == null) return NotFound();

        return View(match);
    }

    public async Task<IActionResult> Availability()
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        var model = new MentorAvailabilityViewModel
        {
            LichRanhDaChon = await CurrentAvailabilityValuesAsync()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Availability(MentorAvailabilityViewModel model)
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        if (model.LichRanhDaChon.Count == 0)
        {
            ModelState.AddModelError(nameof(model.LichRanhDaChon), "Vui lòng chọn ít nhất một khung lịch rảnh.");
        }

        if (!ModelState.IsValid) return View(model);

        await ReplaceAvailabilityAsync(CurrentUserId!.Value, model.LichRanhDaChon);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã cập nhật lịch rảnh cá nhân của mentor.";
        return RedirectToAction(nameof(Index), null, null, "lich-ranh");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptMatch(int id)
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        var match = await CurrentMentorMatchAsync(id);
        if (match == null) return NotFound();

        match.TrangThai = "Mentor đã chấp nhận";
        match.MaYeuCauNavigation.TrangThai = "Đang xếp lịch";
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã chấp nhận ghép nối. Sinh viên có thể lên lịch học.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectMatch(int id)
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        var match = await CurrentMentorMatchAsync(id);
        if (match == null) return NotFound();

        match.TrangThai = "Mentor từ chối";
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã từ chối ghép nối.";
        return RedirectToAction(nameof(Index), null, null, "yeu-cau-ghep-noi");
    }

    public async Task<IActionResult> BaoCao(int id)
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        var session = await CurrentMentorSessionAsync(id);
        if (session == null) return NotFound();

        ViewBag.Session = session;
        return View(new BaoCaoBuoiHocViewModel
        {
            MaLichHoc = id,
            NoiDungDaHoc = session.BaoCaoBuoiHoc?.NoiDungDaHoc ?? string.Empty,
            BaiTap = session.BaoCaoBuoiHoc?.BaiTap,
            MucDoTiepThu = session.BaoCaoBuoiHoc?.MucDoTiepThu ?? "Khá",
            NhanXet = session.BaoCaoBuoiHoc?.NhanXet
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BaoCao(BaoCaoBuoiHocViewModel model)
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        var session = await CurrentMentorSessionAsync(model.MaLichHoc);
        if (session == null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.Session = session;
            return View(model);
        }

        var report = session.BaoCaoBuoiHoc;
        if (report == null)
        {
            report = new BaoCaoBuoiHoc { MaLichHoc = session.MaLichHoc };
            _context.BaoCaoBuoiHocs.Add(report);
        }

        report.NoiDungDaHoc = model.NoiDungDaHoc.Trim();
        report.BaiTap = string.IsNullOrWhiteSpace(model.BaiTap) ? null : model.BaiTap.Trim();
        report.MucDoTiepThu = model.MucDoTiepThu;
        report.NhanXet = string.IsNullOrWhiteSpace(model.NhanXet) ? null : model.NhanXet.Trim();
        report.NgayBaoCao = DateTime.Now;

        session.TrangThai = "Đã hoàn thành";
        session.MaGhepNoiNavigation.TrangThai = "Chờ sinh viên đánh giá";
        session.MaGhepNoiNavigation.MaYeuCauNavigation.TrangThai = "Chờ đánh giá";

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã lưu báo cáo buổi học.";
        return RedirectToAction(nameof(Index), null, null, "bao-cao");
    }

    private async Task<NguoiHuongDan?> CurrentMentorAsync()
    {
        return await _context.NguoiHuongDans.FirstOrDefaultAsync(m => m.MaTaiKhoan == CurrentUserId);
    }

    private IQueryable<GhepNoiHocTap> MentorMatchesQuery(int mentorId)
    {
        return _context.GhepNoiHocTaps
            .Include(g => g.MaYeuCauNavigation)
                .ThenInclude(y => y.MaLinhVucNavigation)
            .Include(g => g.MaYeuCauNavigation)
                .ThenInclude(y => y.MaSinhVienNavigation)
                    .ThenInclude(s => s.MaTaiKhoanNavigation)
                        .ThenInclude(t => t.LichRanhs)
            .Include(g => g.LichHocs)
                .ThenInclude(l => l.BaoCaoBuoiHoc)
            .Include(g => g.DanhGiaHuongDans)
            .Where(g => g.MaHuongDan == mentorId);
    }

    private async Task<GhepNoiHocTap?> CurrentMentorMatchAsync(int matchId)
    {
        var mentor = await CurrentMentorAsync();
        if (mentor == null) return null;

        return await MentorMatchesQuery(mentor.MaHuongDan)
            .FirstOrDefaultAsync(g => g.MaGhepNoi == matchId);
    }

    private async Task<LichHoc?> CurrentMentorSessionAsync(int sessionId)
    {
        var mentor = await CurrentMentorAsync();
        if (mentor == null) return null;

        return await _context.LichHocs
            .Include(l => l.BaoCaoBuoiHoc)
            .Include(l => l.MaGhepNoiNavigation)
                .ThenInclude(g => g.MaYeuCauNavigation)
                    .ThenInclude(y => y.MaSinhVienNavigation)
                        .ThenInclude(s => s.MaTaiKhoanNavigation)
            .Include(l => l.MaGhepNoiNavigation)
                .ThenInclude(g => g.MaYeuCauNavigation)
                    .ThenInclude(y => y.MaLinhVucNavigation)
            .FirstOrDefaultAsync(l => l.MaLichHoc == sessionId && l.MaGhepNoiNavigation.MaHuongDan == mentor.MaHuongDan);
    }

    private async Task<List<string>> CurrentAvailabilityValuesAsync()
    {
        if (!CurrentUserId.HasValue) return [];
        var slots = await _context.LichRanhs
            .Where(l => l.MaTaiKhoan == CurrentUserId.Value)
            .OrderBy(l => l.Thu)
            .ThenBy(l => l.GioBatDau)
            .ToListAsync();

        return slots.Select(l => $"{l.Thu}|{l.GioBatDau:HH\\:mm}|{l.GioKetThuc:HH\\:mm}").ToList();
    }

    private async Task ReplaceAvailabilityAsync(int userId, List<string> values)
    {
        var oldSlots = await _context.LichRanhs.Where(l => l.MaTaiKhoan == userId).ToListAsync();
        _context.LichRanhs.RemoveRange(oldSlots);

        foreach (var slot in values.Select(ParseSlot).Where(slot => slot != null))
        {
            _context.LichRanhs.Add(new LichRanh
            {
                MaTaiKhoan = userId,
                Thu = slot!.Value.Thu,
                GioBatDau = slot.Value.BatDau,
                GioKetThuc = slot.Value.KetThuc
            });
        }
    }

    private static List<DashboardNotificationViewModel> BuildMentorNotifications(List<GhepNoiHocTap> matches, List<LichHoc> sessions)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var notifications = new List<DashboardNotificationViewModel>();

        notifications.AddRange(matches
            .Where(m => m.TrangThai == "Đề xuất")
            .Take(3)
            .Select(m => new DashboardNotificationViewModel
            {
                Title = "Yêu cầu ghép nối mới",
                Message = $"{m.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen} gửi yêu cầu {m.MaYeuCauNavigation.MaLinhVucNavigation.TenLinhVuc}.",
                Url = $"/Mentor/Details/{m.MaGhepNoi}",
                Tone = "orange"
            }));

        notifications.AddRange(sessions
            .Where(l => l.NgayHoc >= today && l.TrangThai != "Đã hoàn thành")
            .Take(3)
            .Select(l => new DashboardNotificationViewModel
            {
                Title = "Lịch dạy sắp tới",
                Message = $"{l.NgayHoc:dd/MM} {l.GioBatDau:HH\\:mm} với {l.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen}.",
                Url = l.BaoCaoBuoiHoc == null ? $"/Mentor/BaoCao/{l.MaLichHoc}" : "/Mentor",
                Tone = "blue"
            }));

        return notifications.Take(5).ToList();
    }

    private static (int Thu, TimeOnly BatDau, TimeOnly KetThuc)? ParseSlot(string value)
    {
        var parts = value.Split('|');
        if (parts.Length != 3) return null;
        if (!int.TryParse(parts[0], out var thu)) return null;
        if (!TimeOnly.TryParse(parts[1], out var batDau)) return null;
        if (!TimeOnly.TryParse(parts[2], out var ketThuc)) return null;
        return ketThuc <= batDau ? null : (thu, batDau, ketThuc);
    }
}

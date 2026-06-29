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

        var currentMonth = DateTime.Now.ToString("MM/yyyy");
        var currentRanking = mentorId == 0
            ? null
            : await _context.XepHangMentors.FirstOrDefaultAsync(x => x.MaHuongDan == mentorId && x.ThangNam == currentMonth);

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
        ViewBag.LichHoc = sessions.Count(l => l.NgayHoc >= today
            && l.TrangThai != "Đã học"
            && l.TrangThai != "Đã hoàn thành"
            && !IsAbsentStatus(l.TrangThai));
        ViewBag.DanhGiaCount = mentorId == 0 ? 0 : await _context.DanhGiaHuongDans.CountAsync(d => d.MaHuongDan == mentorId);
        ViewBag.DanhGia = mentor?.DiemDanhGia ?? 0;
        ViewBag.UyTin = mentor?.DiemUyTin ?? 0;
        ViewBag.XepHang = currentRanking?.HangTong;
        ViewBag.XepHangLinhVuc = currentRanking?.HangTheoLinhVuc;
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

        ViewBag.CurrentSlots = await CurrentAvailabilityListAsync();
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

        var selectedValues = model.LichRanhDaChon.ToList();
        var customSlot = BuildCustomSlotValue(model);
        if (customSlot.HasValue)
        {
            if (customSlot.Value.Value != null)
            {
                selectedValues.Add(customSlot.Value.Value);
            }
            else
            {
                ModelState.AddModelError(string.Empty, customSlot.Value.ErrorMessage ?? "Khung giờ tùy chỉnh không hợp lệ.");
            }
        }

        var normalizedSlots = NormalizeSlots(selectedValues);
        if (normalizedSlots.Count == 0)
        {
            ModelState.AddModelError(nameof(model.LichRanhDaChon), "Vui lòng chọn ít nhất một khung lịch rảnh.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.CurrentSlots = await CurrentAvailabilityListAsync();
            return View(model);
        }

        await ReplaceAvailabilityAsync(CurrentUserId!.Value, normalizedSlots);
        await NotifyStudentsAboutAvailabilityChangeAsync();
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã cập nhật lịch rảnh cá nhân của mentor. Hệ thống sẽ dùng lịch mới để gợi ý và kiểm tra khi sinh viên lên lịch.";
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

        match.TrangThai = "Mentor chấp nhận";
        match.MaYeuCauNavigation.TrangThai = "Đã ghép";
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmSchedule(int id)
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        var session = await CurrentMentorSessionAsync(id);
        if (session == null) return NotFound();

        if (session.TrangThai != "Chờ mentor xác nhận")
        {
            TempData["SuccessMessage"] = "Buổi học này không còn ở trạng thái chờ xác nhận.";
            return RedirectToAction(nameof(Index), null, null, "lich-hoc");
        }

        if (!IsInsideSharedAvailability(session))
        {
            TempData["SuccessMessage"] = "Khung giờ này không còn nằm trong lịch rảnh chung. Vui lòng cập nhật lịch rảnh hoặc yêu cầu sinh viên chọn lại.";
            return RedirectToAction(nameof(Index), null, null, "lich-hoc");
        }

        var conflict = await FindScheduleConflictAsync(session);
        if (conflict != null)
        {
            TempData["SuccessMessage"] = conflict;
            return RedirectToAction(nameof(Index), null, null, "lich-hoc");
        }

        session.TrangThai = "Sắp diễn ra";
        session.MaGhepNoiNavigation.TrangThai = "Đã lên lịch";
        session.MaGhepNoiNavigation.MaYeuCauNavigation.TrangThai = "Đang học";
        AddStudentNotification(session, "Mentor đã xác nhận lịch học", $"{CurrentUserName} đã xác nhận buổi học ngày {session.NgayHoc:dd/MM/yyyy} lúc {session.GioBatDau:HH\\:mm}-{session.GioKetThuc:HH\\:mm}.");

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã xác nhận lịch học. Buổi học được chuyển sang trạng thái sắp diễn ra.";
        return RedirectToAction(nameof(Index), null, null, "lich-hoc");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectSchedule(int id)
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        var session = await CurrentMentorSessionAsync(id);
        if (session == null) return NotFound();

        if (session.TrangThai == "Đã học" || session.TrangThai == "Đã hoàn thành")
        {
            TempData["SuccessMessage"] = "Buổi học đã hoàn tất nên không thể từ chối lịch.";
            return RedirectToAction(nameof(Index), null, null, "lich-hoc");
        }

        session.TrangThai = "Mentor từ chối lịch";
        if (!session.MaGhepNoiNavigation.LichHocs.Any(l => l.MaLichHoc != session.MaLichHoc && l.TrangThai == "Sắp diễn ra"))
        {
            session.MaGhepNoiNavigation.TrangThai = "Mentor chấp nhận";
            session.MaGhepNoiNavigation.MaYeuCauNavigation.TrangThai = "Đã ghép";
        }

        AddStudentNotification(session, "Mentor từ chối lịch học", $"{CurrentUserName} đã từ chối khung {session.NgayHoc:dd/MM/yyyy} {session.GioBatDau:HH\\:mm}-{session.GioKetThuc:HH\\:mm}. Bạn có thể chọn khung lịch rảnh chung khác.");

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã từ chối lịch học và gửi thông báo cho sinh viên.";
        return RedirectToAction(nameof(Index), null, null, "lich-hoc");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkStudentAbsent(int id)
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        var session = await CurrentMentorSessionAsync(id);
        if (session == null) return NotFound();

        if (session.TrangThai != "Sắp diễn ra" && session.TrangThai != "Đã lên lịch")
        {
            TempData["SuccessMessage"] = "Chỉ có thể ghi nhận vắng mặt với buổi học đã được mentor xác nhận.";
            return RedirectToAction(nameof(Index), null, null, "lich-hoc");
        }

        if (!IsSessionTimePassed(session))
        {
            TempData["SuccessMessage"] = "Chỉ có thể ghi nhận sinh viên vắng sau khi buổi học đã qua giờ kết thúc.";
            return RedirectToAction(nameof(Index), null, null, "lich-hoc");
        }

        var sinhVienId = session.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVien;
        var previousAbsences = await CountStudentAbsencesAsync(sinhVienId, session.MaLichHoc);
        var totalAbsences = previousAbsences + 1;

        session.TrangThai = "Sinh viên vắng";
        AddStudentNotification(
            session,
            totalAbsences >= 3 ? "Bạn đã bị khóa tạo yêu cầu hỗ trợ mới" : "Bạn đã vắng buổi học 1-1",
            totalAbsences >= 3
                ? "Bạn đã vắng 3 buổi học 1-1. Hệ thống tạm thời khóa quyền tạo yêu cầu hỗ trợ mới, vui lòng liên hệ cố vấn để được xem xét."
                : $"Mentor đã ghi nhận bạn vắng buổi học ngày {session.NgayHoc:dd/MM/yyyy} lúc {session.GioBatDau:HH\\:mm}-{session.GioKetThuc:HH\\:mm}. Số buổi vắng hiện tại: {totalAbsences}/3.");
        AddMentorNotification(
            totalAbsences >= 3 ? "Đã khóa tạo yêu cầu mới của sinh viên" : "Đã ghi nhận sinh viên vắng",
            totalAbsences >= 3
                ? $"{session.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen} đã vắng 3 buổi học 1-1. Hệ thống đã khóa quyền tạo yêu cầu hỗ trợ mới."
                : $"Đã ghi nhận {session.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen} vắng buổi học ngày {session.NgayHoc:dd/MM/yyyy} {session.GioBatDau:HH\\:mm}-{session.GioKetThuc:HH\\:mm}. Tổng vắng: {totalAbsences}/3.");

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = totalAbsences >= 3
            ? "Đã ghi nhận sinh viên vắng. Sinh viên đã đạt 3 buổi vắng và bị khóa tạo yêu cầu hỗ trợ mới."
            : $"Đã ghi nhận sinh viên vắng ({totalAbsences}/3).";
        return RedirectToAction(nameof(Index), null, null, "lich-hoc");
    }

    public IActionResult BaoCao(int id)
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        TempData["SuccessMessage"] = "Chức năng lập báo cáo sau buổi học đã được lược bỏ. Sinh viên sẽ đánh giá trực tiếp sau buổi học.";
        return RedirectToAction(nameof(Index), null, null, "lich-hoc");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult BaoCao(BaoCaoBuoiHocViewModel model)
    {
        var guard = RequireRoles(AccountRoles.Mentor);
        if (guard != null) return guard;

        TempData["SuccessMessage"] = "Chức năng lập báo cáo sau buổi học đã được lược bỏ.";
        return RedirectToAction(nameof(Index), null, null, "lich-hoc");
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
                .ThenInclude(g => g.LichHocs)
            .Include(l => l.MaGhepNoiNavigation)
                .ThenInclude(g => g.MaHuongDanNavigation)
                    .ThenInclude(m => m.MaTaiKhoanNavigation)
                        .ThenInclude(t => t.LichRanhs)
            .Include(l => l.MaGhepNoiNavigation)
                .ThenInclude(g => g.MaYeuCauNavigation)
                    .ThenInclude(y => y.MaSinhVienNavigation)
                        .ThenInclude(s => s.MaTaiKhoanNavigation)
                            .ThenInclude(t => t.LichRanhs)
            .Include(l => l.MaGhepNoiNavigation)
                .ThenInclude(g => g.MaYeuCauNavigation)
                    .ThenInclude(y => y.MaLinhVucNavigation)
            .FirstOrDefaultAsync(l => l.MaLichHoc == sessionId && l.MaGhepNoiNavigation.MaHuongDan == mentor.MaHuongDan);
    }

    private async Task<string?> FindScheduleConflictAsync(LichHoc session)
    {
        var mentorId = session.MaGhepNoiNavigation.MaHuongDan;
        var studentId = session.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVien;

        var conflicts = await _context.LichHocs
            .Include(l => l.MaGhepNoiNavigation)
                .ThenInclude(g => g.MaYeuCauNavigation)
                    .ThenInclude(y => y.MaSinhVienNavigation)
                        .ThenInclude(s => s.MaTaiKhoanNavigation)
            .Where(l => l.MaLichHoc != session.MaLichHoc)
            .Where(l => l.NgayHoc == session.NgayHoc)
            .Where(l => l.TrangThai != "Mentor từ chối lịch"
                && l.TrangThai != "Đã hủy"
                && l.TrangThai != "Đã hủy lịch"
                && l.TrangThai != "Sinh viên vắng"
                && l.TrangThai != "Vắng mặt"
                && l.TrangThai != "Vắng")
            .Where(l => l.MaGhepNoiNavigation.MaHuongDan == mentorId
                || l.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVien == studentId)
            .ToListAsync();

        var conflict = conflicts.FirstOrDefault(l => TimeOverlaps(session.GioBatDau, session.GioKetThuc, l.GioBatDau, l.GioKetThuc));
        if (conflict == null) return null;

        if (conflict.MaGhepNoiNavigation.MaHuongDan == mentorId)
        {
            var otherStudent = conflict.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen;
            return $"Không thể xác nhận vì khung này trùng với lịch mentor đã có với {otherStudent} ({conflict.NgayHoc:dd/MM/yyyy} {conflict.GioBatDau:HH\\:mm}-{conflict.GioKetThuc:HH\\:mm}).";
        }

        return $"Không thể xác nhận vì khung này trùng với lịch học khác của sinh viên ({conflict.NgayHoc:dd/MM/yyyy} {conflict.GioBatDau:HH\\:mm}-{conflict.GioKetThuc:HH\\:mm}).";
    }

    private static bool IsInsideSharedAvailability(LichHoc session)
    {
        var match = session.MaGhepNoiNavigation;
        var thu = session.NgayHoc.DayOfWeek == DayOfWeek.Sunday ? 8 : (int)session.NgayHoc.DayOfWeek + 1;
        var studentSlots = match.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.LichRanhs.Where(l => l.Thu == thu);
        var mentorSlots = match.MaHuongDanNavigation.MaTaiKhoanNavigation.LichRanhs.Where(l => l.Thu == thu);

        return studentSlots.Any(student => mentorSlots.Any(mentor =>
        {
            var sharedStart = student.GioBatDau > mentor.GioBatDau ? student.GioBatDau : mentor.GioBatDau;
            var sharedEnd = student.GioKetThuc < mentor.GioKetThuc ? student.GioKetThuc : mentor.GioKetThuc;
            return session.GioBatDau >= sharedStart && session.GioKetThuc <= sharedEnd;
        }));
    }

    private void AddStudentNotification(LichHoc session, string title, string message)
    {
        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = session.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoan,
            TieuDe = title,
            NoiDung = message,
            LoaiThongBao = "VangHoc",
            DaDoc = false,
            NgayTao = DateTime.Now
        });
    }

    private void AddMentorNotification(string title, string message)
    {
        if (!CurrentUserId.HasValue) return;

        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = CurrentUserId.Value,
            TieuDe = title,
            NoiDung = message,
            LoaiThongBao = "VangHoc",
            DaDoc = false,
            NgayTao = DateTime.Now
        });
    }

    private async Task<int> CountStudentAbsencesAsync(int sinhVienId, int? excludingSessionId = null)
    {
        return await _context.LichHocs
            .Include(l => l.MaGhepNoiNavigation)
                .ThenInclude(g => g.MaYeuCauNavigation)
            .Where(l => l.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVien == sinhVienId)
            .Where(l => !excludingSessionId.HasValue || l.MaLichHoc != excludingSessionId.Value)
            .CountAsync(l => l.TrangThai == "Sinh viên vắng"
                || l.TrangThai == "Vắng mặt"
                || l.TrangThai == "Vắng");
    }

    private static bool IsAbsentStatus(string? status)
    {
        return status == "Sinh viên vắng" || status == "Vắng mặt" || status == "Vắng";
    }

    private static bool IsSessionTimePassed(LichHoc session)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (session.NgayHoc < today) return true;
        if (session.NgayHoc > today) return false;
        return session.GioKetThuc <= TimeOnly.FromDateTime(DateTime.Now);
    }

    private static bool TimeOverlaps(TimeOnly firstStart, TimeOnly firstEnd, TimeOnly secondStart, TimeOnly secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private static bool IsCompletedSession(LichHoc schedule)
    {
        if (IsAbsentStatus(schedule.TrangThai))
        {
            return false;
        }

        if (schedule.TrangThai == "Đã học" || schedule.TrangThai == "Đã hoàn thành" || schedule.BaoCaoBuoiHoc != null)
        {
            return true;
        }

        if (schedule.TrangThai != "Sắp diễn ra" && schedule.TrangThai != "Đã lên lịch")
        {
            return false;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (schedule.NgayHoc < today) return true;
        if (schedule.NgayHoc > today) return false;
        return schedule.GioKetThuc <= TimeOnly.FromDateTime(DateTime.Now);
    }

    private async Task UpdateStudyHistoryAsync(GhepNoiHocTap match)
    {
        var completedSessions = match.LichHocs
            .Where(IsCompletedSession)
            .OrderBy(l => l.NgayHoc)
            .ThenBy(l => l.GioBatDau)
            .ToList();

        var latestReport = completedSessions
            .Select(l => l.BaoCaoBuoiHoc)
            .LastOrDefault(r => r != null);

        var history = await _context.LichSuHocTaps.FirstOrDefaultAsync(h =>
            h.MaGhepNoi == match.MaGhepNoi && h.MaSinhVien == match.MaYeuCauNavigation.MaSinhVien);

        if (history == null)
        {
            history = new LichSuHocTap
            {
                MaGhepNoi = match.MaGhepNoi,
                MaSinhVien = match.MaYeuCauNavigation.MaSinhVien
            };
            _context.LichSuHocTaps.Add(history);
        }

        history.SoBuoiDaHoc = completedSessions.Count;
        history.TienDo = match.TrangThai == "Hoàn thành"
            ? "Hoàn thành"
            : completedSessions.Count > 0 ? "Đang học" : "Mới ghép nối";
        history.KetQuaTongHop = latestReport == null
            ? $"Ghép nối {match.MaGhepNoi} đang theo dõi tiến độ."
            : $"Đã học {completedSessions.Count} buổi. Gần nhất: {latestReport.NoiDungDaHoc}";
        history.NgayCapNhat = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    private async Task UpdateMentorRatingAndRankingAsync(int mentorId)
    {
        var mentor = await _context.NguoiHuongDans
            .Include(m => m.ChuyenMonNguoiHuongDans)
            .Include(m => m.GhepNoiHocTaps)
                .ThenInclude(g => g.LichHocs)
                    .ThenInclude(l => l.BaoCaoBuoiHoc)
            .Include(m => m.DanhGiaHuongDans)
            .FirstOrDefaultAsync(m => m.MaHuongDan == mentorId);

        if (mentor == null) return;

        var quality = CalculateMentorQuality(mentor);
        mentor.SoLuotDanhGia = quality.ReviewCount;
        mentor.DiemDanhGia = quality.AverageRating;
        mentor.DiemUyTin = quality.Reputation;

        var month = DateTime.Now.ToString("MM/yyyy");
        var primaryFieldId = mentor.ChuyenMonNguoiHuongDans
            .OrderByDescending(c => c.MucDoThanhThao ?? 0)
            .Select(c => c.MaLinhVuc)
            .FirstOrDefault();

        var activeMentors = await _context.NguoiHuongDans
            .Include(m => m.ChuyenMonNguoiHuongDans)
            .Where(m => m.TrangThai == null || m.TrangThai == "Hoạt động")
            .ToListAsync();

        var overallRank = activeMentors
            .Select(m => new { MentorId = m.MaHuongDan, Score = CalculateMentorQuality(m).Reputation })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.MentorId)
            .Select((x, index) => new { x.MentorId, Rank = index + 1 })
            .FirstOrDefault(x => x.MentorId == mentorId)?.Rank ?? 1;

        int? fieldRank = null;
        if (primaryFieldId != 0)
        {
            fieldRank = activeMentors
                .Where(m => m.ChuyenMonNguoiHuongDans.Any(c => c.MaLinhVuc == primaryFieldId))
                .Select(m => new { MentorId = m.MaHuongDan, Score = CalculateMentorQuality(m).Reputation })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.MentorId)
                .Select((x, index) => new { x.MentorId, Rank = index + 1 })
                .FirstOrDefault(x => x.MentorId == mentorId)?.Rank;
        }

        var ranking = await _context.XepHangMentors
            .FirstOrDefaultAsync(x => x.MaHuongDan == mentorId && x.ThangNam == month);

        if (ranking == null)
        {
            ranking = new XepHangMentor
            {
                MaHuongDan = mentorId,
                ThangNam = month
            };
            _context.XepHangMentors.Add(ranking);
        }

        ranking.DiemUyTin = quality.Reputation;
        ranking.HangTong = overallRank;
        ranking.HangTheoLinhVuc = fieldRank;

        await _context.SaveChangesAsync();
    }

    private async Task<List<string>> CurrentAvailabilityValuesAsync()
    {
        var slots = await CurrentAvailabilityListAsync();
        return slots.Select(l => $"{l.Thu}|{l.GioBatDau:HH\\:mm}|{l.GioKetThuc:HH\\:mm}").ToList();
    }

    private async Task<List<LichRanh>> CurrentAvailabilityListAsync()
    {
        if (!CurrentUserId.HasValue) return [];
        return await _context.LichRanhs
            .Where(l => l.MaTaiKhoan == CurrentUserId.Value)
            .OrderBy(l => l.Thu)
            .ThenBy(l => l.GioBatDau)
            .ToListAsync();
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

    private async Task NotifyStudentsAboutAvailabilityChangeAsync()
    {
        var mentor = await CurrentMentorAsync();
        if (mentor == null) return;

        var studentAccountIds = await _context.GhepNoiHocTaps
            .Include(g => g.MaYeuCauNavigation)
                .ThenInclude(y => y.MaSinhVienNavigation)
            .Where(g => g.MaHuongDan == mentor.MaHuongDan)
            .Where(g => g.TrangThai == "Mentor chấp nhận" || g.TrangThai == "Mentor đã chấp nhận" || g.TrangThai == "Đã lên lịch" || g.TrangThai == "Đề xuất" || g.TrangThai == "Đã gửi yêu cầu")
            .Select(g => g.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoan)
            .Distinct()
            .ToListAsync();

        foreach (var accountId in studentAccountIds)
        {
            _context.ThongBaos.Add(new ThongBao
            {
                MaTaiKhoan = accountId,
                TieuDe = "Mentor đã cập nhật lịch rảnh",
                NoiDung = $"{CurrentUserName} vừa cập nhật lịch rảnh. Bạn có thể vào yêu cầu học 1-1 để chọn thời gian phù hợp.",
                LoaiThongBao = "LichRanhMentor",
                DaDoc = false,
                NgayTao = DateTime.Now
            });
        }
    }

    private static List<DashboardNotificationViewModel> BuildMentorNotifications(List<GhepNoiHocTap> matches, List<LichHoc> sessions)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var notifications = new List<DashboardNotificationViewModel>();

        notifications.AddRange(matches
            .Where(m => m.TrangThai == "Đề xuất" || m.TrangThai == "Đã gửi yêu cầu")
            .Take(3)
            .Select(m => new DashboardNotificationViewModel
            {
                Title = "Yêu cầu ghép nối mới",
                Message = $"{m.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen} gửi yêu cầu {m.MaYeuCauNavigation.MaLinhVucNavigation.TenLinhVuc}.",
                Url = $"/Mentor/Details/{m.MaGhepNoi}",
                Tone = "orange"
            }));

        notifications.AddRange(sessions
            .Where(l => l.TrangThai == "Chờ mentor xác nhận")
            .Take(3)
            .Select(l => new DashboardNotificationViewModel
            {
                Title = "Lịch học cần xác nhận",
                Message = $"{l.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen} đề xuất {l.NgayHoc:dd/MM} {l.GioBatDau:HH\\:mm}-{l.GioKetThuc:HH\\:mm}.",
                Url = "/Mentor#lich-hoc",
                Tone = "orange"
            }));

        notifications.AddRange(sessions
            .Where(l => l.NgayHoc >= today
                && l.TrangThai != "Đã học"
                && l.TrangThai != "Đã hoàn thành"
                && !IsAbsentStatus(l.TrangThai))
            .Where(l => l.TrangThai != "Chờ mentor xác nhận" && l.TrangThai != "Mentor từ chối lịch")
            .Take(3)
            .Select(l => new DashboardNotificationViewModel
            {
                Title = "Lịch dạy sắp tới",
                Message = $"{l.NgayHoc:dd/MM} {l.GioBatDau:HH\\:mm} với {l.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen}.",
                Url = "/Mentor#lich-hoc",
                Tone = "blue"
            }));

        return notifications.Take(5).ToList();
    }

    private static MentorQualityMetrics CalculateMentorQuality(NguoiHuongDan mentor)
    {
        var ratings = mentor.DanhGiaHuongDans
            .Where(d => d.SoSao.HasValue)
            .Select(d => d.SoSao!.Value)
            .ToList();

        if (ratings.Count == 0)
        {
            ratings = mentor.GhepNoiHocTaps
                .SelectMany(g => g.DanhGiaHuongDans)
                .Where(d => d.SoSao.HasValue)
                .Select(d => d.SoSao!.Value)
                .ToList();
        }

        var reviewCount = ratings.Count;
        var averageRating = reviewCount == 0 ? 0m : Math.Round((decimal)ratings.Average(), 2);
        var completedSessions = mentor.GhepNoiHocTaps
            .SelectMany(g => g.LichHocs)
            .Count(IsCompletedSession);

        var ratingScore = reviewCount == 0 ? 0m : averageRating / 5m * 7m;
        var sessionScore = Math.Min(completedSessions, 30) / 30m * 2m;
        var reviewVolumeScore = Math.Min(reviewCount, 20) / 20m;
        var reputation = Math.Round(Math.Clamp(ratingScore + sessionScore + reviewVolumeScore, 0m, 10m), 2);

        return new MentorQualityMetrics(reputation, averageRating, reviewCount);
    }

    private sealed record MentorQualityMetrics(decimal Reputation, decimal AverageRating, int ReviewCount);

    private static (int Thu, TimeOnly BatDau, TimeOnly KetThuc)? ParseSlot(string value)
    {
        var parts = value.Split('|');
        if (parts.Length != 3) return null;
        if (!int.TryParse(parts[0], out var thu)) return null;
        if (thu < 2 || thu > 8) return null;
        if (!TimeOnly.TryParse(parts[1], out var batDau)) return null;
        if (!TimeOnly.TryParse(parts[2], out var ketThuc)) return null;
        return ketThuc <= batDau ? null : (thu, batDau, ketThuc);
    }

    private static (string? Value, string? ErrorMessage)? BuildCustomSlotValue(MentorAvailabilityViewModel model)
    {
        var hasAnyCustomValue = model.ThuTuyChinh.HasValue || model.GioBatDauTuyChinh.HasValue || model.GioKetThucTuyChinh.HasValue;
        if (!hasAnyCustomValue) return null;

        if (!model.ThuTuyChinh.HasValue || !model.GioBatDauTuyChinh.HasValue || !model.GioKetThucTuyChinh.HasValue)
        {
            return (null, "Vui lòng nhập đủ thứ, giờ bắt đầu và giờ kết thúc cho khung giờ tùy chỉnh.");
        }

        if (model.ThuTuyChinh.Value < 2 || model.ThuTuyChinh.Value > 8)
        {
            return (null, "Thứ trong tuần không hợp lệ.");
        }

        if (model.GioKetThucTuyChinh.Value <= model.GioBatDauTuyChinh.Value)
        {
            return (null, "Giờ kết thúc tùy chỉnh phải sau giờ bắt đầu.");
        }

        if ((model.GioKetThucTuyChinh.Value - model.GioBatDauTuyChinh.Value).TotalMinutes < 30)
        {
            return (null, "Mỗi khung lịch rảnh nên kéo dài ít nhất 30 phút.");
        }

        return ($"{model.ThuTuyChinh.Value}|{model.GioBatDauTuyChinh.Value:hh\\:mm}|{model.GioKetThucTuyChinh.Value:hh\\:mm}", null);
    }

    private static List<string> NormalizeSlots(List<string> values)
    {
        return values
            .Select(ParseSlot)
            .Where(slot => slot != null)
            .Select(slot => slot!.Value)
            .GroupBy(slot => slot.Thu)
            .OrderBy(group => group.Key)
            .SelectMany(group =>
            {
                var orderedSlots = group
                    .OrderBy(slot => slot.BatDau)
                    .ThenBy(slot => slot.KetThuc)
                    .ToList();
                var merged = new List<(int Thu, TimeOnly BatDau, TimeOnly KetThuc)>();

                foreach (var slot in orderedSlots)
                {
                    if (merged.Count == 0)
                    {
                        merged.Add(slot);
                        continue;
                    }

                    var last = merged[^1];
                    if (slot.BatDau <= last.KetThuc)
                    {
                        merged[^1] = (last.Thu, last.BatDau, slot.KetThuc > last.KetThuc ? slot.KetThuc : last.KetThuc);
                    }
                    else
                    {
                        merged.Add(slot);
                    }
                }

                return merged;
            })
            .Select(slot => $"{slot.Thu}|{slot.BatDau:HH\\:mm}|{slot.KetThuc:HH\\:mm}")
            .ToList();
    }
}

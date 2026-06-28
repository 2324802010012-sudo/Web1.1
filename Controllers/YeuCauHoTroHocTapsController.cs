using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class YeuCauHoTroHocTapsController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public YeuCauHoTroHocTapsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var sinhVien = await CurrentSinhVienAsync();
        if (sinhVien == null)
        {
            return RedirectToAction("HoSo", "SinhVien", new { returnUrl = Url.Action(nameof(Index), "YeuCauHoTroHocTaps") });
        }

        var requests = await _context.YeuCauHoTroHocTaps
            .Include(y => y.MaLinhVucNavigation)
            .Include(y => y.GhepNoiHocTaps)
                .ThenInclude(g => g.MaHuongDanNavigation)
                    .ThenInclude(m => m.MaTaiKhoanNavigation)
            .Include(y => y.GhepNoiHocTaps)
                .ThenInclude(g => g.LichHocs)
                    .ThenInclude(l => l.BaoCaoBuoiHoc)
            .Include(y => y.GhepNoiHocTaps)
                .ThenInclude(g => g.DanhGiaHuongDans)
            .Where(y => y.MaSinhVien == sinhVien.MaSinhVien)
            .OrderByDescending(y => y.NgayTao)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var schedules = requests
            .SelectMany(y => y.GhepNoiHocTaps)
            .SelectMany(g => g.LichHocs)
            .OrderBy(l => l.NgayHoc)
            .ThenBy(l => l.GioBatDau)
            .ToList();

        ViewBag.UpcomingSchedules = schedules
            .Where(l => l.NgayHoc >= today && l.TrangThai != "Đã học" && l.TrangThai != "Đã hoàn thành")
            .Take(6)
            .ToList();
        ViewBag.CompletedScheduleCount = schedules.Count(l => l.TrangThai == "Đã học" || l.TrangThai == "Đã hoàn thành" || l.BaoCaoBuoiHoc != null);
        ViewBag.WaitingScheduleCount = requests
            .SelectMany(y => y.GhepNoiHocTaps)
            .Count(g => (g.TrangThai == "Mentor chấp nhận" || g.TrangThai == "Mentor đã chấp nhận" || g.TrangThai == "Đã lên lịch") && !g.LichHocs.Any());
        ViewBag.WaitingReportCount = schedules.Count(l => l.NgayHoc < today && l.BaoCaoBuoiHoc == null);
        ViewBag.WaitingReviewCount = requests
            .SelectMany(y => y.GhepNoiHocTaps)
            .Count(g => g.LichHocs.Any(l => l.BaoCaoBuoiHoc != null) && !g.DanhGiaHuongDans.Any());

        return View(requests);
    }

    public async Task<IActionResult> Create(int? mentorId = null)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var profileGuard = await RequireCompletedStudentProfileAsync();
        if (profileGuard != null) return profileGuard;

        var model = new YeuCauHoTroCreateViewModel
        {
            MentorId = mentorId,
            MucDoCanHoTro = "Trung bình"
        };

        await ApplySelectedMentorAsync(model);
        await PopulateCreateModelAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(YeuCauHoTroCreateViewModel model)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var profileGuard = await RequireCompletedStudentProfileAsync();
        if (profileGuard != null) return profileGuard;

        var sinhVien = await CurrentSinhVienAsync();
        if (sinhVien == null) return RedirectToAction("HoSo", "SinhVien");
        var sinhVienProfile = await CurrentSinhVienProfileAsync();

        var selectedMentor = await GetActiveMentorAsync(model.MentorId);
        if (model.MentorId.HasValue && selectedMentor == null)
        {
            ModelState.AddModelError(nameof(model.MentorId), "Mentor đã chọn không còn hoạt động.");
        }
        else if (selectedMentor != null && BuildSharedSlots(sinhVienProfile, selectedMentor).Count == 0)
        {
            ModelState.AddModelError(nameof(model.MentorId), "Mentor đã chọn chưa có lịch rảnh chung với bạn. Vui lòng chọn mentor khác hoặc cập nhật lịch rảnh.");
        }

        if (!await _context.LinhVucHocTaps.AnyAsync(l => l.MaLinhVuc == model.MaLinhVuc))
        {
            ModelState.AddModelError(nameof(model.MaLinhVuc), "Lĩnh vực học tập không hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            await ApplySelectedMentorAsync(model);
            await PopulateCreateModelAsync(model);
            return View(model);
        }

        var request = new YeuCauHoTroHocTap
        {
            MaSinhVien = sinhVien.MaSinhVien,
            MaLinhVuc = model.MaLinhVuc,
            MoTaVanDe = BuildProblemDescription(model),
            MucTieu = $"{model.TieuDe.Trim()} | Chủ đề: {model.ChuDeCongNghe.Trim()}",
            MucDoCanHoTro = model.MucDoCanHoTro,
            TrangThai = selectedMentor == null ? "Đang chờ" : "Đã gợi ý",
            NgayTao = DateTime.Now
        };

        _context.YeuCauHoTroHocTaps.Add(request);
        await _context.SaveChangesAsync();

        if (selectedMentor != null)
        {
            var score = AnalyzeMentor(selectedMentor, model.MaLinhVuc, BuildDraftRequestText(model), sinhVienProfile);
            _context.GhepNoiHocTaps.Add(new GhepNoiHocTap
            {
                MaYeuCau = request.MaYeuCau,
                MaHuongDan = selectedMentor.MaHuongDan,
                ChuyenMonScore = score.ChuyenMonScore,
                LichRanhScore = score.LichRanhScore,
                DiemUyTinScore = score.DiemUyTinScore,
                DanhGiaScore = score.DanhGiaScore,
                TuongDongScore = score.TuongDongScore,
                TrangThai = "Đã gửi yêu cầu",
                NgayGhep = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        TempData["SuccessMessage"] = selectedMentor == null
            ? "Đã tạo yêu cầu hỗ trợ 1-1. Hệ thống sẽ dựa trên hồ sơ, lịch rảnh và nội dung yêu cầu để gợi ý mentor phù hợp."
            : $"Đã tạo yêu cầu và gửi đề xuất ghép nối tới mentor {selectedMentor.MaTaiKhoanNavigation.HoTen}.";
        return RedirectToAction(nameof(Details), new { id = request.MaYeuCau });
    }

    public async Task<IActionResult> Details(int id)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var sinhVien = await CurrentSinhVienAsync();
        if (sinhVien == null) return RedirectToAction("HoSo", "SinhVien");

        var request = await _context.YeuCauHoTroHocTaps
            .Include(y => y.MaLinhVucNavigation)
            .Include(y => y.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
                    .ThenInclude(t => t.LichRanhs)
            .Include(y => y.MaSinhVienNavigation)
                .ThenInclude(s => s.ThanhVienClbs)
                    .ThenInclude(t => t.MaClbNavigation)
            .Include(y => y.GhepNoiHocTaps)
                .ThenInclude(g => g.MaHuongDanNavigation)
                    .ThenInclude(m => m.MaTaiKhoanNavigation)
            .Include(y => y.GhepNoiHocTaps)
                .ThenInclude(g => g.LichHocs)
                    .ThenInclude(l => l.BaoCaoBuoiHoc)
            .Include(y => y.GhepNoiHocTaps)
                .ThenInclude(g => g.DanhGiaHuongDans)
            .FirstOrDefaultAsync(y => y.MaYeuCau == id && y.MaSinhVien == sinhVien.MaSinhVien);

        if (request == null) return NotFound();

        var mentorSuggestions = await GetMentorSuggestionsAsync(request);
        ViewBag.MentorGoiY = mentorSuggestions;
        ViewBag.MentorGoiYReason = mentorSuggestions.Count == 0
            ? await BuildNoSuggestionReasonAsync(request)
            : null;
        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeXuatMentor(int yeuCauId, int mentorId)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var sinhVien = await CurrentSinhVienAsync();
        if (sinhVien == null) return RedirectToAction("HoSo", "SinhVien");

        var request = await _context.YeuCauHoTroHocTaps
            .Include(y => y.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
                    .ThenInclude(t => t.LichRanhs)
            .FirstOrDefaultAsync(y => y.MaYeuCau == yeuCauId && y.MaSinhVien == sinhVien.MaSinhVien);
        var mentor = await GetActiveMentorAsync(mentorId);

        if (request == null || mentor == null) return NotFound();

        if (BuildSharedSlots(request.MaSinhVienNavigation, mentor).Count == 0)
        {
            TempData["SuccessMessage"] = $"Mentor {mentor.MaTaiKhoanNavigation.HoTen} chưa có lịch rảnh chung với bạn. Hãy cập nhật lịch rảnh hoặc chọn mentor khác.";
            return RedirectToAction(nameof(Details), new { id = yeuCauId });
        }

        var exists = await _context.GhepNoiHocTaps
            .AnyAsync(g => g.MaYeuCau == yeuCauId && g.MaHuongDan == mentorId);
        if (!exists)
        {
            var score = AnalyzeMentor(mentor, request, request.MaSinhVienNavigation);
            _context.GhepNoiHocTaps.Add(new GhepNoiHocTap
            {
                MaYeuCau = yeuCauId,
                MaHuongDan = mentorId,
                ChuyenMonScore = score.ChuyenMonScore,
                LichRanhScore = score.LichRanhScore,
                DiemUyTinScore = score.DiemUyTinScore,
                DanhGiaScore = score.DanhGiaScore,
                TuongDongScore = score.TuongDongScore,
                TrangThai = "Đã gửi yêu cầu",
                NgayGhep = DateTime.Now
            });
            request.TrangThai = "Đã gợi ý";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã gửi đề xuất ghép nối tới mentor {mentor.MaTaiKhoanNavigation.HoTen}.";
        }
        else
        {
            TempData["SuccessMessage"] = "Mentor này đã nằm trong danh sách ghép nối của yêu cầu.";
        }

        return RedirectToAction(nameof(Details), new { id = yeuCauId });
    }

    public async Task<IActionResult> Schedule(int id)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var match = await StudentMatchAsync(id);
        if (match == null) return NotFound();
        if (!CanStudentSchedule(match))
        {
            TempData["SuccessMessage"] = "Bạn chỉ có thể lên lịch sau khi mentor chấp nhận ghép nối.";
            return RedirectToAction(nameof(Details), new { id = match.MaYeuCau });
        }

        var suggestedSlots = BuildSuggestedSlots(match);
        if (suggestedSlots.Count == 0)
        {
            TempData["SuccessMessage"] = "Chưa có lịch rảnh chung giữa bạn và mentor. Vui lòng cập nhật lịch rảnh hoặc trao đổi để mentor cập nhật lịch trước khi lên lịch học.";
            return RedirectToAction(nameof(Details), new { id = match.MaYeuCau });
        }

        ViewBag.Match = match;
        ViewBag.SharedSlots = BuildSharedSlots(match);
        ViewBag.SuggestedSlots = suggestedSlots;

        var model = new ScheduleSessionViewModel { MaGhepNoi = id };
        var firstSlot = suggestedSlots.FirstOrDefault();
        if (firstSlot != null)
        {
            model.NgayHoc = firstSlot.NgayHoc;
            model.GioBatDau = firstSlot.GioBatDau;
            model.GioKetThuc = firstSlot.GioKetThuc;
            model.HinhThuc = "Online";
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(ScheduleSessionViewModel model)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var match = await StudentMatchAsync(model.MaGhepNoi);
        if (match == null) return NotFound();
        if (!CanStudentSchedule(match))
        {
            TempData["SuccessMessage"] = "Bạn chỉ có thể lên lịch sau khi mentor chấp nhận ghép nối.";
            return RedirectToAction(nameof(Details), new { id = match.MaYeuCau });
        }

        var sharedSlots = BuildSharedSlots(match);
        var selectedSlots = ParseSelectedSlots(model.SelectedSlots);
        if (sharedSlots.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Chưa có lịch rảnh chung giữa sinh viên và mentor. Không thể lưu lịch học.");
        }

        if (selectedSlots.Count == 0)
        {
            if (model.GioKetThuc <= model.GioBatDau)
            {
                ModelState.AddModelError(nameof(model.GioKetThuc), "Giờ kết thúc phải sau giờ bắt đầu.");
            }

            if (!IsInsideSharedAvailability(match, model.NgayHoc, model.GioBatDau, model.GioKetThuc))
            {
                ModelState.AddModelError(string.Empty, "Thời gian này chưa nằm trong lịch rảnh chung của sinh viên và mentor.");
            }

            if (ModelState.IsValid)
            {
                selectedSlots.Add((model.NgayHoc, model.GioBatDau, model.GioKetThuc));
            }
        }

        foreach (var slot in selectedSlots)
        {
            if (slot.GioKetThuc <= slot.GioBatDau)
            {
                ModelState.AddModelError(nameof(model.SelectedSlots), $"Khung {slot.NgayHoc:dd/MM/yyyy} {slot.GioBatDau:hh\\:mm}-{slot.GioKetThuc:hh\\:mm} không hợp lệ.");
                continue;
            }

            if (!IsInsideSharedAvailability(match, slot.NgayHoc, slot.GioBatDau, slot.GioKetThuc))
            {
                ModelState.AddModelError(nameof(model.SelectedSlots), $"Khung {slot.NgayHoc:dd/MM/yyyy} {slot.GioBatDau:hh\\:mm}-{slot.GioKetThuc:hh\\:mm} chưa nằm trong lịch rảnh chung.");
            }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Match = match;
            ViewBag.SharedSlots = sharedSlots;
            ViewBag.SuggestedSlots = BuildSuggestedSlots(match);
            return View(model);
        }

        var existingKeys = match.LichHocs
            .Select(l => $"{l.NgayHoc:yyyy-MM-dd}|{l.GioBatDau:HH\\:mm}|{l.GioKetThuc:HH\\:mm}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newSlots = selectedSlots
            .DistinctBy(s => $"{s.NgayHoc:yyyy-MM-dd}|{s.GioBatDau:hh\\:mm}|{s.GioKetThuc:hh\\:mm}")
            .Where(s => !existingKeys.Contains($"{DateOnly.FromDateTime(s.NgayHoc):yyyy-MM-dd}|{TimeOnly.FromTimeSpan(s.GioBatDau):HH\\:mm}|{TimeOnly.FromTimeSpan(s.GioKetThuc):HH\\:mm}"))
            .ToList();

        if (newSlots.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedSlots), "Các khung đã chọn đã tồn tại trong lịch học.");
            ViewBag.Match = match;
            ViewBag.SharedSlots = sharedSlots;
            ViewBag.SuggestedSlots = BuildSuggestedSlots(match);
            return View(model);
        }

        foreach (var slot in newSlots)
        {
            _context.LichHocs.Add(new LichHoc
            {
                MaGhepNoi = match.MaGhepNoi,
                NgayHoc = DateOnly.FromDateTime(slot.NgayHoc),
                GioBatDau = TimeOnly.FromTimeSpan(slot.GioBatDau),
                GioKetThuc = TimeOnly.FromTimeSpan(slot.GioKetThuc),
                HinhThuc = model.HinhThuc,
                DiaDiem = string.IsNullOrWhiteSpace(model.DiaDiem) ? null : model.DiaDiem.Trim(),
                LinkOnline = string.IsNullOrWhiteSpace(model.LinkOnline) ? null : model.LinkOnline.Trim(),
                TrangThai = "Sắp diễn ra"
            });
        }

        match.TrangThai = "Đã lên lịch";
        match.MaYeuCauNavigation.TrangThai = "Đang học";
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = newSlots.Count == 1
            ? "Đã lên lịch học cho ghép nối. Sau buổi học, mentor sẽ lập báo cáo để bạn đánh giá."
            : $"Đã lên {newSlots.Count} buổi học cho ghép nối. Mentor sẽ thấy các lịch này trong dashboard.";
        return RedirectToAction(nameof(Details), new { id = match.MaYeuCau });
    }

    public async Task<IActionResult> Evaluate(int id)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var match = await StudentMatchAsync(id);
        if (match == null) return NotFound();
        if (!CanStudentEvaluate(match))
        {
            TempData["SuccessMessage"] = "Bạn chỉ có thể đánh giá sau khi mentor lập báo cáo buổi học.";
            return RedirectToAction(nameof(Details), new { id = match.MaYeuCau });
        }

        ViewBag.Match = match;
        return View(new DanhGiaMentorViewModel { MaGhepNoi = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Evaluate(DanhGiaMentorViewModel model)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var match = await StudentMatchAsync(model.MaGhepNoi);
        if (match == null) return NotFound();
        if (!CanStudentEvaluate(match))
        {
            TempData["SuccessMessage"] = "Bạn chỉ có thể đánh giá sau khi mentor lập báo cáo buổi học.";
            return RedirectToAction(nameof(Details), new { id = match.MaYeuCau });
        }

        var sinhVien = await CurrentSinhVienAsync();
        if (sinhVien == null) return RedirectToAction("HoSo", "SinhVien");

        var reportedSession = match.LichHocs
            .Where(l => l.BaoCaoBuoiHoc != null)
            .OrderByDescending(l => l.NgayHoc)
            .ThenByDescending(l => l.GioBatDau)
            .FirstOrDefault();

        if (reportedSession == null)
        {
            ModelState.AddModelError(string.Empty, "Chưa có buổi học nào có báo cáo để đánh giá.");
        }

        if (await _context.DanhGiaHuongDans.AnyAsync(d => d.MaGhepNoi == model.MaGhepNoi && d.MaSinhVien == sinhVien.MaSinhVien))
        {
            ModelState.AddModelError(string.Empty, "Bạn đã đánh giá ghép nối này.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Match = match;
            return View(model);
        }

        _context.DanhGiaHuongDans.Add(new DanhGiaHuongDan
        {
            MaGhepNoi = match.MaGhepNoi,
            MaLichHoc = reportedSession!.MaLichHoc,
            MaHuongDan = match.MaHuongDan,
            MaSinhVien = sinhVien.MaSinhVien,
            SoSao = model.SoSao,
            NhanXet = string.IsNullOrWhiteSpace(model.NhanXet) ? null : model.NhanXet.Trim(),
            NgayDanhGia = DateTime.Now
        });

        match.TrangThai = "Hoàn thành";
        match.MaYeuCauNavigation.TrangThai = "Hoàn thành";
        await _context.SaveChangesAsync();
        await RecalculateMentorRatingAsync(match.MaHuongDan);

        TempData["SuccessMessage"] = "Đã gửi đánh giá mentor và cập nhật điểm uy tín.";
        return RedirectToAction(nameof(Details), new { id = match.MaYeuCau });
    }

    private async Task<SinhVien?> CurrentSinhVienAsync()
    {
        var userId = CurrentUserId;
        if (!userId.HasValue) return null;
        return await _context.SinhViens.FirstOrDefaultAsync(s => s.MaTaiKhoan == userId.Value);
    }

    private async Task<SinhVien?> CurrentSinhVienProfileAsync()
    {
        var userId = CurrentUserId;
        if (!userId.HasValue) return null;

        return await _context.SinhViens
            .Include(s => s.MaTaiKhoanNavigation)
                .ThenInclude(t => t.LichRanhs)
            .Include(s => s.ThanhVienClbs)
                .ThenInclude(t => t.MaClbNavigation)
            .FirstOrDefaultAsync(s => s.MaTaiKhoan == userId.Value);
    }

    private async Task<GhepNoiHocTap?> StudentMatchAsync(int matchId)
    {
        var sinhVien = await CurrentSinhVienAsync();
        if (sinhVien == null) return null;

        return await _context.GhepNoiHocTaps
            .Include(g => g.MaYeuCauNavigation)
                .ThenInclude(y => y.MaLinhVucNavigation)
            .Include(g => g.MaYeuCauNavigation)
                .ThenInclude(y => y.MaSinhVienNavigation)
                    .ThenInclude(s => s.MaTaiKhoanNavigation)
                        .ThenInclude(t => t.LichRanhs)
            .Include(g => g.MaHuongDanNavigation)
                .ThenInclude(m => m.MaTaiKhoanNavigation)
                    .ThenInclude(t => t.LichRanhs)
            .Include(g => g.LichHocs)
                .ThenInclude(l => l.BaoCaoBuoiHoc)
            .Include(g => g.DanhGiaHuongDans)
            .FirstOrDefaultAsync(g => g.MaGhepNoi == matchId && g.MaYeuCauNavigation.MaSinhVien == sinhVien.MaSinhVien);
    }

    private async Task RecalculateMentorRatingAsync(int mentorId)
    {
        var mentor = await _context.NguoiHuongDans
            .Include(m => m.GhepNoiHocTaps)
                .ThenInclude(g => g.LichHocs)
                    .ThenInclude(l => l.BaoCaoBuoiHoc)
            .Include(m => m.GhepNoiHocTaps)
                .ThenInclude(g => g.DanhGiaHuongDans)
            .FirstOrDefaultAsync(m => m.MaHuongDan == mentorId);
        if (mentor == null) return;

        var metrics = CalculateMentorQuality(mentor);

        mentor.SoLuotDanhGia = metrics.ReviewCount;
        mentor.DiemDanhGia = metrics.AverageRating;
        mentor.DiemUyTin = metrics.Reputation;
        await _context.SaveChangesAsync();
    }

    private static bool CanStudentSchedule(GhepNoiHocTap match)
    {
        return match.TrangThai == "Mentor chấp nhận" || match.TrangThai == "Mentor đã chấp nhận" || match.TrangThai == "Đã lên lịch";
    }

    private static bool CanStudentEvaluate(GhepNoiHocTap match)
    {
        return match.LichHocs.Any(l => l.BaoCaoBuoiHoc != null) && !match.DanhGiaHuongDans.Any();
    }

    private async Task<IActionResult?> RequireCompletedStudentProfileAsync()
    {
        var sinhVien = await CurrentSinhVienAsync();
        var hasProfile = sinhVien != null
            && !string.IsNullOrWhiteSpace(sinhVien.ChuyenNganh)
            && !string.IsNullOrWhiteSpace(sinhVien.KyNang)
            && !string.IsNullOrWhiteSpace(sinhVien.GioiThieu);
        var hasAvailability = CurrentUserId.HasValue && await _context.LichRanhs.AnyAsync(l => l.MaTaiKhoan == CurrentUserId.Value);

        if (hasProfile && hasAvailability) return null;

        TempData["InfoMessage"] = "Bạn cần cập nhật hồ sơ sinh viên, kỹ năng, mục tiêu học và lịch rảnh trước khi tạo yêu cầu hỗ trợ 1-1.";
        return RedirectToAction("HoSo", "SinhVien", new
        {
            returnUrl = HttpContext.Request.Path + HttpContext.Request.QueryString
        });
    }

    private async Task ApplySelectedMentorAsync(YeuCauHoTroCreateViewModel model)
    {
        var mentor = await GetActiveMentorAsync(model.MentorId);
        if (mentor == null) return;

        model.MentorDaChon = mentor.MaTaiKhoanNavigation.HoTen;

        if (model.MaLinhVuc == 0)
        {
            model.MaLinhVuc = mentor.ChuyenMonNguoiHuongDans
                .OrderByDescending(c => c.MucDoThanhThao ?? 0)
                .Select(c => c.MaLinhVuc)
                .FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(model.ChuDeCongNghe))
        {
            model.ChuDeCongNghe = string.Join(", ", mentor.ChuyenMonNguoiHuongDans
                .OrderByDescending(c => c.MucDoThanhThao ?? 0)
                .Select(c => c.MaLinhVucNavigation.TenLinhVuc)
                .Take(3));
        }
    }

    private async Task<NguoiHuongDan?> GetActiveMentorAsync(int? mentorId)
    {
        if (!mentorId.HasValue) return null;

        return await _context.NguoiHuongDans
            .Include(m => m.MaTaiKhoanNavigation)
                .ThenInclude(t => t.LichRanhs)
            .Include(m => m.MaTaiKhoanNavigation)
                .ThenInclude(t => t.SinhVien)
                    .ThenInclude(s => s!.ThanhVienClbs)
                        .ThenInclude(t => t.MaClbNavigation)
            .Include(m => m.ChuyenMonNguoiHuongDans)
                .ThenInclude(c => c.MaLinhVucNavigation)
            .Include(m => m.GhepNoiHocTaps)
                .ThenInclude(g => g.LichHocs)
                    .ThenInclude(l => l.BaoCaoBuoiHoc)
            .Include(m => m.DanhGiaHuongDans)
            .FirstOrDefaultAsync(m => m.MaHuongDan == mentorId.Value && (m.TrangThai == null || m.TrangThai == "Hoạt động"));
    }

    private async Task PopulateCreateModelAsync(YeuCauHoTroCreateViewModel model)
    {
        var linhVuc = await _context.LinhVucHocTaps
            .Where(l => l.TrangThai == null || l.TrangThai == "Hoạt động")
            .OrderBy(l => l.TenLinhVuc)
            .ToListAsync();

        model.LinhVucOptions = linhVuc.Select(l => new SelectListItem(l.TenLinhVuc, l.MaLinhVuc.ToString()));
        model.MucDoOptions = new[]
        {
            "Cơ bản",
            "Trung bình",
            "Nâng cao"
        }.Select(level => new SelectListItem(level, level));

        var selectedFieldId = model.MaLinhVuc != 0 ? model.MaLinhVuc : linhVuc.FirstOrDefault()?.MaLinhVuc ?? 0;
        var sinhVien = await CurrentSinhVienProfileAsync();
        model.MentorGoiY = selectedFieldId == 0
            ? []
            : await GetMentorSuggestionsAsync(selectedFieldId, BuildDraftRequestText(model), sinhVien);
    }

    private Task<List<MentorSuggestionViewModel>> GetMentorSuggestionsAsync(YeuCauHoTroHocTap request)
    {
        var requestText = $"{request.MucTieu} {request.MoTaVanDe} {request.MucDoCanHoTro}";
        return GetMentorSuggestionsAsync(request.MaLinhVuc, requestText, request.MaSinhVienNavigation);
    }

    private async Task<string> BuildNoSuggestionReasonAsync(YeuCauHoTroHocTap request)
    {
        var mentors = await _context.NguoiHuongDans
            .Include(m => m.MaTaiKhoanNavigation)
                .ThenInclude(t => t.LichRanhs)
            .Include(m => m.ChuyenMonNguoiHuongDans)
                .ThenInclude(c => c.MaLinhVucNavigation)
            .Include(m => m.GhepNoiHocTaps)
                .ThenInclude(g => g.LichHocs)
                    .ThenInclude(l => l.BaoCaoBuoiHoc)
            .Include(m => m.DanhGiaHuongDans)
            .Where(m => m.TrangThai == null || m.TrangThai == "Hoạt động")
            .Where(m => m.ChuyenMonNguoiHuongDans.Any(c => c.MaLinhVuc == request.MaLinhVuc))
            .ToListAsync();

        if (mentors.Count == 0)
        {
            return "Chưa có mentor đang hoạt động cho lĩnh vực này.";
        }

        var mentorsWithSharedSchedule = mentors
            .Count(m => BuildSharedSlots(request.MaSinhVienNavigation, m).Count > 0);
        if (mentorsWithSharedSchedule == 0)
        {
            return $"Có {mentors.Count} mentor đúng lĩnh vực, nhưng chưa có lịch rảnh chung với bạn. Hãy cập nhật thêm lịch rảnh hoặc chọn khung giờ trùng với mentor.";
        }

        return $"Có {mentorsWithSharedSchedule} mentor có lịch rảnh chung, nhưng điểm phù hợp chưa đạt ngưỡng gợi ý. Hãy mô tả rõ hơn công nghệ/vấn đề cần hỗ trợ hoặc cập nhật hồ sơ kỹ năng.";
    }

    private async Task<List<MentorSuggestionViewModel>> GetMentorSuggestionsAsync(int maLinhVuc, string requestText, SinhVien? sinhVien)
    {
        var mentors = await _context.NguoiHuongDans
            .Include(m => m.MaTaiKhoanNavigation)
                .ThenInclude(t => t.LichRanhs)
            .Include(m => m.MaTaiKhoanNavigation)
                .ThenInclude(t => t.SinhVien)
                    .ThenInclude(s => s!.ThanhVienClbs)
                        .ThenInclude(t => t.MaClbNavigation)
            .Include(m => m.ChuyenMonNguoiHuongDans)
                .ThenInclude(c => c.MaLinhVucNavigation)
            .Include(m => m.GhepNoiHocTaps)
                .ThenInclude(g => g.LichHocs)
                    .ThenInclude(l => l.BaoCaoBuoiHoc)
            .Include(m => m.DanhGiaHuongDans)
            .Where(m => m.TrangThai == null || m.TrangThai == "Hoạt động")
            .Where(m => m.ChuyenMonNguoiHuongDans.Any(c => c.MaLinhVuc == maLinhVuc))
            .ToListAsync();

        return mentors
            .Select(m => new { Mentor = m, Score = AnalyzeMentor(m, maLinhVuc, requestText, sinhVien) })
            .Where(x => x.Score.Total >= 45 && x.Score.LichRanhScore > 0)
            .OrderByDescending(x => x.Score.Total)
            .ThenByDescending(x => CalculateMentorQuality(x.Mentor).Reputation)
            .ThenByDescending(x => CalculateMentorQuality(x.Mentor).AverageRating)
            .Take(8)
            .Select(x => new MentorSuggestionViewModel
            {
                MaHuongDan = x.Mentor.MaHuongDan,
                HoTen = x.Mentor.MaTaiKhoanNavigation.HoTen,
                ChuyenMon = string.Join(", ", x.Mentor.ChuyenMonNguoiHuongDans
                    .OrderByDescending(c => c.MaLinhVuc == maLinhVuc)
                    .ThenByDescending(c => c.MucDoThanhThao ?? 0)
                    .Select(c => c.MaLinhVucNavigation.TenLinhVuc)
                    .Distinct()),
                DiemUyTin = CalculateMentorQuality(x.Mentor).Reputation,
                DiemDanhGia = CalculateMentorQuality(x.Mentor).AverageRating,
                SoLuotDanhGia = CalculateMentorQuality(x.Mentor).ReviewCount,
                DiemPhuHop = x.Score.Total,
                ChuyenMonScore = x.Score.ChuyenMonScore,
                LichRanhScore = x.Score.LichRanhScore,
                DiemUyTinScore = x.Score.DiemUyTinScore,
                DanhGiaScore = x.Score.DanhGiaScore,
                TuongDongScore = x.Score.TuongDongScore,
                TaiHienTai = x.Score.ActiveLoad,
                LyDoGoiY = x.Score.Reasons,
                LichRanhChung = BuildSharedSlots(sinhVien, x.Mentor)
            })
            .ToList();
    }

    private static MentorFitScore AnalyzeMentor(NguoiHuongDan mentor, YeuCauHoTroHocTap request, SinhVien? sinhVien)
    {
        var requestText = $"{request.MucTieu} {request.MoTaVanDe} {request.MucDoCanHoTro}";
        return AnalyzeMentor(mentor, request.MaLinhVuc, requestText, sinhVien);
    }

    private static MentorFitScore AnalyzeMentor(NguoiHuongDan mentor, int maLinhVuc, string requestText, SinhVien? sinhVien)
    {
        var reasons = new List<string>();
        var specialty = mentor.ChuyenMonNguoiHuongDans.FirstOrDefault(c => c.MaLinhVuc == maLinhVuc);
        var chuyenMonScore = specialty == null ? 0m : NormalizeScore100(specialty.MucDoThanhThao ?? 0);
        if (chuyenMonScore >= 80m) reasons.Add("Đúng lĩnh vực và chuyên môn mạnh");
        else if (specialty != null) reasons.Add("Có chuyên môn cùng lĩnh vực");

        var keywords = BuildKeywords(
            requestText,
            sinhVien?.KyNang,
            sinhVien?.ChuyenNganh,
            sinhVien?.GioiThieu);
        var mentorText = string.Join(" ", mentor.ChuyenMonNguoiHuongDans.Select(c =>
            $"{c.MaLinhVucNavigation.TenLinhVuc} {c.MoTaKinhNghiem}")) + $" {mentor.MaTaiKhoanNavigation.HoTen}";
        var matchedKeywords = keywords.Count(k => ContainsKeyword(mentorText, k));
        if (matchedKeywords > 0) reasons.Add($"Trùng {matchedKeywords} từ khóa nhu cầu học");

        var sharedSlots = BuildSharedSlots(sinhVien, mentor);
        var lichRanhScore = sharedSlots.Count > 0 ? 100m : 0m;
        if (sharedSlots.Count > 0) reasons.Add($"Có {sharedSlots.Count} khung lịch rảnh chung");

        var quality = CalculateMentorQuality(mentor);
        var mentorRating = quality.AverageRating > 0 ? quality.AverageRating : mentor.DiemDanhGia ?? 0m;
        var danhGiaScore = mentorRating <= 5m ? mentorRating * 20m : mentorRating;
        danhGiaScore = Math.Clamp(Math.Round(danhGiaScore, 2), 0m, 100m);
        if (danhGiaScore >= 85m) reasons.Add("Đánh giá tốt từ sinh viên");

        var diemUyTinScore = NormalizeScore100(mentor.DiemUyTin ?? quality.Reputation);
        if (diemUyTinScore >= 80m) reasons.Add("Điểm uy tín cao");

        var completedSessions = mentor.GhepNoiHocTaps
            .SelectMany(g => g.LichHocs)
            .Count(l => l.TrangThai == "Đã học" || l.TrangThai == "Đã hoàn thành" || l.BaoCaoBuoiHoc != null);
        if (completedSessions > 0) reasons.Add("Có lịch sử hỗ trợ học tập");

        var keywordDenominator = Math.Min(Math.Max(keywords.Count, 1), 10);
        var keywordScore = keywords.Count == 0 ? 0m : Math.Min(70m, matchedKeywords / (decimal)keywordDenominator * 70m);
        var historyScore = Math.Min(30m, completedSessions * 5m + quality.ReviewCount * 0.5m);
        var tuongDongScore = Math.Clamp(Math.Round(keywordScore + historyScore, 2), 0m, 100m);

        var hasClubData = mentor.MaTaiKhoanNavigation.SinhVien?.ThanhVienClbs.Any(t => t.TrangThai == null || t.TrangThai == "Hoạt động") == true;
        if (hasClubData) reasons.Add("Có dữ liệu hoạt động CLB");

        var activeLoad = mentor.GhepNoiHocTaps.Count(g =>
            g.TrangThai != "Hoàn thành"
            && g.TrangThai != "Mentor từ chối"
            && g.TrangThai != "Đã hủy");
        if (activeLoad <= 2) reasons.Add("Tải ghép nối hiện tại phù hợp");

        var total = 0.35m * chuyenMonScore
            + 0.20m * lichRanhScore
            + 0.20m * diemUyTinScore
            + 0.15m * danhGiaScore
            + 0.10m * tuongDongScore;
        total = Math.Clamp(Math.Round(total, 0), 0, 100);

        if (reasons.Count == 0) reasons.Add("Mentor đang hoạt động và có hồ sơ chuyên môn");
        return new MentorFitScore(
            total,
            activeLoad,
            reasons.Take(4).ToList(),
            chuyenMonScore,
            lichRanhScore,
            diemUyTinScore,
            danhGiaScore,
            tuongDongScore);
    }

    private static List<string> BuildSharedSlots(GhepNoiHocTap match)
    {
        return BuildSharedSlots(match.MaYeuCauNavigation.MaSinhVienNavigation, match.MaHuongDanNavigation);
    }

    private static List<string> BuildSharedSlots(SinhVien? sinhVien, NguoiHuongDan mentor)
    {
        if (sinhVien == null) return [];

        var studentSlots = sinhVien.MaTaiKhoanNavigation.LichRanhs;
        var mentorSlots = mentor.MaTaiKhoanNavigation.LichRanhs;
        var result = new List<string>();

        foreach (var student in studentSlots)
        {
            foreach (var mentorSlot in mentorSlots.Where(m => m.Thu == student.Thu))
            {
                var start = student.GioBatDau > mentorSlot.GioBatDau ? student.GioBatDau : mentorSlot.GioBatDau;
                var end = student.GioKetThuc < mentorSlot.GioKetThuc ? student.GioKetThuc : mentorSlot.GioKetThuc;
                if (end > start)
                {
                    result.Add($"{FormatDay(student.Thu)} {start:HH\\:mm}-{end:HH\\:mm}");
                }
            }
        }

        return result.Distinct().Take(8).ToList();
    }

    private static List<SharedScheduleSlotViewModel> BuildSuggestedSlots(GhepNoiHocTap match)
    {
        var studentSlots = match.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.LichRanhs;
        var mentorSlots = match.MaHuongDanNavigation.MaTaiKhoanNavigation.LichRanhs;
        var result = new List<SharedScheduleSlotViewModel>();
        var today = DateTime.Today;

        foreach (var student in studentSlots)
        {
            foreach (var mentorSlot in mentorSlots.Where(m => m.Thu == student.Thu))
            {
                var start = student.GioBatDau > mentorSlot.GioBatDau ? student.GioBatDau : mentorSlot.GioBatDau;
                var end = student.GioKetThuc < mentorSlot.GioKetThuc ? student.GioKetThuc : mentorSlot.GioKetThuc;
                if (end <= start) continue;

                var firstDate = NextDateForStudyDay(student.Thu, today.AddDays(1));
                for (var week = 0; week < 3; week++)
                {
                    var date = firstDate.AddDays(week * 7);
                    result.Add(new SharedScheduleSlotViewModel
                    {
                        NgayHoc = date,
                        GioBatDau = start.ToTimeSpan(),
                        GioKetThuc = end.ToTimeSpan(),
                        Label = $"{FormatDay(student.Thu)}, {date:dd/MM/yyyy} {start:HH\\:mm}-{end:HH\\:mm}"
                    });
                }
            }
        }

        return result
            .GroupBy(s => $"{s.NgayHoc:yyyyMMdd}-{s.GioBatDau}-{s.GioKetThuc}")
            .Select(g => g.First())
            .OrderBy(s => s.NgayHoc)
            .ThenBy(s => s.GioBatDau)
            .Take(10)
            .ToList();
    }

    private static bool IsInsideSharedAvailability(GhepNoiHocTap match, DateTime ngayHoc, TimeSpan gioBatDau, TimeSpan gioKetThuc)
    {
        var thu = ToStudyDay(ngayHoc);
        var start = TimeOnly.FromTimeSpan(gioBatDau);
        var end = TimeOnly.FromTimeSpan(gioKetThuc);
        var studentSlots = match.MaYeuCauNavigation.MaSinhVienNavigation.MaTaiKhoanNavigation.LichRanhs.Where(l => l.Thu == thu);
        var mentorSlots = match.MaHuongDanNavigation.MaTaiKhoanNavigation.LichRanhs.Where(l => l.Thu == thu);

        return studentSlots.Any(student => mentorSlots.Any(mentor =>
        {
            var sharedStart = student.GioBatDau > mentor.GioBatDau ? student.GioBatDau : mentor.GioBatDau;
            var sharedEnd = student.GioKetThuc < mentor.GioKetThuc ? student.GioKetThuc : mentor.GioKetThuc;
            return start >= sharedStart && end <= sharedEnd;
        }));
    }

    private static List<(DateTime NgayHoc, TimeSpan GioBatDau, TimeSpan GioKetThuc)> ParseSelectedSlots(IEnumerable<string>? values)
    {
        var result = new List<(DateTime NgayHoc, TimeSpan GioBatDau, TimeSpan GioKetThuc)>();
        if (values == null) return result;

        foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct())
        {
            var parts = value.Split('|');
            if (parts.Length != 3) continue;
            if (!DateTime.TryParse(parts[0], out var ngayHoc)) continue;
            if (!TimeSpan.TryParse(parts[1], out var gioBatDau)) continue;
            if (!TimeSpan.TryParse(parts[2], out var gioKetThuc)) continue;

            result.Add((ngayHoc.Date, gioBatDau, gioKetThuc));
        }

        return result;
    }

    private static int ToStudyDay(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Sunday ? 8 : (int)date.DayOfWeek + 1;
    }

    private static DateTime NextDateForStudyDay(int studyDay, DateTime fromDate)
    {
        var current = ToStudyDay(fromDate);
        var offset = (studyDay - current + 7) % 7;
        return fromDate.Date.AddDays(offset);
    }

    private static string BuildDraftRequestText(YeuCauHoTroCreateViewModel model)
    {
        return $"{model.TieuDe} {model.ChuDeCongNghe} {model.MoTaChiTiet} {model.MucDoCanHoTro} {string.Join(" ", model.PhanCanHoTro)} {model.DaThuNhungGi}";
    }

    private static List<string> BuildKeywords(params string?[] values)
    {
        var separators = new[] { ' ', ',', '.', ';', ':', '/', '\\', '-', '_', '|', '\r', '\n', '\t', '(', ')', '[', ']' };
        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .SelectMany(v => v!.ToLowerInvariant().Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(word => word.Length >= 2)
            .Where(word => !StopWords.Contains(word))
            .Distinct()
            .Take(32)
            .ToList();
    }

    private static bool ContainsKeyword(string text, string keyword)
    {
        return text.ToLowerInvariant().Contains(keyword);
    }

    private static decimal NormalizeScore100(decimal value)
    {
        if (value <= 0m) return 0m;
        var normalized = value <= 5m ? value * 20m : value <= 10m ? value * 10m : value;
        return Math.Clamp(Math.Round(normalized, 2), 0m, 100m);
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "can", "cần", "hoc", "học", "ho", "hỗ", "tro", "trợ", "mentor", "minh", "mình",
        "ban", "bạn", "voi", "với", "ve", "về", "cho", "cac", "các", "va", "và", "de", "để",
        "dang", "đang", "phan", "phần", "noi", "nội", "dung", "muc", "mức", "do", "độ"
    };

    private sealed record MentorFitScore(
        decimal Total,
        int ActiveLoad,
        List<string> Reasons,
        decimal ChuyenMonScore,
        decimal LichRanhScore,
        decimal DiemUyTinScore,
        decimal DanhGiaScore,
        decimal TuongDongScore);

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
            .Count(l => l.TrangThai == "Đã học" || l.TrangThai == "Đã hoàn thành" || l.BaoCaoBuoiHoc != null);
        var reportCount = mentor.GhepNoiHocTaps
            .SelectMany(g => g.LichHocs)
            .Count(l => l.BaoCaoBuoiHoc != null);

        var ratingScore = reviewCount == 0 ? 0m : averageRating / 5m * 6m;
        var sessionScore = Math.Min(completedSessions, 30) / 30m * 2m;
        var reportScore = completedSessions == 0 ? 0m : Math.Min(reportCount, completedSessions) / (decimal)completedSessions;
        var reviewVolumeScore = Math.Min(reviewCount, 20) / 20m;
        var reputation = Math.Round(Math.Clamp(ratingScore + sessionScore + reportScore + reviewVolumeScore, 0m, 10m), 2);

        return new MentorQualityMetrics(reputation, averageRating, reviewCount);
    }

    private sealed record MentorQualityMetrics(decimal Reputation, decimal AverageRating, int ReviewCount);

    private static string FormatDay(int day)
    {
        return day switch
        {
            2 => "Thứ 2",
            3 => "Thứ 3",
            4 => "Thứ 4",
            5 => "Thứ 5",
            6 => "Thứ 6",
            7 => "Thứ 7",
            8 => "Chủ nhật",
            _ => $"Thứ {day}"
        };
    }

    private static string BuildProblemDescription(YeuCauHoTroCreateViewModel model)
    {
        var parts = new List<string>
        {
            $"Tiêu đề: {model.TieuDe.Trim()}",
            $"Chủ đề/Công nghệ: {model.ChuDeCongNghe.Trim()}",
            $"Mô tả chi tiết: {model.MoTaChiTiet.Trim()}"
        };

        if (model.PhanCanHoTro.Count > 0)
        {
            parts.Add($"Phần cần hỗ trợ: {string.Join(", ", model.PhanCanHoTro)}");
        }

        if (!string.IsNullOrWhiteSpace(model.DaThuNhungGi))
        {
            parts.Add($"Đã thử: {model.DaThuNhungGi.Trim()}");
        }

        return string.Join(Environment.NewLine, parts);
    }
}

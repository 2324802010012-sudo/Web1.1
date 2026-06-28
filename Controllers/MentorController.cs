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
        ViewBag.LichHoc = sessions.Count(l => l.NgayHoc >= today && l.TrangThai != "Đã học" && l.TrangThai != "Đã hoàn thành");
        ViewBag.BaoCao = mentorId == 0 ? 0 : await _context.BaoCaoBuoiHocs.CountAsync(b => b.MaLichHocNavigation.MaGhepNoiNavigation.MaHuongDan == mentorId);
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

        session.TrangThai = "Đã học";
        session.MaGhepNoiNavigation.TrangThai = "Đã lên lịch";
        session.MaGhepNoiNavigation.MaYeuCauNavigation.TrangThai = "Đang học";

        await _context.SaveChangesAsync();
        await UpdateStudyHistoryAsync(session.MaGhepNoiNavigation);
        await UpdateMentorRatingAndRankingAsync(session.MaGhepNoiNavigation.MaHuongDan);
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

    private async Task UpdateStudyHistoryAsync(GhepNoiHocTap match)
    {
        var completedSessions = match.LichHocs
            .Where(l => l.TrangThai == "Đã học" || l.TrangThai == "Đã hoàn thành" || l.BaoCaoBuoiHoc != null)
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
            .Where(l => l.NgayHoc >= today && l.TrangThai != "Đã học" && l.TrangThai != "Đã hoàn thành")
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

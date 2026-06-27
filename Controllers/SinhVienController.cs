using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class SinhVienController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public SinhVienController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var sinhVien = await _context.SinhViens
            .Include(s => s.MaTaiKhoanNavigation)
                .ThenInclude(t => t.LichRanhs)
            .Include(s => s.ThanhVienClbs)
                .ThenInclude(t => t.MaClbNavigation)
            .FirstOrDefaultAsync(s => s.MaTaiKhoan == CurrentUserId);
        var sinhVienId = sinhVien?.MaSinhVien ?? 0;

        ViewBag.UserName = CurrentUserName;
        ViewBag.YeuCau = sinhVienId == 0 ? 0 : await _context.YeuCauHoTroHocTaps.CountAsync(y => y.MaSinhVien == sinhVienId);
        ViewBag.GhepNoi = sinhVienId == 0 ? 0 : await _context.GhepNoiHocTaps.CountAsync(g => g.MaYeuCauNavigation.MaSinhVien == sinhVienId);
        ViewBag.LichHoc = sinhVienId == 0 ? 0 : await _context.LichHocs.CountAsync(l => l.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVien == sinhVienId && l.NgayHoc >= DateOnly.FromDateTime(DateTime.Today));
        ViewBag.BuoiHoanThanh = sinhVienId == 0 ? 0 : await _context.LichHocs.CountAsync(l => l.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVien == sinhVienId && (l.TrangThai == "Đã hoàn thành" || l.BaoCaoBuoiHoc != null));
        ViewBag.DanhGia = sinhVienId == 0 ? 0 : await _context.DanhGiaHuongDans.CountAsync(d => d.MaSinhVien == sinhVienId);
        ViewBag.CauLacBo = sinhVienId == 0 ? 0 : await _context.ThanhVienClbs.CountAsync(t => t.MaSinhVien == sinhVienId);
        ViewBag.DaCapNhatHoSo = sinhVien != null && await HasAvailabilityAsync(CurrentUserId!.Value) && IsProfileComplete(sinhVien);
        ViewBag.SinhVien = sinhVien;
        ViewBag.TodayText = DateTime.Now.ToString("'Hôm nay là' dddd, dd/MM/yyyy", new System.Globalization.CultureInfo("vi-VN"));

        var upcomingSessions = sinhVienId == 0
            ? new List<LichHoc>()
            : await _context.LichHocs
                .Include(l => l.MaGhepNoiNavigation)
                    .ThenInclude(g => g.MaHuongDanNavigation)
                        .ThenInclude(m => m.MaTaiKhoanNavigation)
                .Include(l => l.MaGhepNoiNavigation)
                    .ThenInclude(g => g.MaYeuCauNavigation)
                        .ThenInclude(y => y.MaLinhVucNavigation)
                .Where(l => l.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVien == sinhVienId && l.NgayHoc >= DateOnly.FromDateTime(DateTime.Today))
                .OrderBy(l => l.NgayHoc)
                .ThenBy(l => l.GioBatDau)
                .Take(3)
                .ToListAsync();
        ViewBag.UpcomingSessions = upcomingSessions;

        ViewBag.MentorSuggestions = await _context.NguoiHuongDans
            .Include(m => m.MaTaiKhoanNavigation)
            .Include(m => m.GhepNoiHocTaps)
            .Include(m => m.ChuyenMonNguoiHuongDans)
                .ThenInclude(c => c.MaLinhVucNavigation)
            .Where(m => m.TrangThai == null || m.TrangThai == "Hoạt động")
            .OrderByDescending(m => m.DiemUyTin)
            .ThenByDescending(m => m.DiemDanhGia)
            .Take(4)
            .ToListAsync();

        ViewBag.ClbActivities = await _context.HoatDongClbs
            .Include(h => h.MaClbNavigation)
            .OrderByDescending(h => h.ThoiGian ?? h.NgayDang)
            .Take(3)
            .ToListAsync();

        var latestMentorApplication = sinhVienId == 0 ? null : await _context.DangKyHuongDans
            .Include(d => d.MaLinhVucNavigation)
            .Where(d => d.MaSinhVien == sinhVienId)
            .OrderByDescending(d => d.NgayDangKy)
            .FirstOrDefaultAsync();

        var databaseNotifications = CurrentUserId.HasValue
            ? await _context.ThongBaos
                .Where(t => t.MaTaiKhoan == CurrentUserId.Value)
                .OrderByDescending(t => t.NgayTao)
                .Take(8)
                .ToListAsync()
            : [];

        ViewBag.MentorApplication = latestMentorApplication;
        ViewBag.DatabaseNotifications = databaseNotifications;
        ViewBag.UnreadNotificationCount = databaseNotifications.Count(t => t.DaDoc != true);
        ViewBag.Notifications = BuildStudentNotifications(upcomingSessions, latestMentorApplication, databaseNotifications);

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationsRead()
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        if (CurrentUserId.HasValue)
        {
            var unread = await _context.ThongBaos
                .Where(t => t.MaTaiKhoan == CurrentUserId.Value && t.DaDoc != true)
                .ToListAsync();

            foreach (var item in unread)
            {
                item.DaDoc = true;
            }

            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> HoSo(string? returnUrl = null)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var userId = CurrentUserId!.Value;
        var sinhVien = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaTaiKhoan == userId);
        var lichRanh = await _context.LichRanhs
            .Where(l => l.MaTaiKhoan == userId)
            .Select(l => $"{l.Thu}|{l.GioBatDau:HH\\:mm}|{l.GioKetThuc:HH\\:mm}")
            .ToListAsync();

        var model = new SinhVienHoSoViewModel
        {
            Mssv = sinhVien?.Mssv ?? string.Empty,
            ChuyenNganh = sinhVien?.ChuyenNganh ?? string.Empty,
            Lop = sinhVien?.Lop,
            Gpa = sinhVien?.Gpa,
            KyNang = sinhVien?.KyNang ?? string.Empty,
            MucTieuHoc = sinhVien?.GioiThieu ?? string.Empty,
            LichRanhDaChon = lichRanh,
            ReturnUrl = returnUrl
        };

        await PopulateHoSoViewDataAsync(returnUrl, sinhVien?.MaSinhVien);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HoSo(SinhVienHoSoViewModel model)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var userId = CurrentUserId!.Value;
        model.LichRanhDaChon ??= [];
        ModelState.Clear();
        NormalizeGpaModelState(model);
        var normalizedMssv = model.Mssv?.Trim();
        var normalizedChuyenNganh = model.ChuyenNganh?.Trim();
        var normalizedLop = model.Lop?.Trim();
        var normalizedKyNang = model.KyNang?.Trim();
        var normalizedMucTieuHoc = model.MucTieuHoc?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedMssv))
        {
            ModelState.AddModelError(nameof(model.Mssv), "Vui lòng nhập mã số sinh viên.");
        }
        else if (normalizedMssv.Length > 50)
        {
            ModelState.AddModelError(nameof(model.Mssv), "Mã số sinh viên không được vượt quá 50 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(normalizedChuyenNganh))
        {
            ModelState.AddModelError(nameof(model.ChuyenNganh), "Vui lòng nhập chuyên ngành.");
        }
        else if (normalizedChuyenNganh.Length > 100)
        {
            ModelState.AddModelError(nameof(model.ChuyenNganh), "Chuyên ngành không được vượt quá 100 ký tự.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedLop) && normalizedLop.Length > 50)
        {
            ModelState.AddModelError(nameof(model.Lop), "Lớp không được vượt quá 50 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(normalizedKyNang))
        {
            ModelState.AddModelError(nameof(model.KyNang), "Vui lòng nhập kỹ năng hiện có.");
        }

        if (string.IsNullOrWhiteSpace(normalizedMucTieuHoc))
        {
            ModelState.AddModelError(nameof(model.MucTieuHoc), "Vui lòng nhập mục tiêu học.");
        }
        if (model.LichRanhDaChon.Count == 0)
        {
            ModelState.AddModelError(nameof(model.LichRanhDaChon), "Vui lòng chọn ít nhất một lịch rảnh.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedMssv) &&
            await _context.SinhViens.AnyAsync(s => s.MaTaiKhoan != userId && s.Mssv == normalizedMssv))
        {
            ModelState.AddModelError(nameof(model.Mssv), "Mã số sinh viên này đã được sử dụng.");
        }

        if (!ModelState.IsValid)
        {
            var currentSinhVien = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaTaiKhoan == userId);
            await PopulateHoSoViewDataAsync(model.ReturnUrl, currentSinhVien?.MaSinhVien);
            ViewBag.ErrorMessage = "Chưa lưu được hồ sơ. Vui lòng kiểm tra các thông tin được báo lỗi bên dưới.";
            return View(model);
        }

        var sinhVien = await _context.SinhViens.FirstOrDefaultAsync(s => s.MaTaiKhoan == userId);
        if (sinhVien == null)
        {
            sinhVien = new SinhVien { MaTaiKhoan = userId, Mssv = normalizedMssv! };
            _context.SinhViens.Add(sinhVien);
        }

        sinhVien.Mssv = normalizedMssv!;
        sinhVien.ChuyenNganh = normalizedChuyenNganh!;
        sinhVien.Lop = string.IsNullOrWhiteSpace(normalizedLop) ? null : normalizedLop;
        sinhVien.Gpa = model.Gpa;
        sinhVien.KyNang = normalizedKyNang!;
        sinhVien.GioiThieu = normalizedMucTieuHoc!;

        var oldSlots = await _context.LichRanhs.Where(l => l.MaTaiKhoan == userId).ToListAsync();
        _context.LichRanhs.RemoveRange(oldSlots);

        foreach (var slot in model.LichRanhDaChon.Distinct().Select(ParseSlot).Where(slot => slot != null))
        {
            _context.LichRanhs.Add(new LichRanh
            {
                MaTaiKhoan = userId,
                Thu = slot!.Value.Thu,
                GioBatDau = slot.Value.BatDau,
                GioKetThuc = slot.Value.KetThuc
            });
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã cập nhật hồ sơ và lịch rảnh.";

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction(nameof(HoSo));
    }

    private void NormalizeGpaModelState(SinhVienHoSoViewModel model)
    {
        var rawGpa = Request.Form[nameof(model.Gpa)].ToString();
        ModelState.Remove(nameof(model.Gpa));

        if (string.IsNullOrWhiteSpace(rawGpa))
        {
            model.Gpa = null;
            return;
        }

        var normalized = rawGpa.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var gpa))
        {
            ModelState.AddModelError(nameof(model.Gpa), "GPA phải là số hợp lệ, ví dụ 3.3 hoặc 3,3.");
            return;
        }

        if (gpa < 0 || gpa > 4)
        {
            ModelState.AddModelError(nameof(model.Gpa), "GPA phải nằm trong khoảng 0 đến 4.");
            return;
        }

        model.Gpa = gpa;
    }

    private async Task PopulateHoSoViewDataAsync(string? returnUrl, int? sinhVienId)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (!sinhVienId.HasValue)
        {
            ViewBag.StudyHistory = new List<LichHoc>();
            return;
        }

        ViewBag.StudyHistory = await _context.LichHocs
            .Include(l => l.BaoCaoBuoiHoc)
            .Include(l => l.MaGhepNoiNavigation)
                .ThenInclude(g => g.MaHuongDanNavigation)
                    .ThenInclude(m => m.MaTaiKhoanNavigation)
            .Include(l => l.MaGhepNoiNavigation)
                .ThenInclude(g => g.MaYeuCauNavigation)
                    .ThenInclude(y => y.MaLinhVucNavigation)
            .Where(l => l.MaGhepNoiNavigation.MaYeuCauNavigation.MaSinhVien == sinhVienId.Value)
            .OrderByDescending(l => l.NgayHoc)
            .ThenByDescending(l => l.GioBatDau)
            .Take(8)
            .ToListAsync();
    }

    private static bool IsProfileComplete(SinhVien sinhVien)
    {
        return !string.IsNullOrWhiteSpace(sinhVien.ChuyenNganh)
            && !string.IsNullOrWhiteSpace(sinhVien.KyNang)
            && !string.IsNullOrWhiteSpace(sinhVien.GioiThieu);
    }

    private async Task<bool> HasAvailabilityAsync(int userId)
    {
        return await _context.LichRanhs.AnyAsync(l => l.MaTaiKhoan == userId);
    }

    private static (int Thu, TimeOnly BatDau, TimeOnly KetThuc)? ParseSlot(string value)
    {
        var parts = value.Split('|');
        if (parts.Length != 3) return null;
        if (!int.TryParse(parts[0], out var thu)) return null;
        if (!TimeOnly.TryParse(parts[1], out var batDau)) return null;
        if (!TimeOnly.TryParse(parts[2], out var ketThuc)) return null;
        return (thu, batDau, ketThuc);
    }

    private static List<DashboardNotificationViewModel> BuildStudentNotifications(List<LichHoc> sessions, DangKyHuongDan? mentorApplication, List<ThongBao> databaseNotifications)
    {
        var notifications = databaseNotifications.Take(5).Select(item => new DashboardNotificationViewModel
        {
            Title = item.TieuDe,
            Message = item.NoiDung ?? string.Empty,
            Url = item.LoaiThongBao == "DangKyMentor" ? "/DangKyHuongDans/Create" : null,
            Tone = item.DaDoc == true ? "blue" : "orange"
        }).ToList();

        notifications.AddRange(sessions.Take(3).Select(session => new DashboardNotificationViewModel
        {
            Title = "Sắp đến lịch học 1-1",
            Message = $"{session.NgayHoc:dd/MM} {session.GioBatDau:HH\\:mm} học {session.MaGhepNoiNavigation.MaYeuCauNavigation.MaLinhVucNavigation.TenLinhVuc} với {session.MaGhepNoiNavigation.MaHuongDanNavigation.MaTaiKhoanNavigation.HoTen}.",
            Url = $"/YeuCauHoTroHocTaps/Details/{session.MaGhepNoiNavigation.MaYeuCau}",
            Tone = "blue"
        }));

        if (mentorApplication != null)
        {
            notifications.Add(new DashboardNotificationViewModel
            {
                Title = "Trạng thái đăng ký mentor",
                Message = $"Hồ sơ {mentorApplication.MaLinhVucNavigation.TenLinhVuc}: {mentorApplication.TrangThaiDuyet}.",
                Url = "/DangKyHuongDans/Create",
                Tone = mentorApplication.TrangThaiDuyet == "Đã duyệt" ? "green" : mentorApplication.TrangThaiDuyet == "Từ chối" ? "orange" : "blue"
            });
        }

        return notifications.Take(5).ToList();
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class DangKyHuongDansController : RoleProtectedController
{
    private static readonly HashSet<string> AllowedEvidenceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".zip", ".rar"
    };

    private const long MaxEvidenceSize = 10 * 1024 * 1024;

    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public DangKyHuongDansController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<IActionResult> Create()
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var sinhVien = await CurrentSinhVienAsync();
        if (sinhVien == null || !IsStudentProfileReady(sinhVien))
        {
            TempData["InfoMessage"] = "Bạn cần cập nhật hồ sơ sinh viên trước khi đăng ký làm mentor.";
            return RedirectToAction("HoSo", "SinhVien", new { returnUrl = Url.Action(nameof(Create), "DangKyHuongDans") });
        }

        var latest = await LatestApplicationAsync(sinhVien.MaSinhVien);
        var model = new MentorApplicationViewModel
        {
            TrangThaiHienTai = latest?.TrangThaiDuyet ?? latest?.TrangThaiCoVan,
            LichRanhDaChon = await CurrentAvailabilityValuesAsync()
        };

        if (latest != null && latest.TrangThaiDuyet != "Từ chối")
        {
            model.MaLinhVuc = latest.MaLinhVuc;
            model.DiemMon = latest.DiemMon;
            model.MinhChung = latest.MinhChung;
            model.BangChungDaTai = latest.MinhChung;
            model.LyDo = latest.LyDo ?? string.Empty;
        }

        await PopulateFieldsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxEvidenceSize)]
    public async Task<IActionResult> Create(MentorApplicationViewModel model)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var sinhVien = await CurrentSinhVienAsync();
        if (sinhVien == null || !IsStudentProfileReady(sinhVien))
        {
            TempData["InfoMessage"] = "Bạn cần cập nhật hồ sơ sinh viên trước khi đăng ký làm mentor.";
            return RedirectToAction("HoSo", "SinhVien", new { returnUrl = Url.Action(nameof(Create), "DangKyHuongDans") });
        }

        if (model.LichRanhDaChon.Count == 0)
        {
            ModelState.AddModelError(nameof(model.LichRanhDaChon), "Vui lòng chọn ít nhất một khung lịch rảnh để cố vấn xét duyệt.");
        }

        if (!await _context.LinhVucHocTaps.AnyAsync(l => l.MaLinhVuc == model.MaLinhVuc))
        {
            ModelState.AddModelError(nameof(model.MaLinhVuc), "Lĩnh vực học tập không hợp lệ.");
        }

        if (model.BangChungFile == null && string.IsNullOrWhiteSpace(model.MinhChung))
        {
            ModelState.AddModelError(nameof(model.BangChungFile), "Vui lòng tải lên hoặc nhập link minh chứng trình độ chuyên môn/kỹ năng.");
        }

        if (model.BangChungFile is { Length: > 0 })
        {
            var extension = Path.GetExtension(model.BangChungFile.FileName);
            if (model.BangChungFile.Length > MaxEvidenceSize)
            {
                ModelState.AddModelError(nameof(model.BangChungFile), "Tệp minh chứng không được vượt quá 10MB.");
            }
            else if (string.IsNullOrWhiteSpace(extension) || !AllowedEvidenceExtensions.Contains(extension))
            {
                ModelState.AddModelError(nameof(model.BangChungFile), "Định dạng minh chứng chưa được hỗ trợ.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateFieldsAsync(model);
            return View(model);
        }

        var pending = await _context.DangKyHuongDans
            .Where(d => d.MaSinhVien == sinhVien.MaSinhVien && d.TrangThaiDuyet == "Chờ duyệt")
            .OrderByDescending(d => d.NgayDangKy)
            .FirstOrDefaultAsync();

        if (pending == null)
        {
            pending = new DangKyHuongDan
            {
                MaSinhVien = sinhVien.MaSinhVien,
                NgayDangKy = DateTime.Now
            };
            _context.DangKyHuongDans.Add(pending);
        }

        var evidence = await SaveEvidenceAsync(model);

        pending.MaLinhVuc = model.MaLinhVuc;
        pending.DiemMon = model.DiemMon;
        pending.MinhChung = evidence;
        pending.LyDo = model.LyDo.Trim();
        pending.TrangThaiClb = "Không yêu cầu";
        pending.TrangThaiCoVan = "Chờ duyệt";
        pending.TrangThaiDuyet = "Chờ duyệt";
        pending.NgayDangKy = DateTime.Now;

        await ReplaceAvailabilityAsync(CurrentUserId!.Value, model.LichRanhDaChon);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã gửi đăng ký làm mentor tới cố vấn. Cố vấn sẽ xem minh chứng chuyên môn/kỹ năng và duyệt kết quả.";
        return RedirectToAction("Index", "SinhVien");
    }

    private async Task<string?> SaveEvidenceAsync(MentorApplicationViewModel model)
    {
        if (model.BangChungFile == null || model.BangChungFile.Length == 0)
        {
            return string.IsNullOrWhiteSpace(model.MinhChung) ? null : TrimToMax(model.MinhChung.Trim(), 255);
        }

        var extension = Path.GetExtension(model.BangChungFile.FileName).ToLowerInvariant();
        var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "mentor-evidence");
        Directory.CreateDirectory(uploadRoot);

        var storedName = $"mentor-{CurrentUserId}-{DateTime.Now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var storedPath = Path.Combine(uploadRoot, storedName);
        await using (var stream = System.IO.File.Create(storedPath))
        {
            await model.BangChungFile.CopyToAsync(stream);
        }

        return $"/uploads/mentor-evidence/{storedName}";
    }

    private async Task<SinhVien?> CurrentSinhVienAsync()
    {
        if (!CurrentUserId.HasValue) return null;
        return await _context.SinhViens.FirstOrDefaultAsync(s => s.MaTaiKhoan == CurrentUserId.Value);
    }

    private async Task<DangKyHuongDan?> LatestApplicationAsync(int sinhVienId)
    {
        return await _context.DangKyHuongDans
            .Include(d => d.MaLinhVucNavigation)
            .Where(d => d.MaSinhVien == sinhVienId)
            .OrderByDescending(d => d.NgayDangKy)
            .FirstOrDefaultAsync();
    }

    private async Task PopulateFieldsAsync(MentorApplicationViewModel model)
    {
        model.LinhVucOptions = await _context.LinhVucHocTaps
            .Where(l => l.TrangThai == null || l.TrangThai == "Hoạt động")
            .OrderBy(l => l.TenLinhVuc)
            .Select(l => new SelectListItem(l.TenLinhVuc, l.MaLinhVuc.ToString(), l.MaLinhVuc == model.MaLinhVuc))
            .ToListAsync();
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

    private static bool IsStudentProfileReady(SinhVien sinhVien)
    {
        return !string.IsNullOrWhiteSpace(sinhVien.ChuyenNganh)
            && !string.IsNullOrWhiteSpace(sinhVien.KyNang)
            && !string.IsNullOrWhiteSpace(sinhVien.GioiThieu);
    }

    private static string TrimToMax(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class DangKyHuongDansController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public DangKyHuongDansController(AppDbContext context)
    {
        _context = context;
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
            TrangThaiHienTai = latest == null ? null : latest.TrangThaiDuyet ?? latest.TrangThaiCoVan,
            LichRanhDaChon = await CurrentAvailabilityValuesAsync()
        };

        if (latest != null && latest.TrangThaiDuyet != "Từ chối")
        {
            model.MaLinhVuc = latest.MaLinhVuc;
            model.DiemMon = latest.DiemMon;
            model.MinhChung = latest.MinhChung;
            model.LyDo = latest.LyDo ?? string.Empty;
        }

        await PopulateFieldsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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

        pending.MaLinhVuc = model.MaLinhVuc;
        pending.DiemMon = model.DiemMon;
        pending.MinhChung = string.IsNullOrWhiteSpace(model.MinhChung) ? null : model.MinhChung.Trim();
        pending.LyDo = model.LyDo.Trim();
        pending.TrangThaiClb = "Không yêu cầu";
        pending.TrangThaiCoVan = "Chờ duyệt";
        pending.TrangThaiDuyet = "Chờ duyệt";
        pending.NgayDangKy = DateTime.Now;

        await ReplaceAvailabilityAsync(CurrentUserId!.Value, model.LichRanhDaChon);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã gửi đăng ký làm mentor tới cố vấn. Bạn sẽ thấy kết quả sau khi cố vấn duyệt hoặc từ chối.";
        return RedirectToAction("Index", "SinhVien");
    }

    private async Task<SinhVien?> CurrentSinhVienAsync()
    {
        if (!CurrentUserId.HasValue) return null;
        return await _context.SinhViens.FirstOrDefaultAsync(s => s.MaTaiKhoan == CurrentUserId.Value);
    }

    private async Task<DangKyHuongDan?> LatestApplicationAsync(int sinhVienId)
    {
        return await _context.DangKyHuongDans
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

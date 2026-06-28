using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class LinhVucHocTapsController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public LinhVucHocTapsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;

        var fields = await _context.LinhVucHocTaps
            .Include(l => l.ChuyenMonNguoiHuongDans)
            .Include(l => l.YeuCauHoTroHocTaps)
            .Include(l => l.DangKyHuongDans)
            .OrderBy(l => l.TenLinhVuc)
            .ToListAsync();

        ViewBag.ActiveCount = fields.Count(l => l.TrangThai == null || l.TrangThai == "Hoạt động");
        ViewBag.HiddenCount = fields.Count(l => l.TrangThai == "Tạm ẩn");
        ViewBag.MentorSpecialtyCount = fields.Sum(l => l.ChuyenMonNguoiHuongDans.Count);
        ViewBag.RequestCount = fields.Sum(l => l.YeuCauHoTroHocTaps.Count);

        return View(fields);
    }

    public IActionResult Create()
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;

        PopulateStatusOptions();
        return View(new LinhVucHocTap { TrangThai = "Hoạt động" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("TenLinhVuc,MoTa,TrangThai")] LinhVucHocTap field)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;

        NormalizeField(field);
        await ValidateFieldAsync(field);

        if (!ModelState.IsValid)
        {
            PopulateStatusOptions(field.TrangThai);
            return View(field);
        }

        _context.LinhVucHocTaps.Add(field);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã thêm lĩnh vực {field.TenLinhVuc}.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;
        if (id == null) return NotFound();

        var field = await _context.LinhVucHocTaps.FindAsync(id);
        if (field == null) return NotFound();

        PopulateStatusOptions(field.TrangThai);
        return View(field);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("MaLinhVuc,TenLinhVuc,MoTa,TrangThai")] LinhVucHocTap field)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;
        if (id != field.MaLinhVuc) return NotFound();

        NormalizeField(field);
        await ValidateFieldAsync(field);

        if (!ModelState.IsValid)
        {
            PopulateStatusOptions(field.TrangThai);
            return View(field);
        }

        var existing = await _context.LinhVucHocTaps.FindAsync(id);
        if (existing == null) return NotFound();

        existing.TenLinhVuc = field.TenLinhVuc;
        existing.MoTa = field.MoTa;
        existing.TrangThai = field.TrangThai;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã cập nhật lĩnh vực {existing.TenLinhVuc}.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int? id)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;
        if (id == null) return NotFound();

        var field = await _context.LinhVucHocTaps
            .Include(l => l.ChuyenMonNguoiHuongDans)
                .ThenInclude(c => c.MaHuongDanNavigation)
                    .ThenInclude(m => m.MaTaiKhoanNavigation)
            .Include(l => l.YeuCauHoTroHocTaps)
            .Include(l => l.DangKyHuongDans)
            .FirstOrDefaultAsync(l => l.MaLinhVuc == id);

        return field == null ? NotFound() : View(field);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;
        if (id == null) return NotFound();

        var field = await _context.LinhVucHocTaps
            .Include(l => l.ChuyenMonNguoiHuongDans)
            .Include(l => l.YeuCauHoTroHocTaps)
            .Include(l => l.DangKyHuongDans)
            .FirstOrDefaultAsync(l => l.MaLinhVuc == id);

        return field == null ? NotFound() : View(field);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var guard = RequireRoles(AccountRoles.QuanTri);
        if (guard != null) return guard;

        var field = await _context.LinhVucHocTaps
            .Include(l => l.ChuyenMonNguoiHuongDans)
            .Include(l => l.YeuCauHoTroHocTaps)
            .Include(l => l.DangKyHuongDans)
            .FirstOrDefaultAsync(l => l.MaLinhVuc == id);

        if (field == null) return NotFound();

        var hasRelatedData = field.ChuyenMonNguoiHuongDans.Any()
            || field.YeuCauHoTroHocTaps.Any()
            || field.DangKyHuongDans.Any();

        if (hasRelatedData)
        {
            field.TrangThai = "Tạm ẩn";
            TempData["SuccessMessage"] = $"Lĩnh vực {field.TenLinhVuc} đã có dữ liệu nên được chuyển sang trạng thái Tạm ẩn.";
        }
        else
        {
            _context.LinhVucHocTaps.Remove(field);
            TempData["SuccessMessage"] = $"Đã xóa lĩnh vực {field.TenLinhVuc}.";
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateFieldAsync(LinhVucHocTap field)
    {
        if (string.IsNullOrWhiteSpace(field.TenLinhVuc))
        {
            ModelState.AddModelError(nameof(field.TenLinhVuc), "Vui lòng nhập tên lĩnh vực.");
        }
        else if (await _context.LinhVucHocTaps.AnyAsync(l => l.MaLinhVuc != field.MaLinhVuc && l.TenLinhVuc == field.TenLinhVuc))
        {
            ModelState.AddModelError(nameof(field.TenLinhVuc), "Tên lĩnh vực này đã tồn tại.");
        }

        if (field.TrangThai is not ("Hoạt động" or "Tạm ẩn"))
        {
            ModelState.AddModelError(nameof(field.TrangThai), "Trạng thái lĩnh vực không hợp lệ.");
        }
    }

    private static void NormalizeField(LinhVucHocTap field)
    {
        field.TenLinhVuc = field.TenLinhVuc?.Trim() ?? string.Empty;
        field.MoTa = string.IsNullOrWhiteSpace(field.MoTa) ? null : field.MoTa.Trim();
        field.TrangThai = string.IsNullOrWhiteSpace(field.TrangThai) ? "Hoạt động" : field.TrangThai.Trim();
    }

    private void PopulateStatusOptions(string? selected = null)
    {
        ViewBag.StatusOptions = new[]
        {
            new SelectListItem("Hoạt động", "Hoạt động", selected == "Hoạt động"),
            new SelectListItem("Tạm ẩn", "Tạm ẩn", selected == "Tạm ẩn")
        };
    }
}

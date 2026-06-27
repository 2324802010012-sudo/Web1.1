using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class ClbController : RoleProtectedController
{
    private static readonly HashSet<string> AllowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt", ".zip", ".rar", ".png", ".jpg", ".jpeg"
    };

    private const long MaxDocumentSize = 20 * 1024 * 1024;

    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public ClbController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(int clbId)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var sinhVien = await CurrentSinhVienAsync();
        var club = await _context.CauLacBos.FindAsync(clbId);
        if (sinhVien == null || club == null) return NotFound();

        var membership = await _context.ThanhVienClbs
            .FirstOrDefaultAsync(t => t.MaClb == clbId && t.MaSinhVien == sinhVien.MaSinhVien);

        if (membership == null)
        {
            _context.ThanhVienClbs.Add(new ThanhVienClb
            {
                MaClb = clbId,
                MaSinhVien = sinhVien.MaSinhVien,
                VaiTroClb = "Thành viên",
                NgayThamGia = DateOnly.FromDateTime(DateTime.Today),
                TrangThai = "Hoạt động"
            });
        }
        else
        {
            membership.VaiTroClb ??= "Thành viên";
            membership.NgayThamGia ??= DateOnly.FromDateTime(DateTime.Today);
            membership.TrangThai = "Hoạt động";
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = $"Bạn đã tham gia {club.TenClb}.";
        return RedirectToAction("CauLacBo", "Home", null, "clb-cua-toi");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxDocumentSize)]
    public async Task<IActionResult> UploadDocument(int clbId, string? tieuDe, string? moTa, IFormFile? tepTaiLieu, string? returnTo)
    {
        var guard = RequireRoles(AccountRoles.SinhVien, AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var club = await _context.CauLacBos.FindAsync(clbId);
        if (club == null) return NotFound();

        if (!await CanUploadToClubAsync(clbId))
        {
            TempData["Error"] = "Bạn cần là thành viên hoặc Chủ nhiệm của CLB để đăng tài liệu.";
            return RedirectAfterDocumentChange(returnTo);
        }

        if (tepTaiLieu == null || tepTaiLieu.Length == 0)
        {
            TempData["Error"] = "Vui lòng chọn tệp tài liệu cần tải lên.";
            return RedirectAfterDocumentChange(returnTo);
        }

        if (tepTaiLieu.Length > MaxDocumentSize)
        {
            TempData["Error"] = "Tệp tài liệu không được vượt quá 20MB.";
            return RedirectAfterDocumentChange(returnTo);
        }

        var extension = Path.GetExtension(tepTaiLieu.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedDocumentExtensions.Contains(extension))
        {
            TempData["Error"] = "Định dạng tài liệu chưa được hỗ trợ.";
            return RedirectAfterDocumentChange(returnTo);
        }

        var uploadRoot = Path.Combine(_environment.WebRootPath, "uploads", "clb-documents");
        Directory.CreateDirectory(uploadRoot);

        var storedName = $"clb-{clbId}-{DateTime.Now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var storedPath = Path.Combine(uploadRoot, storedName);
        await using (var stream = System.IO.File.Create(storedPath))
        {
            await tepTaiLieu.CopyToAsync(stream);
        }

        var cleanTitle = string.IsNullOrWhiteSpace(tieuDe)
            ? Path.GetFileNameWithoutExtension(tepTaiLieu.FileName)
            : tieuDe.Trim();

        _context.TaiLieuClbs.Add(new TaiLieuClb
        {
            MaClb = clbId,
            TieuDe = cleanTitle.Length > 200 ? cleanTitle[..200] : cleanTitle,
            MoTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim(),
            TepDinhKem = $"/uploads/clb-documents/{storedName}",
            NguoiDang = CurrentUserId!.Value,
            NgayDang = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Success"] = $"Đã đăng tài liệu cho {club.TenClb}.";
        return RedirectAfterDocumentChange(returnTo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocument(int id, string? returnTo)
    {
        var guard = RequireRoles(AccountRoles.SinhVien, AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var document = await _context.TaiLieuClbs
            .Include(t => t.MaClbNavigation)
            .FirstOrDefaultAsync(t => t.MaTaiLieu == id);

        if (document == null) return NotFound();

        var canDelete = document.NguoiDang == CurrentUserId ||
            (string.Equals(CurrentUserRole, AccountRoles.ChuNhiemClb, StringComparison.OrdinalIgnoreCase) && await CanUploadToClubAsync(document.MaClb));

        if (!canDelete)
        {
            TempData["Error"] = "Bạn không có quyền xóa tài liệu này.";
            return RedirectAfterDocumentChange(returnTo);
        }

        DeleteStoredDocument(document.TepDinhKem);
        _context.TaiLieuClbs.Remove(document);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Đã xóa tài liệu khỏi {document.MaClbNavigation.TenClb}.";
        return RedirectAfterDocumentChange(returnTo);
    }

    [HttpGet]
    public async Task<IActionResult> ViewDocument(int id)
    {
        var document = await _context.TaiLieuClbs
            .Include(t => t.MaClbNavigation)
            .Include(t => t.NguoiDangNavigation)
            .FirstOrDefaultAsync(t => t.MaTaiLieu == id);

        if (document == null) return NotFound();

        return View(document);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelfNominate(int dotId, string? lyDo)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var sinhVien = await CurrentSinhVienAsync();
        var dot = await _context.DotDeCuPhoChuNhiems.FindAsync(dotId);
        if (sinhVien == null || dot == null) return NotFound();

        if (!await IsActiveMemberAsync(sinhVien.MaSinhVien, dot.MaClb))
        {
            TempData["Error"] = "Bạn cần là thành viên CLB để ứng cử.";
            return RedirectToAction("CauLacBo", "Home", null, "bau-cu");
        }

        if (!IsElectionOpen(dot))
        {
            TempData["Error"] = "Đợt bầu chọn chưa mở hoặc đã kết thúc.";
            return RedirectToAction("CauLacBo", "Home", null, "bau-cu");
        }

        await UpsertCandidateAsync(dotId, sinhVien.MaSinhVien, sinhVien.MaSinhVien, lyDo);
        TempData["Success"] = "Đã ghi nhận thông tin tự ứng cử Phó chủ nhiệm.";
        return RedirectToAction("CauLacBo", "Home", null, "bau-cu");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nominate(int dotId, int sinhVienId, string? lyDo)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var nominator = await CurrentSinhVienAsync();
        var dot = await _context.DotDeCuPhoChuNhiems.FindAsync(dotId);
        if (nominator == null || dot == null) return NotFound();

        if (!await IsActiveMemberAsync(nominator.MaSinhVien, dot.MaClb) ||
            !await IsActiveMemberAsync(sinhVienId, dot.MaClb))
        {
            TempData["Error"] = "Người đề cử và ứng viên phải là thành viên của CLB.";
            return RedirectToAction("CauLacBo", "Home", null, "bau-cu");
        }

        if (!IsElectionOpen(dot))
        {
            TempData["Error"] = "Đợt bầu chọn chưa mở hoặc đã kết thúc.";
            return RedirectToAction("CauLacBo", "Home", null, "bau-cu");
        }

        await UpsertCandidateAsync(dotId, sinhVienId, nominator.MaSinhVien, lyDo);
        TempData["Success"] = "Đã ghi nhận đề cử thành viên.";
        return RedirectToAction("CauLacBo", "Home", null, "bau-cu");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(int dotId, int ungVienId)
    {
        var guard = RequireRoles(AccountRoles.SinhVien);
        if (guard != null) return guard;

        var sinhVien = await CurrentSinhVienAsync();
        var dot = await _context.DotDeCuPhoChuNhiems.FindAsync(dotId);
        var candidate = await _context.UngVienPhoChuNhiems
            .FirstOrDefaultAsync(u => u.MaUngVien == ungVienId && u.MaDot == dotId);

        if (sinhVien == null || dot == null || candidate == null) return NotFound();

        if (!await IsActiveMemberAsync(sinhVien.MaSinhVien, dot.MaClb))
        {
            TempData["Error"] = "Bạn cần là thành viên CLB để bỏ phiếu.";
            return RedirectToAction("CauLacBo", "Home", null, "bau-cu");
        }

        if (!IsElectionOpen(dot))
        {
            TempData["Error"] = "Đợt bầu chọn chưa mở hoặc đã kết thúc.";
            return RedirectToAction("CauLacBo", "Home", null, "bau-cu");
        }

        var vote = await _context.PhieuBauPhoChuNhiems
            .FirstOrDefaultAsync(p => p.MaDot == dotId && p.MaSinhVienBau == sinhVien.MaSinhVien);

        if (vote == null)
        {
            _context.PhieuBauPhoChuNhiems.Add(new PhieuBauPhoChuNhiem
            {
                MaDot = dotId,
                MaUngVien = ungVienId,
                MaSinhVienBau = sinhVien.MaSinhVien,
                ThoiGianBau = DateTime.Now
            });
        }
        else
        {
            vote.MaUngVien = ungVienId;
            vote.ThoiGianBau = DateTime.Now;
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Lá phiếu của bạn đã được cập nhật.";
        return RedirectToAction("CauLacBo", "Home", null, "bau-cu");
    }

    private async Task<SinhVien?> CurrentSinhVienAsync()
    {
        return CurrentUserId.HasValue
            ? await _context.SinhViens.FirstOrDefaultAsync(s => s.MaTaiKhoan == CurrentUserId.Value)
            : null;
    }

    private async Task<bool> IsActiveMemberAsync(int sinhVienId, int clbId)
    {
        return await _context.ThanhVienClbs.AnyAsync(t =>
            t.MaSinhVien == sinhVienId &&
            t.MaClb == clbId &&
            (t.TrangThai == null || (t.TrangThai != "Đã rời" && t.TrangThai != "Đã khóa")));
    }

    private async Task<bool> CanUploadToClubAsync(int clbId)
    {
        if (!CurrentUserId.HasValue) return false;

        var sinhVien = await CurrentSinhVienAsync();
        if (sinhVien == null) return false;

        var isMember = await IsActiveMemberAsync(sinhVien.MaSinhVien, clbId);
        if (!isMember) return false;

        if (string.Equals(CurrentUserRole, AccountRoles.ChuNhiemClb, StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(CurrentUserRole, AccountRoles.SinhVien, StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult RedirectAfterDocumentChange(string? returnTo)
    {
        if (string.Equals(returnTo, "ChuNhiem", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Index", "ChuNhiemCLB", null, "tai-lieu-clb");
        }

        return RedirectToAction("CauLacBo", "Home", null, "tai-lieu-clb");
    }

    private void DeleteStoredDocument(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath)) return;

        var uploadRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "clb-documents"));
        var fileName = Path.GetFileName(publicPath);
        if (string.IsNullOrWhiteSpace(fileName)) return;

        var filePath = Path.GetFullPath(Path.Combine(uploadRoot, fileName));
        if (!filePath.StartsWith(uploadRoot, StringComparison.OrdinalIgnoreCase)) return;
        if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
    }

    private static bool IsElectionOpen(DotDeCuPhoChuNhiem dot)
    {
        var now = DateTime.Now;
        var activeStatus = dot.TrangThai == null ||
            dot.TrangThai == "Hoạt động" ||
            dot.TrangThai == "Đang diễn ra" ||
            dot.TrangThai == "Mở";

        return activeStatus && dot.ThoiGianBatDau <= now && dot.ThoiGianKetThuc >= now;
    }

    private async Task UpsertCandidateAsync(int dotId, int sinhVienId, int nguoiDeCu, string? lyDo)
    {
        var candidate = await _context.UngVienPhoChuNhiems
            .FirstOrDefaultAsync(u => u.MaDot == dotId && u.MaSinhVien == sinhVienId);

        if (candidate == null)
        {
            _context.UngVienPhoChuNhiems.Add(new UngVienPhoChuNhiem
            {
                MaDot = dotId,
                MaSinhVien = sinhVienId,
                NguoiDeCu = nguoiDeCu,
                LyDoDeCu = string.IsNullOrWhiteSpace(lyDo) ? "Thành viên được cộng đồng đề cử." : lyDo.Trim(),
                TrangThai = "Hợp lệ"
            });
        }
        else
        {
            candidate.NguoiDeCu ??= nguoiDeCu;
            candidate.LyDoDeCu = string.IsNullOrWhiteSpace(lyDo) ? candidate.LyDoDeCu : lyDo.Trim();
            candidate.TrangThai = "Hợp lệ";
        }

        await _context.SaveChangesAsync();
    }
}

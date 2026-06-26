using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class ClbController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public ClbController(AppDbContext context)
    {
        _context = context;
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
            (t.TrangThai == null || t.TrangThai == "Hoạt động"));
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
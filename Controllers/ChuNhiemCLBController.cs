using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers;

public class ChuNhiemCLBController : RoleProtectedController
{
    private readonly AppDbContext _context;

    public ChuNhiemCLBController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var clbIds = await ManagedClubIdsAsync();
        var clubs = clbIds.Count == 0
            ? new List<CauLacBo>()
            : await _context.CauLacBos
                .Where(c => clbIds.Contains(c.MaClb))
                .OrderBy(c => c.TenClb)
                .ToListAsync();

        var pendingApplications = await PendingMentorApplicationsQuery(clbIds)
            .OrderByDescending(d => d.NgayDangKy)
            .Take(10)
            .ToListAsync();

        var model = new ChuNhiemClbDashboardViewModel
        {
            UserName = CurrentUserName,
            Clubs = clubs,
            MemberCount = clbIds.Count == 0 ? 0 : await _context.ThanhVienClbs.CountAsync(t => clbIds.Contains(t.MaClb) && (t.TrangThai == null || (t.TrangThai != "Đã rời" && t.TrangThai != "Đã khóa"))),
            Members = clbIds.Count == 0 ? [] : await _context.ThanhVienClbs
                .Include(t => t.MaClbNavigation)
                .Include(t => t.MaSinhVienNavigation)
                    .ThenInclude(s => s.MaTaiKhoanNavigation)
                .Where(t => clbIds.Contains(t.MaClb))
                .OrderBy(t => t.MaClbNavigation.TenClb)
                .ThenBy(t => t.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen)
                .Take(60)
                .ToListAsync(),
            Activities = clbIds.Count == 0 ? [] : await _context.HoatDongClbs
                .Include(h => h.MaClbNavigation)
                .Where(h => clbIds.Contains(h.MaClb))
                .OrderByDescending(h => h.ThoiGian ?? h.NgayDang)
                .Take(8)
                .ToListAsync(),
            Documents = clbIds.Count == 0 ? [] : await _context.TaiLieuClbs
                .Include(t => t.MaClbNavigation)
                .Include(t => t.NguoiDangNavigation)
                .Where(t => clbIds.Contains(t.MaClb))
                .OrderByDescending(t => t.NgayDang)
                .Take(12)
                .ToListAsync(),
            Elections = clbIds.Count == 0 ? [] : await _context.DotDeCuPhoChuNhiems
                .Include(d => d.MaClbNavigation)
                .Include(d => d.UngVienPhoChuNhiems)
                    .ThenInclude(u => u.MaSinhVienNavigation)
                        .ThenInclude(s => s.MaTaiKhoanNavigation)
                .Include(d => d.UngVienPhoChuNhiems)
                    .ThenInclude(u => u.NguoiDeCuNavigation)
                        .ThenInclude(s => s!.MaTaiKhoanNavigation)
                .Include(d => d.PhieuBauPhoChuNhiems)
                .Where(d => clbIds.Contains(d.MaClb))
                .OrderByDescending(d => d.ThoiGianBatDau)
                .Take(8)
                .ToListAsync(),
            CandidateVoteCounts = clbIds.Count == 0 ? [] : await _context.UngVienPhoChuNhiems
                .Where(u => clbIds.Contains(u.MaDotNavigation.MaClb))
                .Select(u => new
                {
                    u.MaUngVien,
                    Votes = u.PhieuBauPhoChuNhiems.Count
                })
                .ToDictionaryAsync(x => x.MaUngVien, x => x.Votes),
            PendingMentorApplications = pendingApplications,
            PendingMentorConfirmations = pendingApplications.Count
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateClub(int clbId, string tenClb, string? moTa, string? trangThai)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        if (!await IsManagedClubAsync(clbId)) return NotFound();
        var club = await _context.CauLacBos.FindAsync(clbId);
        if (club == null) return NotFound();

        if (string.IsNullOrWhiteSpace(tenClb))
        {
            TempData["Error"] = "Tên CLB không được để trống.";
            return RedirectDashboard("quan-ly-clb");
        }

        club.TenClb = tenClb.Trim();
        club.MoTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim();
        club.TrangThai = string.IsNullOrWhiteSpace(trangThai) ? "Hoạt động" : trangThai.Trim();
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã cập nhật thông tin CLB.";
        return RedirectDashboard("quan-ly-clb");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateActivity(int clbId, string tieuDe, string? noiDung, DateTime? thoiGian, string? diaDiem)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        if (!await IsManagedClubAsync(clbId)) return NotFound();
        if (string.IsNullOrWhiteSpace(tieuDe))
        {
            TempData["Error"] = "Tiêu đề hoạt động không được để trống.";
            return RedirectDashboard("hoat-dong-clb");
        }

        var activity = new HoatDongClb
        {
            MaClb = clbId,
            TieuDe = tieuDe.Trim(),
            NoiDung = string.IsNullOrWhiteSpace(noiDung) ? null : noiDung.Trim(),
            ThoiGian = thoiGian,
            DiaDiem = string.IsNullOrWhiteSpace(diaDiem) ? null : diaDiem.Trim(),
            NguoiDang = CurrentUserId,
            NgayDang = DateTime.Now
        };

        _context.HoatDongClbs.Add(activity);
        await NotifyClubMembersAsync(clbId, "Hoạt động CLB mới", $"CLB vừa đăng hoạt động: {activity.TieuDe}.", "HoatDongCLB");
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã đăng hoạt động CLB.";
        return RedirectDashboard("hoat-dong-clb");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteActivity(int id)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var activity = await _context.HoatDongClbs.FindAsync(id);
        if (activity == null) return NotFound();
        if (!await IsManagedClubAsync(activity.MaClb)) return NotFound();

        _context.HoatDongClbs.Remove(activity);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã xóa hoạt động CLB.";
        return RedirectDashboard("hoat-dong-clb");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int clbId, string sinhVienIdentifier, string? vaiTroClb)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        if (!await IsManagedClubAsync(clbId)) return NotFound();
        if (string.IsNullOrWhiteSpace(sinhVienIdentifier))
        {
            TempData["Error"] = "Vui lòng nhập MSSV hoặc email sinh viên.";
            return RedirectDashboard("thanh-vien-clb");
        }

        var identifier = sinhVienIdentifier.Trim();
        var student = await _context.SinhViens
            .Include(s => s.MaTaiKhoanNavigation)
            .FirstOrDefaultAsync(s =>
                s.Mssv == identifier ||
                s.MaTaiKhoanNavigation.Email == identifier);

        if (student == null)
        {
            TempData["Error"] = "Không tìm thấy sinh viên theo MSSV hoặc email đã nhập.";
            return RedirectDashboard("thanh-vien-clb");
        }

        var membership = await _context.ThanhVienClbs
            .FirstOrDefaultAsync(t => t.MaClb == clbId && t.MaSinhVien == student.MaSinhVien);

        var role = string.IsNullOrWhiteSpace(vaiTroClb) ? "Thành viên" : vaiTroClb.Trim();
        if (membership == null)
        {
            _context.ThanhVienClbs.Add(new ThanhVienClb
            {
                MaClb = clbId,
                MaSinhVien = student.MaSinhVien,
                VaiTroClb = role,
                NgayThamGia = DateOnly.FromDateTime(DateTime.Today),
                TrangThai = "Hoạt động"
            });
        }
        else
        {
            membership.VaiTroClb = role;
            membership.TrangThai = "Hoạt động";
            membership.NgayThamGia ??= DateOnly.FromDateTime(DateTime.Today);
        }

        var clubName = await _context.CauLacBos
            .Where(c => c.MaClb == clbId)
            .Select(c => c.TenClb)
            .FirstAsync();

        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = student.MaTaiKhoan,
            TieuDe = "Bạn đã được thêm vào CLB",
            NoiDung = $"Chủ nhiệm đã thêm bạn vào {clubName} với vai trò {role}.",
            LoaiThongBao = "ThanhVienCLB",
            DaDoc = false,
            NgayTao = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Success"] = $"Đã thêm {student.MaTaiKhoanNavigation.HoTen} vào {clubName}.";
        return RedirectDashboard("thanh-vien-clb");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMember(int memberId, string vaiTroClb, string trangThai)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var member = await _context.ThanhVienClbs
            .Include(t => t.MaSinhVienNavigation)
            .FirstOrDefaultAsync(t => t.MaThanhVien == memberId);

        if (member == null) return NotFound();
        if (!await IsManagedClubAsync(member.MaClb)) return NotFound();

        member.VaiTroClb = string.IsNullOrWhiteSpace(vaiTroClb) ? "Thành viên" : vaiTroClb.Trim();
        member.TrangThai = string.IsNullOrWhiteSpace(trangThai) ? "Hoạt động" : trangThai.Trim();

        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = member.MaSinhVienNavigation.MaTaiKhoan,
            TieuDe = "Thông tin thành viên CLB được cập nhật",
            NoiDung = $"Vai trò/trạng thái CLB của bạn vừa được Chủ nhiệm cập nhật: {member.VaiTroClb} - {member.TrangThai}.",
            LoaiThongBao = "ThanhVienCLB",
            DaDoc = false,
            NgayTao = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật thành viên CLB.";
        return RedirectDashboard("thanh-vien-clb");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateElection(int clbId, string tenDot, DateTime thoiGianBatDau, DateTime thoiGianKetThuc)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        if (!await IsManagedClubAsync(clbId)) return NotFound();
        if (string.IsNullOrWhiteSpace(tenDot))
        {
            TempData["Error"] = "Tên đợt bầu cử không được để trống.";
            return RedirectDashboard("bau-cu");
        }

        if (thoiGianKetThuc <= thoiGianBatDau)
        {
            TempData["Error"] = "Thời gian kết thúc phải sau thời gian bắt đầu.";
            return RedirectDashboard("bau-cu");
        }

        _context.DotDeCuPhoChuNhiems.Add(new DotDeCuPhoChuNhiem
        {
            MaClb = clbId,
            TenDot = tenDot.Trim(),
            ThoiGianBatDau = thoiGianBatDau,
            ThoiGianKetThuc = thoiGianKetThuc,
            TrangThai = "Mở"
        });

        await NotifyClubMembersAsync(clbId, "Mở bầu Phó chủ nhiệm CLB", $"CLB đã mở đợt bầu cử: {tenDot.Trim()}.", "BauCuCLB");
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã tạo đợt bầu cử Phó chủ nhiệm.";
        return RedirectDashboard("bau-cu");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddElectionCandidate(int electionId, int sinhVienId, string? lyDo)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var election = await _context.DotDeCuPhoChuNhiems
            .Include(d => d.MaClbNavigation)
            .FirstOrDefaultAsync(d => d.MaDot == electionId);

        if (election == null) return NotFound();
        if (!await IsManagedClubAsync(election.MaClb)) return NotFound();

        var membership = await _context.ThanhVienClbs
            .Include(t => t.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
            .FirstOrDefaultAsync(t => t.MaClb == election.MaClb && t.MaSinhVien == sinhVienId);

        if (membership == null || !IsActiveMembership(membership))
        {
            TempData["Error"] = "Thành viên được chọn không thuộc CLB hoặc không còn hoạt động.";
            return RedirectDashboard("bau-cu");
        }

        var exists = await _context.UngVienPhoChuNhiems
            .AnyAsync(u => u.MaDot == electionId && u.MaSinhVien == sinhVienId);

        if (exists)
        {
            TempData["Error"] = "Thành viên này đã nằm trong danh sách ứng viên của đợt bầu cử.";
            return RedirectDashboard("bau-cu");
        }

        var nominatorId = await CurrentSinhVienIdAsync();
        _context.UngVienPhoChuNhiems.Add(new UngVienPhoChuNhiem
        {
            MaDot = electionId,
            MaSinhVien = sinhVienId,
            NguoiDeCu = nominatorId == 0 ? null : nominatorId,
            LyDoDeCu = string.IsNullOrWhiteSpace(lyDo)
                ? "Chủ nhiệm CLB thêm vào danh sách ứng viên."
                : lyDo.Trim(),
            TrangThai = "Hợp lệ"
        });

        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = membership.MaSinhVienNavigation.MaTaiKhoan,
            TieuDe = "Bạn được thêm vào danh sách ứng viên Phó chủ nhiệm",
            NoiDung = $"Bạn đã được thêm vào đợt bầu cử {election.TenDot} của {election.MaClbNavigation.TenClb}.",
            LoaiThongBao = "BauCuCLB",
            DaDoc = false,
            NgayTao = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Success"] = $"Đã thêm {membership.MaSinhVienNavigation.MaTaiKhoanNavigation.HoTen} vào danh sách ứng viên.";
        return RedirectDashboard("bau-cu");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseElection(int id)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var election = await _context.DotDeCuPhoChuNhiems.FindAsync(id);
        if (election == null) return NotFound();
        if (!await IsManagedClubAsync(election.MaClb)) return NotFound();

        election.TrangThai = "Đã kết thúc";
        election.ThoiGianKetThuc = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã kết thúc đợt bầu cử.";
        return RedirectDashboard("bau-cu");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCandidateStatus(int id, string trangThai)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var candidate = await _context.UngVienPhoChuNhiems
            .Include(u => u.MaDotNavigation)
            .Include(u => u.MaSinhVienNavigation)
            .FirstOrDefaultAsync(u => u.MaUngVien == id);

        if (candidate == null) return NotFound();
        if (!await IsManagedClubAsync(candidate.MaDotNavigation.MaClb)) return NotFound();

        candidate.TrangThai = string.IsNullOrWhiteSpace(trangThai) ? "Hợp lệ" : trangThai.Trim();
        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = candidate.MaSinhVienNavigation.MaTaiKhoan,
            TieuDe = "Trạng thái ứng viên Phó chủ nhiệm",
            NoiDung = $"Hồ sơ ứng viên của bạn trong đợt {candidate.MaDotNavigation.TenDot} đã được cập nhật: {candidate.TrangThai}.",
            LoaiThongBao = "BauCuCLB",
            DaDoc = false,
            NgayTao = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật trạng thái ứng viên.";
        return RedirectDashboard("bau-cu");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteWinner(int electionId)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var election = await _context.DotDeCuPhoChuNhiems
            .Include(d => d.UngVienPhoChuNhiems)
                .ThenInclude(u => u.PhieuBauPhoChuNhiems)
            .Include(d => d.UngVienPhoChuNhiems)
                .ThenInclude(u => u.MaSinhVienNavigation)
            .FirstOrDefaultAsync(d => d.MaDot == electionId);

        if (election == null) return NotFound();
        if (!await IsManagedClubAsync(election.MaClb)) return NotFound();

        var winner = election.UngVienPhoChuNhiems
            .Where(u => u.TrangThai == null || u.TrangThai == "Hợp lệ")
            .OrderByDescending(u => u.PhieuBauPhoChuNhiems.Count)
            .ThenBy(u => u.MaUngVien)
            .FirstOrDefault();

        if (winner == null || winner.PhieuBauPhoChuNhiems.Count == 0)
        {
            TempData["Error"] = "Chưa có ứng viên hợp lệ có phiếu bầu để công nhận.";
            return RedirectDashboard("bau-cu");
        }

        var membership = await _context.ThanhVienClbs
            .FirstOrDefaultAsync(t => t.MaClb == election.MaClb && t.MaSinhVien == winner.MaSinhVien);
        if (membership == null) return NotFound();

        membership.VaiTroClb = "Phó chủ nhiệm";
        membership.TrangThai = "Hoạt động";
        election.TrangThai = "Đã kết thúc";
        election.ThoiGianKetThuc = DateTime.Now;

        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = winner.MaSinhVienNavigation.MaTaiKhoan,
            TieuDe = "Bạn được công nhận Phó chủ nhiệm CLB",
            NoiDung = $"Bạn đã được công nhận là Phó chủ nhiệm sau đợt bầu cử {election.TenDot}.",
            LoaiThongBao = "BauCuCLB",
            DaDoc = false,
            NgayTao = DateTime.Now
        });

        await _context.SaveChangesAsync();
        TempData["Success"] = "Đã công nhận ứng viên có phiếu cao nhất làm Phó chủ nhiệm.";
        return RedirectDashboard("bau-cu");
    }

    public async Task<IActionResult> DangKyMentorDetails(int id)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var clbIds = await ManagedClubIdsAsync();
        var application = await PendingMentorApplicationsQuery(clbIds)
            .FirstOrDefaultAsync(d => d.MaDangKy == id);

        if (application == null) return NotFound();
        ViewBag.ManagedClubNames = await ManagedClubNamesForStudentAsync(clbIds, application.MaSinhVien);
        return View(application);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XacNhanMentorClb(int id)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var clbIds = await ManagedClubIdsAsync();
        var application = await PendingMentorApplicationsQuery(clbIds)
            .FirstOrDefaultAsync(d => d.MaDangKy == id);

        if (application == null) return NotFound();

        application.TrangThaiClb = "Đã xác nhận";
        await AddStudentNotificationAsync(application, "Chủ nhiệm CLB đã xác nhận hồ sơ mentor", "Hồ sơ mentor của bạn đã được CLB xác nhận và đang chờ cố vấn duyệt chuyên môn.");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xác nhận đóng góp CLB cho hồ sơ mentor.";
        return RedirectToAction(nameof(DangKyMentorDetails), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TuChoiMentorClb(int id)
    {
        var guard = RequireRoles(AccountRoles.ChuNhiemClb);
        if (guard != null) return guard;

        var clbIds = await ManagedClubIdsAsync();
        var application = await PendingMentorApplicationsQuery(clbIds)
            .FirstOrDefaultAsync(d => d.MaDangKy == id);

        if (application == null) return NotFound();

        application.TrangThaiClb = "Từ chối";
        application.TrangThaiCoVan = "Không duyệt";
        application.TrangThaiDuyet = "Từ chối";
        await AddStudentNotificationAsync(application, "Hồ sơ mentor bị CLB từ chối", "Chủ nhiệm CLB chưa xác nhận đủ điều kiện đóng góp CLB cho hồ sơ mentor của bạn.");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã từ chối xác nhận CLB cho hồ sơ mentor.";
        return RedirectToAction(nameof(Index), null, null, "xac-nhan-mentor");
    }

    private async Task<List<int>> ManagedClubIdsAsync()
    {
        if (!CurrentUserId.HasValue) return [];

        var sinhVienId = await _context.SinhViens
            .Where(s => s.MaTaiKhoan == CurrentUserId.Value)
            .Select(s => s.MaSinhVien)
            .FirstOrDefaultAsync();

        if (sinhVienId == 0) return [];

        var ownedClubIds = await _context.ThanhVienClbs
            .Where(t => t.MaSinhVien == sinhVienId && (t.TrangThai == null || (t.TrangThai != "Đã rời" && t.TrangThai != "Đã khóa")))
            .Where(t => t.VaiTroClb != null && (t.VaiTroClb.Contains("Chủ nhiệm") || t.VaiTroClb.Contains("Chu nhiem")))
            .Select(t => t.MaClb)
            .Distinct()
            .ToListAsync();

        return ownedClubIds;
    }

    private async Task<bool> IsManagedClubAsync(int clbId)
    {
        var clbIds = await ManagedClubIdsAsync();
        return clbIds.Contains(clbId);
    }

    private async Task<int> CurrentSinhVienIdAsync()
    {
        if (!CurrentUserId.HasValue) return 0;

        return await _context.SinhViens
            .Where(s => s.MaTaiKhoan == CurrentUserId.Value)
            .Select(s => s.MaSinhVien)
            .FirstOrDefaultAsync();
    }

    private IActionResult RedirectDashboard(string fragment)
    {
        return RedirectToAction(nameof(Index), null, null, fragment);
    }

    private static bool IsActiveMembership(ThanhVienClb member)
    {
        return member.TrangThai == null || (member.TrangThai != "Đã rời" && member.TrangThai != "Đã khóa");
    }

    private async Task NotifyClubMembersAsync(int clbId, string title, string message, string type)
    {
        var accountIds = await _context.ThanhVienClbs
            .Include(t => t.MaSinhVienNavigation)
            .Where(t => t.MaClb == clbId && (t.TrangThai == null || (t.TrangThai != "Đã rời" && t.TrangThai != "Đã khóa")))
            .Select(t => t.MaSinhVienNavigation.MaTaiKhoan)
            .Distinct()
            .ToListAsync();

        foreach (var accountId in accountIds)
        {
            _context.ThongBaos.Add(new ThongBao
            {
                MaTaiKhoan = accountId,
                TieuDe = title,
                NoiDung = message,
                LoaiThongBao = type,
                DaDoc = false,
                NgayTao = DateTime.Now
            });
        }
    }

    private IQueryable<DangKyHuongDan> PendingMentorApplicationsQuery(List<int> clbIds)
    {
        if (clbIds.Count == 0) return _context.DangKyHuongDans.Where(d => false);

        return _context.DangKyHuongDans
            .Include(d => d.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
            .Include(d => d.MaSinhVienNavigation)
                .ThenInclude(s => s.ThanhVienClbs)
                    .ThenInclude(t => t.MaClbNavigation)
            .Include(d => d.MaLinhVucNavigation)
            .Where(d => d.TrangThaiClb == "Chờ xác nhận" &&
                d.MaSinhVienNavigation.ThanhVienClbs.Any(t => clbIds.Contains(t.MaClb) && (t.TrangThai == null || (t.TrangThai != "Đã rời" && t.TrangThai != "Đã khóa"))));
    }

    private async Task<List<string>> ManagedClubNamesForStudentAsync(List<int> clbIds, int sinhVienId)
    {
        return await _context.ThanhVienClbs
            .Include(t => t.MaClbNavigation)
            .Where(t => t.MaSinhVien == sinhVienId && clbIds.Contains(t.MaClb) && (t.TrangThai == null || (t.TrangThai != "Đã rời" && t.TrangThai != "Đã khóa")))
            .Select(t => t.MaClbNavigation.TenClb)
            .ToListAsync();
    }

    private async Task AddStudentNotificationAsync(DangKyHuongDan application, string title, string message)
    {
        var accountId = await _context.SinhViens
            .Where(s => s.MaSinhVien == application.MaSinhVien)
            .Select(s => s.MaTaiKhoan)
            .FirstOrDefaultAsync();

        _context.ThongBaos.Add(new ThongBao
        {
            MaTaiKhoan = accountId,
            TieuDe = title,
            NoiDung = message,
            LoaiThongBao = "DangKyMentor",
            DaDoc = false,
            NgayTao = DateTime.Now
        });
    }
}

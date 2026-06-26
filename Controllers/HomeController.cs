using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Hoc11(int? linhVucId, string? keyword)
        {
            var fields = await _context.LinhVucHocTaps
                .Where(l => l.TrangThai == null || l.TrangThai == "Hoạt động")
                .OrderBy(l => l.TenLinhVuc)
                .ToListAsync();

            var mentorQuery = _context.NguoiHuongDans
                .Include(m => m.MaTaiKhoanNavigation)
                    .ThenInclude(t => t.LichRanhs)
                .Include(m => m.ChuyenMonNguoiHuongDans)
                    .ThenInclude(c => c.MaLinhVucNavigation)
                .Include(m => m.GhepNoiHocTaps)
                    .ThenInclude(g => g.LichHocs)
                        .ThenInclude(l => l.BaoCaoBuoiHoc)
                .Where(m => m.TrangThai == null || m.TrangThai == "Hoạt động")
                .Where(m => m.ChuyenMonNguoiHuongDans.Any())
                .AsQueryable();

            if (linhVucId.HasValue)
            {
                mentorQuery = mentorQuery.Where(m => m.ChuyenMonNguoiHuongDans.Any(c => c.MaLinhVuc == linhVucId.Value));
            }

            var cleanKeyword = keyword?.Trim();
            if (!string.IsNullOrWhiteSpace(cleanKeyword))
            {
                mentorQuery = mentorQuery.Where(m =>
                    m.MaTaiKhoanNavigation.HoTen.Contains(cleanKeyword)
                    || m.ChuyenMonNguoiHuongDans.Any(c =>
                        c.MaLinhVucNavigation.TenLinhVuc.Contains(cleanKeyword)
                        || (c.MoTaKinhNghiem != null && c.MoTaKinhNghiem.Contains(cleanKeyword))));
            }

            var mentors = await mentorQuery.ToListAsync();
            mentors = mentors
                .OrderByDescending(m => CalculateFitPercent(m, linhVucId, cleanKeyword))
                .ThenByDescending(m => m.DiemUyTin)
                .ThenByDescending(m => m.DiemDanhGia)
                .Take(8)
                .ToList();

            var activeMentorQuery = _context.NguoiHuongDans
                .Where(m => m.TrangThai == null || m.TrangThai == "Hoạt động");

            var averageRating = await activeMentorQuery
                .Where(m => m.DiemDanhGia.HasValue)
                .AverageAsync(m => (decimal?)m.DiemDanhGia) ?? 0;

            var model = new Hoc11ViewModel
            {
                SelectedLinhVucId = linhVucId,
                Keyword = cleanKeyword,
                MentorCount = await activeMentorQuery.CountAsync(),
                CompletedSessionCount = await _context.LichHocs.CountAsync(l => l.TrangThai == "Đã hoàn thành" || l.BaoCaoBuoiHoc != null),
                AverageRating = averageRating,
                LinhVucOptions = fields
                    .Select(l => new SelectListItem(l.TenLinhVuc, l.MaLinhVuc.ToString(), l.MaLinhVuc == linhVucId))
                    .ToList(),
                PopularTags = fields.Take(6).Select(l => l.TenLinhVuc).ToList(),
                MentorGoiY = mentors.Select(m => BuildMentorCard(m, linhVucId, cleanKeyword)).ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> MentorDetail(int id)
        {
            var mentor = await _context.NguoiHuongDans
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
                .FirstOrDefaultAsync(m => m.MaHuongDan == id && (m.TrangThai == null || m.TrangThai == "Hoạt động"));

            if (mentor == null) return NotFound();

            var card = BuildMentorCard(mentor, null, null);
            var student = mentor.MaTaiKhoanNavigation.SinhVien;
            var model = new MentorDetailViewModel
            {
                MaHuongDan = card.MaHuongDan,
                HoTen = card.HoTen,
                AnhDaiDien = card.AnhDaiDien,
                LoaiNguoiHuongDan = card.LoaiNguoiHuongDan,
                DiemDanhGia = card.DiemDanhGia,
                SoLuotDanhGia = card.SoLuotDanhGia,
                DiemUyTin = card.DiemUyTin,
                SoBuoiHoTro = card.SoBuoiHoTro,
                TyLePhuHop = card.TyLePhuHop,
                TrangThaiLichRanh = card.TrangThaiLichRanh,
                MoTaKinhNghiem = card.MoTaKinhNghiem,
                ChuyenMon = card.ChuyenMon,
                Email = mentor.MaTaiKhoanNavigation.Email,
                SoDienThoai = mentor.MaTaiKhoanNavigation.SoDienThoai,
                ChuyenNganh = student?.ChuyenNganh,
                KyNang = student?.KyNang,
                MucTieuChiaSe = student?.GioiThieu,
                LichRanh = mentor.MaTaiKhoanNavigation.LichRanhs
                    .OrderBy(l => l.Thu)
                    .ThenBy(l => l.GioBatDau)
                    .Select(l => $"{FormatDay(l.Thu)} {l.GioBatDau:HH\\:mm}-{l.GioKetThuc:HH\\:mm}")
                    .ToList(),
                CauLacBo = student?.ThanhVienClbs
                    .Where(t => t.TrangThai == null || t.TrangThai == "Hoạt động")
                    .Select(t => $"{t.MaClbNavigation.TenClb} - {t.VaiTroClb ?? "Thành viên"}")
                    .ToList() ?? [],
                LichSuHoTro = mentor.GhepNoiHocTaps
                    .SelectMany(g => g.LichHocs)
                    .OrderByDescending(l => l.NgayHoc)
                    .Take(5)
                    .Select(l => $"{l.NgayHoc:dd/MM/yyyy} - {l.GioBatDau:HH\\:mm}-{l.GioKetThuc:HH\\:mm} - {l.TrangThai ?? "Đã lên lịch"}")
                    .ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> CauLacBo()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var sinhVien = userId.HasValue
                ? await _context.SinhViens.FirstOrDefaultAsync(s => s.MaTaiKhoan == userId.Value)
                : null;

            var clubs = await _context.CauLacBos
                .Include(c => c.ThanhVienClbs)
                    .ThenInclude(t => t.MaSinhVienNavigation)
                        .ThenInclude(s => s.MaTaiKhoanNavigation)
                .Where(c => c.TrangThai == null || c.TrangThai == "Hoạt động")
                .OrderBy(c => c.TenClb)
                .ToListAsync();

            var elections = await _context.DotDeCuPhoChuNhiems
                .Include(d => d.MaClbNavigation)
                    .ThenInclude(c => c.ThanhVienClbs)
                        .ThenInclude(t => t.MaSinhVienNavigation)
                            .ThenInclude(s => s.MaTaiKhoanNavigation)
                .Include(d => d.UngVienPhoChuNhiems)
                    .ThenInclude(u => u.MaSinhVienNavigation)
                        .ThenInclude(s => s.MaTaiKhoanNavigation)
                .Include(d => d.UngVienPhoChuNhiems)
                    .ThenInclude(u => u.NguoiDeCuNavigation)
                        .ThenInclude(s => s!.MaTaiKhoanNavigation)
                .Include(d => d.PhieuBauPhoChuNhiems)
                .OrderByDescending(d => d.ThoiGianBatDau)
                .Take(6)
                .ToListAsync();

            var model = new ClubCommunityViewModel
            {
                CurrentSinhVienId = sinhVien?.MaSinhVien,
                Clubs = clubs,
                MyMemberships = sinhVien == null
                    ? []
                    : await _context.ThanhVienClbs
                        .Include(t => t.MaClbNavigation)
                        .Where(t => t.MaSinhVien == sinhVien.MaSinhVien && (t.TrangThai == null || t.TrangThai == "Hoạt động"))
                        .OrderBy(t => t.MaClbNavigation.TenClb)
                        .ToListAsync(),
                Activities = await _context.HoatDongClbs
                    .Include(h => h.MaClbNavigation)
                    .OrderByDescending(h => h.ThoiGian ?? h.NgayDang)
                    .Take(10)
                    .ToListAsync(),
                Documents = await _context.TaiLieuClbs
                    .Include(t => t.MaClbNavigation)
                    .Include(t => t.NguoiDangNavigation)
                    .OrderByDescending(t => t.NgayDang)
                    .Take(10)
                    .ToListAsync(),
                Elections = elections,
                MemberCounts = clubs.ToDictionary(c => c.MaClb, c => c.ThanhVienClbs.Count(t => t.TrangThai == null || t.TrangThai == "Hoạt động")),
                CandidateVoteCounts = elections
                    .SelectMany(d => d.UngVienPhoChuNhiems)
                    .ToDictionary(u => u.MaUngVien, u => elections
                        .SelectMany(e => e.PhieuBauPhoChuNhiems)
                        .Count(p => p.MaUngVien == u.MaUngVien)),
                MyVotesByElection = sinhVien == null
                    ? []
                    : await _context.PhieuBauPhoChuNhiems
                        .Where(p => p.MaSinhVienBau == sinhVien.MaSinhVien)
                        .ToDictionaryAsync(p => p.MaDot, p => p.MaUngVien)
            };

            return View(model);
        }

        private static Hoc11MentorCardViewModel BuildMentorCard(NguoiHuongDan mentor, int? selectedFieldId, string? keyword)
        {
            var specialties = mentor.ChuyenMonNguoiHuongDans
                .OrderByDescending(c => c.MaLinhVuc == selectedFieldId)
                .ThenByDescending(c => c.MucDoThanhThao ?? 0)
                .Select(c => c.MaLinhVucNavigation.TenLinhVuc)
                .Distinct()
                .Take(3)
                .ToList();

            var description = mentor.ChuyenMonNguoiHuongDans
                .OrderByDescending(c => c.MaLinhVuc == selectedFieldId)
                .ThenByDescending(c => c.MucDoThanhThao ?? 0)
                .Select(c => c.MoTaKinhNghiem)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                ?? "Mentor đã được duyệt và sẵn sàng hỗ trợ học 1-1.";

            var firstSlot = mentor.MaTaiKhoanNavigation.LichRanhs
                .OrderBy(l => l.Thu)
                .ThenBy(l => l.GioBatDau)
                .FirstOrDefault();

            var completedSessions = mentor.GhepNoiHocTaps
                .SelectMany(g => g.LichHocs)
                .Count(l => l.TrangThai == "Đã hoàn thành" || l.BaoCaoBuoiHoc != null);

            if (completedSessions == 0)
            {
                completedSessions = mentor.GhepNoiHocTaps.Count;
            }

            return new Hoc11MentorCardViewModel
            {
                MaHuongDan = mentor.MaHuongDan,
                HoTen = mentor.MaTaiKhoanNavigation.HoTen,
                AnhDaiDien = mentor.MaTaiKhoanNavigation.AnhDaiDien,
                LoaiNguoiHuongDan = mentor.LoaiNguoiHuongDan,
                DiemDanhGia = mentor.DiemDanhGia ?? 0,
                SoLuotDanhGia = mentor.SoLuotDanhGia ?? 0,
                DiemUyTin = mentor.DiemUyTin ?? 0,
                SoBuoiHoTro = completedSessions,
                TyLePhuHop = CalculateFitPercent(mentor, selectedFieldId, keyword),
                TrangThaiLichRanh = firstSlot == null
                    ? "Chưa cập nhật lịch rảnh"
                    : $"{FormatDay(firstSlot.Thu)} {firstSlot.GioBatDau:HH\\:mm}-{firstSlot.GioKetThuc:HH\\:mm}",
                MoTaKinhNghiem = description,
                ChuyenMon = specialties
            };
        }

        private static int CalculateFitPercent(NguoiHuongDan mentor, int? selectedFieldId, string? keyword)
        {
            var score = 68m;
            score += (mentor.DiemDanhGia ?? 0) * 4;
            score += Math.Min(mentor.DiemUyTin ?? 0, 10);

            if (selectedFieldId.HasValue && mentor.ChuyenMonNguoiHuongDans.Any(c => c.MaLinhVuc == selectedFieldId.Value))
            {
                score += 8;
            }

            if (!string.IsNullOrWhiteSpace(keyword)
                && mentor.ChuyenMonNguoiHuongDans.Any(c => c.MaLinhVucNavigation.TenLinhVuc.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                score += 5;
            }

            if (mentor.MaTaiKhoanNavigation.LichRanhs.Any())
            {
                score += 4;
            }

            return Math.Clamp((int)Math.Round(score), 72, 99);
        }

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

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

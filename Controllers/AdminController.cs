using Microsoft.AspNetCore.Mvc;
using StudyConnect.Data;
using StudyConnect.ViewModels;

namespace StudyConnect.Controllers
{
    public class AdminController : RoleProtectedController
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var guard = RequireRoles(AccountRoles.QuanTri);
            if (guard != null) return guard;

            ViewBag.TongNguoiDung = _context.TaiKhoans.Count();
            ViewBag.SinhVien = _context.SinhViens.Count();
            ViewBag.Mentor = _context.NguoiHuongDans.Count();
            ViewBag.CLB = _context.CauLacBos.Count();
            ViewBag.YeuCau = _context.YeuCauHoTroHocTaps.Count();
            ViewBag.BuoiHoc = _context.LichHocs.Count();

            return View();
        }
    }
}

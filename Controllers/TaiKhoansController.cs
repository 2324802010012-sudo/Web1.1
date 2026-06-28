using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.Models;
using StudyConnect.Services;
using StudyConnect.ViewModels;

public class TaiKhoansController : Controller
{
    private const string SessionUserId = "UserId";
    private const string SessionUserName = "UserName";
    private const string SessionUserEmail = "UserEmail";
    private const string SessionUserRole = "UserRole";

    private readonly AppDbContext _context;

    public TaiKhoansController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var guard = RequireRole(AccountRoles.QuanTri);
        if (guard != null) return guard;

        var taiKhoans = await _context.TaiKhoans
            .OrderBy(t => t.VaiTro)
            .ThenBy(t => t.HoTen)
            .ToListAsync();

        return View(taiKhoans);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(m => m.MaTaiKhoan == id);
        if (taiKhoan == null) return NotFound();

        if (!IsAdmin() && CurrentUserId() != taiKhoan.MaTaiKhoan)
        {
            return RedirectToAction(nameof(Denied));
        }

        return View(taiKhoan);
    }

    public IActionResult Create()
    {
        var isAdminCreate = IsAdmin();
        ViewBag.IsAdminCreate = isAdminCreate;
        ViewBag.RoleOptions = RoleOptions(allowAdmin: isAdminCreate);

        return View(new RegisterViewModel
        {
            VaiTro = AccountRoles.SinhVien
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RegisterViewModel model)
    {
        var allowAdmin = IsAdmin();
        ViewBag.IsAdminCreate = allowAdmin;
        ViewBag.RoleOptions = RoleOptions(allowAdmin);

        if (!allowAdmin)
        {
            model.VaiTro = AccountRoles.SinhVien;
        }

        if (!allowAdmin && model.VaiTro != AccountRoles.SinhVien)
        {
            ModelState.AddModelError(nameof(model.VaiTro), "Đăng ký công khai chỉ dành cho vai trò Sinh viên. Vai trò khác do quản trị viên cấp hoặc được duyệt theo quy trình.");
        }

        if (!AccountRoles.IsValid(model.VaiTro))
        {
            ModelState.AddModelError(nameof(model.VaiTro), "Vai trò không hợp lệ.");
        }

        var email = NormalizeEmail(model.Email);
        if (await _context.TaiKhoans.AnyAsync(t => t.Email.ToLower() == email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email này đã được sử dụng.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var taiKhoan = new TaiKhoan
        {
            HoTen = model.HoTen.Trim(),
            Email = email,
            MatKhau = PasswordService.Hash(model.MatKhau),
            VaiTro = model.VaiTro,
            SoDienThoai = string.IsNullOrWhiteSpace(model.SoDienThoai) ? null : model.SoDienThoai.Trim(),
            TrangThai = "Hoạt động",
            NgayTao = DateTime.Now
        };

        _context.TaiKhoans.Add(taiKhoan);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = allowAdmin
            ? "Đã tạo tài khoản mới."
            : "Đăng ký thành công. Bạn có thể đăng nhập ngay.";

        return allowAdmin ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Login));
    }

    public IActionResult Login(string? returnUrl = null)
    {
        if (IsLoggedIn())
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToRoleDashboard(CurrentUserRole());
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var loginId = model.Email.Trim();
        var normalizedLoginId = NormalizeEmail(loginId);
        var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(t =>
            t.Email.ToLower() == normalizedLoginId || t.SoDienThoai == loginId);
        if (taiKhoan == null || !PasswordService.Verify(model.MatKhau, taiKhoan.MatKhau))
        {
            ModelState.AddModelError(string.Empty, "Email, số điện thoại hoặc mật khẩu không đúng.");
            return View(model);
        }

        if (!string.Equals(taiKhoan.TrangThai, "Hoạt động", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Tài khoản đang bị khóa hoặc chưa được kích hoạt.");
            return View(model);
        }

        if (PasswordService.NeedsRehash(taiKhoan.MatKhau))
        {
            taiKhoan.MatKhau = PasswordService.Hash(model.MatKhau);
            await _context.SaveChangesAsync();
        }

        SignIn(taiKhoan);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToRoleDashboard(taiKhoan.VaiTro);
    }
        [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        TempData["SuccessMessage"] = "Bạn đã đăng xuất.";
        return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> Profile()
    {
        var guard = RequireLogin();
        if (guard != null) return guard;

        var id = CurrentUserId();
        var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.MaTaiKhoan == id);
        if (taiKhoan == null)
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        return View(taiKhoan);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        var guard = RequireRole(AccountRoles.QuanTri);
        if (guard != null) return guard;
        if (id == null) return NotFound();

        var taiKhoan = await _context.TaiKhoans.FindAsync(id);
        if (taiKhoan == null) return NotFound();

        ViewBag.RoleOptions = RoleOptions(allowAdmin: true);
        ViewBag.StatusOptions = StatusOptions();
        return View(ToEditViewModel(taiKhoan));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TaiKhoanEditViewModel model)
    {
        var guard = RequireRole(AccountRoles.QuanTri);
        if (guard != null) return guard;
        if (id != model.MaTaiKhoan) return NotFound();

        ViewBag.RoleOptions = RoleOptions(allowAdmin: true);
        ViewBag.StatusOptions = StatusOptions();

        if (!AccountRoles.IsValid(model.VaiTro))
        {
            ModelState.AddModelError(nameof(model.VaiTro), "Vai trò không hợp lệ.");
        }

        var email = NormalizeEmail(model.Email);
        if (await _context.TaiKhoans.AnyAsync(t => t.MaTaiKhoan != id && t.Email.ToLower() == email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email này đã được sử dụng.");
        }

        if (!ModelState.IsValid) return View(model);

        var taiKhoan = await _context.TaiKhoans.FindAsync(id);
        if (taiKhoan == null) return NotFound();

        taiKhoan.HoTen = model.HoTen.Trim();
        taiKhoan.Email = email;
        taiKhoan.SoDienThoai = string.IsNullOrWhiteSpace(model.SoDienThoai) ? null : model.SoDienThoai.Trim();
        taiKhoan.VaiTro = model.VaiTro;
        taiKhoan.TrangThai = model.TrangThai;

        if (!string.IsNullOrWhiteSpace(model.MatKhauMoi))
        {
            taiKhoan.MatKhau = PasswordService.Hash(model.MatKhauMoi);
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã cập nhật tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        var guard = RequireRole(AccountRoles.QuanTri);
        if (guard != null) return guard;
        if (id == null) return NotFound();

        var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(m => m.MaTaiKhoan == id);
        if (taiKhoan == null) return NotFound();

        return View(taiKhoan);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var guard = RequireRole(AccountRoles.QuanTri);
        if (guard != null) return guard;

        var taiKhoan = await _context.TaiKhoans.FindAsync(id);
        if (taiKhoan != null)
        {
            if (taiKhoan.MaTaiKhoan == CurrentUserId())
            {
                TempData["ErrorMessage"] = "Bạn không thể xóa chính tài khoản đang đăng nhập.";
                return RedirectToAction(nameof(Index));
            }

            _context.TaiKhoans.Remove(taiKhoan);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa tài khoản.";
        }

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Denied()
    {
        return View();
    }

    private void SignIn(TaiKhoan taiKhoan)
    {
        HttpContext.Session.SetInt32(SessionUserId, taiKhoan.MaTaiKhoan);
        HttpContext.Session.SetString(SessionUserName, taiKhoan.HoTen);
        HttpContext.Session.SetString(SessionUserEmail, taiKhoan.Email);
        HttpContext.Session.SetString(SessionUserRole, taiKhoan.VaiTro);
    }

    private IActionResult RedirectToRoleDashboard(string? role)
    {
        return role switch
        {
            AccountRoles.QuanTri => RedirectToAction("Index", "Admin"),
            AccountRoles.SinhVien => RedirectToAction("Index", "SinhVien"),
            AccountRoles.Mentor => RedirectToAction("Index", "Mentor"),
            AccountRoles.ChuNhiemClb => RedirectToAction("Index", "ChuNhiemCLB"),
            AccountRoles.CoVan => RedirectToAction("Index", "CoVan"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    private bool IsLoggedIn() => HttpContext.Session.GetInt32(SessionUserId).HasValue;

    private bool IsAdmin() => string.Equals(CurrentUserRole(), AccountRoles.QuanTri, StringComparison.OrdinalIgnoreCase);

    private int? CurrentUserId() => HttpContext.Session.GetInt32(SessionUserId);

    private string? CurrentUserRole() => HttpContext.Session.GetString(SessionUserRole);

    private IActionResult? RequireLogin()
    {
        if (IsLoggedIn()) return null;
        return RedirectToAction(nameof(Login), new { returnUrl = HttpContext.Request.Path + HttpContext.Request.QueryString });
    }

    private IActionResult? RequireRole(params string[] roles)
    {
        var loginGuard = RequireLogin();
        if (loginGuard != null) return loginGuard;

        var currentRole = CurrentUserRole();
        if (currentRole != null && roles.Any(role => string.Equals(role, currentRole, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return RedirectToAction(nameof(Denied));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static TaiKhoanEditViewModel ToEditViewModel(TaiKhoan taiKhoan)
    {
        return new TaiKhoanEditViewModel
        {
            MaTaiKhoan = taiKhoan.MaTaiKhoan,
            HoTen = taiKhoan.HoTen,
            Email = taiKhoan.Email,
            SoDienThoai = taiKhoan.SoDienThoai,
            VaiTro = taiKhoan.VaiTro,
            TrangThai = taiKhoan.TrangThai ?? "Hoạt động"
        };
    }

    private static IEnumerable<SelectListItem> RoleOptions(bool allowAdmin)
    {
        return AccountRoles.All
            .Where(role => allowAdmin || role == AccountRoles.SinhVien)
            .Select(role => new SelectListItem(AccountRoles.DisplayName(role), role));
    }

    private static IEnumerable<SelectListItem> StatusOptions()
    {
        return new[] { "Hoạt động", "Bị khóa" }.Select(status => new SelectListItem(status, status));
    }
}

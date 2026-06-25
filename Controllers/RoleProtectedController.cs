using Microsoft.AspNetCore.Mvc;

namespace StudyConnect.Controllers;

public abstract class RoleProtectedController : Controller
{
    protected int? CurrentUserId => HttpContext.Session.GetInt32("UserId");

    protected string? CurrentUserRole => HttpContext.Session.GetString("UserRole");

    protected string CurrentUserName => HttpContext.Session.GetString("UserName") ?? "StudyConnect";

    protected IActionResult? RequireRoles(params string[] roles)
    {
        if (!CurrentUserId.HasValue)
        {
            return RedirectToAction("Login", "TaiKhoans", new
            {
                returnUrl = HttpContext.Request.Path + HttpContext.Request.QueryString
            });
        }

        if (CurrentUserRole != null &&
            roles.Any(role => string.Equals(role, CurrentUserRole, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return RedirectToAction("Denied", "TaiKhoans");
    }
}

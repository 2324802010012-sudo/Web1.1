using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;

namespace StudyConnect.Controllers;

public class ThongBaosController : Controller
{
    private readonly AppDbContext _context;

    public ThongBaosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(string? returnUrl = null)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "TaiKhoans", new { returnUrl });
        }

        var unread = await _context.ThongBaos
            .Where(t => t.MaTaiKhoan == userId.Value && t.DaDoc != true)
            .ToListAsync();

        foreach (var item in unread)
        {
            item.DaDoc = true;
        }

        await _context.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}

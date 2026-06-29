using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Data;
using StudyConnect.ViewModels;

namespace StudyConnect.ViewComponents;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly AppDbContext _context;

    public NotificationBellViewComponent(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return Content(string.Empty);
        }

        var notifications = await _context.ThongBaos
            .Where(t => t.MaTaiKhoan == userId.Value)
            .OrderByDescending(t => t.NgayTao)
            .ThenByDescending(t => t.MaThongBao)
            .Take(8)
            .ToListAsync();

        var unreadCount = await _context.ThongBaos
            .CountAsync(t => t.MaTaiKhoan == userId.Value && t.DaDoc != true);

        var model = new NotificationBellViewModel
        {
            UnreadCount = unreadCount,
            ReturnUrl = HttpContext.Request.Path + HttpContext.Request.QueryString,
            Items = notifications.Select(item => new NotificationBellItemViewModel
            {
                Title = item.TieuDe,
                Message = item.NoiDung ?? string.Empty,
                TimeText = FormatTime(item.NgayTao),
                IsUnread = item.DaDoc != true,
                Tone = ToneFor(item.LoaiThongBao)
            }).ToList()
        };

        return View(model);
    }

    private static string ToneFor(string? type)
    {
        return type switch
        {
            "XacNhanLichHoc" or "LichHoc" => "blue",
            "VangHoc" => "orange",
            "DangKyMentor" or "QuanLyMentor" => "green",
            "BauCuCLB" or "ThanhVienCLB" => "orange",
            _ => "blue"
        };
    }

    private static string FormatTime(DateTime? value)
    {
        if (value == null) return "";
        var date = value.Value;
        var span = DateTime.Now - date;
        if (span.TotalMinutes < 1) return "Vừa xong";
        if (span.TotalHours < 1) return $"{Math.Max(1, (int)span.TotalMinutes)} phút trước";
        if (span.TotalDays < 1) return $"{Math.Max(1, (int)span.TotalHours)} giờ trước";
        if (span.TotalDays < 7) return $"{Math.Max(1, (int)span.TotalDays)} ngày trước";
        return date.ToString("dd/MM/yyyy HH:mm");
    }
}

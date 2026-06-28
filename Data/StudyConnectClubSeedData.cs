using Microsoft.EntityFrameworkCore;
using StudyConnect.Models;

namespace StudyConnect.Data;

public static class StudyConnectClubSeedData
{
    public static async Task EnsureClubWorkflowSeededAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var aiClub = await EnsureClubAsync(context, "AI Club", "Cộng đồng nghiên cứu AI, Machine Learning và ứng dụng trong học tập.");
        var codeClub = await EnsureClubAsync(context, "Code Warriors", "CLB lập trình, thuật toán, ASP.NET Core và dự án thực tế.");
        var securityClub = await EnsureClubAsync(context, "Cyber Security Club", "CLB an toàn thông tin, bảo mật web và phòng chống tấn công.");
        var dataClub = await EnsureClubAsync(context, "Data Science Club", "CLB phân tích dữ liệu, SQL, trực quan hóa và Machine Learning.");
        var roboticsClub = await EnsureClubAsync(context, "Robotics & IoT Club", "CLB nghiên cứu robot, cảm biến, IoT và hệ thống nhúng.");
        var mobileClub = await EnsureClubAsync(context, "Mobile App Club", "CLB phát triển ứng dụng di động Android, iOS và Flutter.");
        var uxClub = await EnsureClubAsync(context, "UI/UX Design Club", "CLB thiết kế giao diện, trải nghiệm người dùng và prototype sản phẩm.");
        var cloudClub = await EnsureClubAsync(context, "Cloud Computing Club", "CLB điện toán đám mây, DevOps, triển khai web và hạ tầng ứng dụng.");

        await NormalizeMembershipsAsync(context, aiClub, codeClub, securityClub, dataClub, roboticsClub, mobileClub, uxClub, cloudClub);
        await EnsureActivitiesAsync(context, aiClub, codeClub, securityClub, dataClub, roboticsClub, mobileClub, uxClub, cloudClub);
        await EnsureDocumentsAsync(context, aiClub, codeClub, securityClub, dataClub, roboticsClub, mobileClub, uxClub, cloudClub);
        await EnsureElectionAsync(context, aiClub, "Bầu Phó chủ nhiệm AI Club nhiệm kỳ 2026");
        await EnsureElectionAsync(context, codeClub, "Bầu Phó chủ nhiệm Code Warriors nhiệm kỳ 2026");

        await context.SaveChangesAsync();
    }

    private static async Task<CauLacBo> EnsureClubAsync(AppDbContext context, string name, string description)
    {
        var club = await context.CauLacBos.FirstOrDefaultAsync(c => c.TenClb == name);
        if (club == null)
        {
            club = new CauLacBo
            {
                TenClb = name,
                NgayThanhLap = new DateOnly(2024, 9, 1)
            };
            context.CauLacBos.Add(club);
            await context.SaveChangesAsync();
        }

        club.MoTa = description;
        club.TrangThai = "Hoạt động";
        return club;
    }

    private static async Task NormalizeMembershipsAsync(AppDbContext context, params CauLacBo[] clubs)
    {
        var clubIds = clubs.Select(c => c.MaClb).ToArray();

        foreach (var membership in await context.ThanhVienClbs
            .Where(t => clubIds.Contains(t.MaClb))
            .ToListAsync())
        {
            membership.TrangThai = "Hoạt động";
            membership.VaiTroClb = string.IsNullOrWhiteSpace(membership.VaiTroClb) ? "Thành viên" : membership.VaiTroClb;
            membership.NgayThamGia ??= new DateOnly(2024, 10, 1);
        }

        var demoStudent = await context.SinhViens
            .Include(s => s.MaTaiKhoanNavigation)
            .FirstOrDefaultAsync(s => s.MaTaiKhoanNavigation.Email == "student.demo@studyconnect.local");

        if (demoStudent != null)
        {
            await EnsureMembershipAsync(context, demoStudent, clubs[0], "Thành viên");
            await EnsureMembershipAsync(context, demoStudent, clubs[1], "Thành viên");
        }
    }

    private static async Task EnsureMembershipAsync(AppDbContext context, SinhVien student, CauLacBo club, string role)
    {
        var membership = await context.ThanhVienClbs
            .FirstOrDefaultAsync(t => t.MaSinhVien == student.MaSinhVien && t.MaClb == club.MaClb);

        if (membership == null)
        {
            context.ThanhVienClbs.Add(new ThanhVienClb
            {
                MaSinhVien = student.MaSinhVien,
                MaClb = club.MaClb,
                VaiTroClb = role,
                NgayThamGia = new DateOnly(2024, 10, 1),
                TrangThai = "Hoạt động"
            });
        }
        else
        {
            membership.VaiTroClb = role;
            membership.TrangThai = "Hoạt động";
        }
    }

    private static async Task EnsureActivitiesAsync(
        AppDbContext context,
        CauLacBo aiClub,
        CauLacBo codeClub,
        CauLacBo securityClub,
        CauLacBo dataClub,
        CauLacBo roboticsClub,
        CauLacBo mobileClub,
        CauLacBo uxClub,
        CauLacBo cloudClub)
    {
        var author = await context.TaiKhoans.FirstOrDefaultAsync(t => t.VaiTro == "CoVan")
            ?? await context.TaiKhoans.FirstAsync();

        var activities = new[]
        {
            (Club: aiClub, Title: "Workshop Build Chatbot với RAG", Content: "Thực hành xây dựng chatbot học tập dựa trên tài liệu CLB.", Time: DateTime.Today.AddDays(3).AddHours(19), Place: "Online Google Meet"),
            (Club: codeClub, Title: "Code Challenge: Algorithm Week", Content: "Luyện giải thuật, cấu trúc dữ liệu và chia sẻ lời giải.", Time: DateTime.Today.AddDays(5).AddHours(19), Place: "Phòng Lab A2"),
            (Club: securityClub, Title: "Seminar An toàn thông tin cơ bản", Content: "OWASP Top 10 và các lỗi bảo mật thường gặp trong đồ án web.", Time: DateTime.Today.AddDays(7).AddHours(18), Place: "Hội trường B"),
            (Club: dataClub, Title: "Ngày chia sẻ tài liệu học thuật", Content: "Tổng hợp tài liệu SQL, Pandas và trực quan hóa dữ liệu.", Time: DateTime.Today.AddDays(9).AddHours(18), Place: "Thư viện số"),
            (Club: roboticsClub, Title: "Demo cảm biến IoT và Arduino", Content: "Lắp mạch cảm biến, đọc dữ liệu và gửi về dashboard web.", Time: DateTime.Today.AddDays(11).AddHours(18), Place: "Lab IoT C1"),
            (Club: mobileClub, Title: "Flutter UI Sprint", Content: "Thiết kế màn hình mobile, state management và điều hướng cơ bản.", Time: DateTime.Today.AddDays(13).AddHours(19), Place: "Phòng Lab Mobile"),
            (Club: uxClub, Title: "Design Critique Night", Content: "Góp ý wireframe, prototype và cải thiện trải nghiệm người dùng.", Time: DateTime.Today.AddDays(15).AddHours(18), Place: "Studio UX"),
            (Club: cloudClub, Title: "Deploy ASP.NET Core lên Cloud", Content: "Thực hành publish web app, cấu hình biến môi trường và log.", Time: DateTime.Today.AddDays(17).AddHours(19), Place: "Online")
        };

        foreach (var activity in activities)
        {
            if (await context.HoatDongClbs.AnyAsync(h => h.MaClb == activity.Club.MaClb && h.TieuDe == activity.Title)) continue;

            context.HoatDongClbs.Add(new HoatDongClb
            {
                MaClb = activity.Club.MaClb,
                TieuDe = activity.Title,
                NoiDung = activity.Content,
                ThoiGian = activity.Time,
                DiaDiem = activity.Place,
                NguoiDang = author.MaTaiKhoan,
                NgayDang = DateTime.Now
            });
        }
    }

    private static async Task EnsureDocumentsAsync(
        AppDbContext context,
        CauLacBo aiClub,
        CauLacBo codeClub,
        CauLacBo securityClub,
        CauLacBo dataClub,
        CauLacBo roboticsClub,
        CauLacBo mobileClub,
        CauLacBo uxClub,
        CauLacBo cloudClub)
    {
        var author = await context.TaiKhoans.FirstOrDefaultAsync(t => t.VaiTro == "CoVan")
            ?? await context.TaiKhoans.FirstAsync();

        var documents = new[]
        {
            (Club: aiClub, Title: "Tài liệu nhập môn Machine Learning", Description: "Tổng quan quy trình học ML, Python và đánh giá mô hình."),
            (Club: codeClub, Title: "Cấu trúc dữ liệu và giải thuật - Tóm tắt", Description: "Danh sách, stack, queue, tree và các thuật toán thường dùng."),
            (Club: securityClub, Title: "OWASP Top 10 cho đồ án Web", Description: "Checklist bảo mật cơ bản trước khi nộp đồ án."),
            (Club: dataClub, Title: "SQL nâng cao và bài tập thực hành", Description: "Join, group by, index và tối ưu truy vấn SQL Server."),
            (Club: roboticsClub, Title: "Arduino và cảm biến cơ bản", Description: "Sơ đồ mạch, đọc tín hiệu cảm biến và gửi dữ liệu IoT."),
            (Club: mobileClub, Title: "Flutter layout cookbook", Description: "Tổng hợp layout, widget phổ biến và điều hướng trong Flutter."),
            (Club: uxClub, Title: "UI/UX heuristic checklist", Description: "Checklist đánh giá giao diện, khả dụng và luồng người dùng."),
            (Club: cloudClub, Title: "Cloud deployment checklist", Description: "Các bước chuẩn bị publish, cấu hình connection string và logging.")
        };

        foreach (var document in documents)
        {
            if (await context.TaiLieuClbs.AnyAsync(t => t.MaClb == document.Club.MaClb && t.TieuDe == document.Title)) continue;

            context.TaiLieuClbs.Add(new TaiLieuClb
            {
                MaClb = document.Club.MaClb,
                TieuDe = document.Title,
                MoTa = document.Description,
                TepDinhKem = "#",
                NguoiDang = author.MaTaiKhoan,
                NgayDang = DateTime.Now.AddDays(-2)
            });
        }
    }

    private static async Task EnsureElectionAsync(AppDbContext context, CauLacBo club, string name)
    {
        var election = await context.DotDeCuPhoChuNhiems
            .FirstOrDefaultAsync(d => d.MaClb == club.MaClb && d.TenDot == name);

        if (election == null)
        {
            election = new DotDeCuPhoChuNhiem
            {
                MaClb = club.MaClb,
                TenDot = name,
                ThoiGianBatDau = DateTime.Now.AddDays(-1),
                ThoiGianKetThuc = DateTime.Now.AddDays(14),
                TrangThai = "Đang đề cử"
            };
            context.DotDeCuPhoChuNhiems.Add(election);
            await context.SaveChangesAsync();
        }
        else
        {
            election.ThoiGianBatDau = DateTime.Now.AddDays(-1);
            election.ThoiGianKetThuc = DateTime.Now.AddDays(14);
            election.TrangThai = "Đang đề cử";
        }

        var members = await context.ThanhVienClbs
            .Include(t => t.MaSinhVienNavigation)
                .ThenInclude(s => s.MaTaiKhoanNavigation)
            .Where(t => t.MaClb == club.MaClb && (t.TrangThai == null || t.TrangThai != "Đã rời"))
            .OrderBy(t => t.MaThanhVien)
            .Take(3)
            .ToListAsync();

        foreach (var member in members)
        {
            await EnsureCandidateAsync(context, election, member.MaSinhVien, member.MaSinhVien, $"Muốn đóng góp cho kế hoạch học thuật, tài liệu và hoạt động của {club.TenClb}.");
        }

        var candidates = await context.UngVienPhoChuNhiems
            .Where(u => u.MaDot == election.MaDot)
            .OrderBy(u => u.MaUngVien)
            .ToListAsync();

        for (var i = 0; i < members.Count && candidates.Count > 0; i++)
        {
            var candidate = candidates[i % candidates.Count];
            if (await context.PhieuBauPhoChuNhiems.AnyAsync(p => p.MaDot == election.MaDot && p.MaSinhVienBau == members[i].MaSinhVien)) continue;

            context.PhieuBauPhoChuNhiems.Add(new PhieuBauPhoChuNhiem
            {
                MaDot = election.MaDot,
                MaUngVien = candidate.MaUngVien,
                MaSinhVienBau = members[i].MaSinhVien,
                ThoiGianBau = DateTime.Now.AddHours(-i)
            });
        }
    }

    private static async Task EnsureCandidateAsync(AppDbContext context, DotDeCuPhoChuNhiem election, int studentId, int nominatorId, string reason)
    {
        var candidate = await context.UngVienPhoChuNhiems
            .FirstOrDefaultAsync(u => u.MaDot == election.MaDot && u.MaSinhVien == studentId);

        if (candidate == null)
        {
            context.UngVienPhoChuNhiems.Add(new UngVienPhoChuNhiem
            {
                MaDot = election.MaDot,
                MaSinhVien = studentId,
                NguoiDeCu = nominatorId,
                LyDoDeCu = reason,
                TrangThai = "Hợp lệ"
            });
        }
        else
        {
            candidate.LyDoDeCu = reason;
            candidate.TrangThai = "Hợp lệ";
        }
    }
}

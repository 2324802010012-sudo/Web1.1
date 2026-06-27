using Microsoft.EntityFrameworkCore;
using StudyConnect.Models;
using StudyConnect.Services;
using StudyConnect.ViewModels;

namespace StudyConnect.Data;

public static class StudyConnectSeedData
{
    private const string SeedPassword = "StudyConnect@123";

    public static async Task EnsureSeededAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await SeedLearningFieldsAsync(context);
        await SeedClubsAsync(context);
        var coVanAccount = await SeedAdvisorAsync(context);
        await SeedMentorsAsync(context, coVanAccount);
        await SeedLearningHistoryAsync(context);
    }

    private static async Task SeedLearningFieldsAsync(AppDbContext context)
    {
        var fields = new (string Name, string Description)[]
        {
            ("Lập trình", "Ngôn ngữ lập trình, OOP và tư duy phát triển phần mềm."),
            ("Cơ sở dữ liệu", "SQL, thiết kế CSDL, tối ưu truy vấn và mô hình dữ liệu."),
            ("AI / Machine Learning", "Machine Learning, Python, xử lý dữ liệu và mô hình AI."),
            ("An toàn thông tin", "Bảo mật web, mạng máy tính và phòng chống tấn công."),
            ("Thiết kế UI/UX", "Thiết kế giao diện, trải nghiệm người dùng và prototype."),
            ("Cấu trúc dữ liệu & Giải thuật", "Cấu trúc dữ liệu, thuật toán và luyện giải bài tập."),
            ("ASP.NET Core", "MVC, Razor, Entity Framework Core và xây dựng web app."),
            ("Phân tích dữ liệu", "Pandas, Excel, trực quan hóa và xử lý dữ liệu thực tế.")
        };

        foreach (var field in fields)
        {
            if (await context.LinhVucHocTaps.AnyAsync(l => l.TenLinhVuc == field.Name)) continue;

            context.LinhVucHocTaps.Add(new LinhVucHocTap
            {
                TenLinhVuc = field.Name,
                MoTa = field.Description,
                TrangThai = "Hoạt động"
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedClubsAsync(AppDbContext context)
    {
        var clubs = new (string Name, string Description)[]
        {
            ("AI Club", "Cộng đồng nghiên cứu và ứng dụng AI trong học tập."),
            ("Code Warriors", "CLB lập trình, thuật toán và dự án thực tế."),
            ("Cyber Security Club", "CLB an toàn thông tin và bảo mật ứng dụng."),
            ("Data Science Club", "CLB phân tích dữ liệu, trực quan hóa và machine learning."),
            ("Robotics & IoT Club", "CLB nghiên cứu robot, cảm biến, IoT và hệ thống nhúng."),
            ("Mobile App Club", "CLB phát triển ứng dụng di động Android, iOS và Flutter."),
            ("UI/UX Design Club", "CLB thiết kế giao diện, trải nghiệm người dùng và prototype sản phẩm."),
            ("Cloud Computing Club", "CLB điện toán đám mây, DevOps, triển khai web và hạ tầng ứng dụng.")
        };

        foreach (var club in clubs)
        {
            if (await context.CauLacBos.AnyAsync(c => c.TenClb == club.Name)) continue;

            context.CauLacBos.Add(new CauLacBo
            {
                TenClb = club.Name,
                MoTa = club.Description,
                NgayThanhLap = new DateOnly(2024, 9, 1),
                TrangThai = "Hoạt động"
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<TaiKhoan> SeedAdvisorAsync(AppDbContext context)
    {
        var account = await EnsureAccountAsync(
            context,
            "covan@studyconnect.local",
            "TS. Nguyễn Minh Đức",
            AccountRoles.CoVan,
            "0901000001");

        if (!await context.GiangViens.AnyAsync(g => g.MaTaiKhoan == account.MaTaiKhoan))
        {
            context.GiangViens.Add(new GiangVien
            {
                MaTaiKhoan = account.MaTaiKhoan,
                MaGv = "GV-SEED-001",
                HocVi = "Tiến sĩ",
                BoMon = "Công nghệ phần mềm"
            });

            await context.SaveChangesAsync();
        }

        return account;
    }

    private static async Task SeedMentorsAsync(AppDbContext context, TaiKhoan coVanAccount)
    {
        var mentors = new[]
        {
            new MentorSeed("mentor01@studyconnect.local", "Phạm Quốc Bảo", "SVMENTOR001", "Kỹ thuật phần mềm", 3.72m, "Python, SQL, Machine Learning", "Muốn hỗ trợ các bạn nắm chắc Python và AI cơ bản.", 9.88m, 4.9m, 124, "AI Club", new[] { "AI / Machine Learning", "Phân tích dữ liệu", "Cơ sở dữ liệu" }),
            new MentorSeed("mentor02@studyconnect.local", "Trần Thị Mai", "SVMENTOR002", "Khoa học máy tính", 3.64m, "C++, OOP, thuật toán", "Hỗ trợ tư duy giải thuật và cấu trúc dữ liệu.", 9.42m, 4.8m, 96, "Code Warriors", new[] { "Cấu trúc dữ liệu & Giải thuật", "Lập trình" }),
            new MentorSeed("mentor03@studyconnect.local", "Vũ Hoàng Nam", "SVMENTOR003", "Công nghệ phần mềm", 3.55m, "Java, Spring Boot, SQL", "Đồng hành học backend và thiết kế API.", 9.23m, 4.8m, 87, "Code Warriors", new[] { "Lập trình", "Cơ sở dữ liệu" }),
            new MentorSeed("mentor04@studyconnect.local", "Lê Minh Châu", "SVMENTOR004", "Hệ thống thông tin", 3.48m, "Excel, SQL, Power BI, Pandas", "Hướng dẫn phân tích dữ liệu thực tế.", 8.95m, 4.7m, 72, "Data Science Club", new[] { "Phân tích dữ liệu", "Cơ sở dữ liệu" }),
            new MentorSeed("mentor05@studyconnect.local", "Nguyễn Văn Tùng", "SVMENTOR005", "Công nghệ phần mềm", 3.50m, "HTML, CSS, JavaScript, ASP.NET Core", "Hỗ trợ xây dựng web MVC và giao diện.", 8.76m, 4.8m, 68, "Code Warriors", new[] { "ASP.NET Core", "Lập trình", "Thiết kế UI/UX" }),
            new MentorSeed("mentor06@studyconnect.local", "Đỗ Quang Huy", "SVMENTOR006", "Trí tuệ nhân tạo", 3.82m, "Python, scikit-learn, xử lý dữ liệu", "Hỗ trợ mô hình ML và tiền xử lý dữ liệu.", 8.62m, 4.7m, 61, "AI Club", new[] { "AI / Machine Learning", "Phân tích dữ liệu" }),
            new MentorSeed("mentor07@studyconnect.local", "Trần Quốc Anh", "SVMENTOR007", "Công nghệ phần mềm", 3.41m, "C#, ASP.NET Core, Entity Framework", "Hỗ trợ đồ án web, database và backend.", 8.35m, 4.6m, 54, "Code Warriors", new[] { "ASP.NET Core", "Cơ sở dữ liệu" }),
            new MentorSeed("mentor08@studyconnect.local", "Phạm Thùy Linh", "SVMENTOR008", "Thiết kế số", 3.38m, "Figma, UI kit, usability", "Hỗ trợ cải thiện giao diện và trải nghiệm người dùng.", 8.12m, 4.6m, 49, null, new[] { "Thiết kế UI/UX" }),
            new MentorSeed("mentor09@studyconnect.local", "Nguyễn Hoài Nam", "SVMENTOR009", "An toàn thông tin", 3.66m, "Web security, OWASP, network", "Hướng dẫn bảo mật cơ bản và kiểm thử web.", 7.98m, 4.6m, 44, "Cyber Security Club", new[] { "An toàn thông tin", "Lập trình" }),
            new MentorSeed("mentor10@studyconnect.local", "Bùi Minh Đức", "SVMENTOR010", "Hệ thống thông tin", 3.44m, "SQL Server, thiết kế CSDL, tối ưu truy vấn", "Hỗ trợ mô hình dữ liệu và truy vấn SQL.", 7.85m, 4.5m, 39, "Data Science Club", new[] { "Cơ sở dữ liệu", "Phân tích dữ liệu" })
        };

        var rank = 1;
        foreach (var mentor in mentors)
        {
            var account = await EnsureAccountAsync(context, mentor.Email, mentor.Name, AccountRoles.Mentor, null);
            var student = await EnsureStudentProfileAsync(context, account, mentor);
            var guide = await EnsureMentorProfileAsync(context, account, mentor);

            await EnsureClubMembershipAsync(context, student, mentor.ClubName);
            await EnsureSpecialtiesAsync(context, guide, mentor.Fields);
            await EnsureAvailabilityAsync(context, account.MaTaiKhoan, rank);
            await EnsureMentorRankingAsync(context, guide, mentor, rank);
            await EnsureApprovedMentorApplicationAsync(context, student, mentor, coVanAccount);

            rank++;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedLearningHistoryAsync(AppDbContext context)
    {
        var learner = await EnsureAccountAsync(context, "student.demo@studyconnect.local", "Nguyễn Minh Anh", AccountRoles.SinhVien, "0902000001");
        var student = await EnsureStudentProfileAsync(context, learner, new MentorSeed(
            "student.demo@studyconnect.local",
            "Nguyễn Minh Anh",
            "SVDEMO001",
            "Công nghệ phần mềm",
            3.10m,
            "C#, SQL cơ bản, HTML/CSS",
            "Muốn học chắc ASP.NET Core và CSDL để hoàn thành đồ án.",
            0,
            0,
            0,
            null,
            Array.Empty<string>()));

        var field = await context.LinhVucHocTaps.FirstAsync(l => l.TenLinhVuc == "ASP.NET Core");
        var mentorIds = await context.NguoiHuongDans
            .Include(m => m.MaTaiKhoanNavigation)
            .Where(m => m.MaTaiKhoanNavigation.Email == "mentor05@studyconnect.local" || m.MaTaiKhoanNavigation.Email == "mentor07@studyconnect.local")
            .Select(m => m.MaHuongDan)
            .ToListAsync();

        foreach (var mentorId in mentorIds)
        {
            if (await context.GhepNoiHocTaps.AnyAsync(g => g.MaHuongDan == mentorId && g.MaYeuCauNavigation.MaSinhVien == student.MaSinhVien)) continue;

            var request = new YeuCauHoTroHocTap
            {
                MaSinhVien = student.MaSinhVien,
                MaLinhVuc = field.MaLinhVuc,
                MoTaVanDe = "Cần hỗ trợ xây dựng chức năng CRUD, phân quyền và Entity Framework Core.",
                MucTieu = "Hoàn thành đồ án Web 1-1 với ASP.NET Core MVC",
                MucDoCanHoTro = "Trung bình",
                TrangThai = "Đã ghép nối",
                NgayTao = DateTime.Now.AddDays(-14)
            };

            context.YeuCauHoTroHocTaps.Add(request);
            await context.SaveChangesAsync();

            var match = new GhepNoiHocTap
            {
                MaYeuCau = request.MaYeuCau,
                MaHuongDan = mentorId,
                DiemPhuHop = 92.5m,
                TrangThai = "Hoàn thành",
                NgayGhep = DateTime.Now.AddDays(-12)
            };

            context.GhepNoiHocTaps.Add(match);
            await context.SaveChangesAsync();

            var lesson = new LichHoc
            {
                MaGhepNoi = match.MaGhepNoi,
                NgayHoc = DateOnly.FromDateTime(DateTime.Today.AddDays(-7)),
                GioBatDau = new TimeOnly(19, 0),
                GioKetThuc = new TimeOnly(20, 30),
                HinhThuc = "Online",
                LinkOnline = "https://meet.studyconnect.local/demo",
                TrangThai = "Đã hoàn thành"
            };

            context.LichHocs.Add(lesson);
            await context.SaveChangesAsync();

            context.BaoCaoBuoiHocs.Add(new BaoCaoBuoiHoc
            {
                MaLichHoc = lesson.MaLichHoc,
                NoiDungDaHoc = "Ôn MVC, routing, DbContext và cách tổ chức controller/view.",
                BaiTap = "Hoàn thiện form tạo yêu cầu hỗ trợ và kiểm tra phân quyền.",
                MucDoTiepThu = "Tốt",
                NhanXet = "Sinh viên nắm được luồng xử lý và cần luyện thêm truy vấn LINQ.",
                NgayBaoCao = DateTime.Now.AddDays(-7)
            });

            context.LichSuHocTaps.Add(new LichSuHocTap
            {
                MaSinhVien = student.MaSinhVien,
                MaGhepNoi = match.MaGhepNoi,
                SoBuoiDaHoc = 3,
                TienDo = "Đang tiến bộ",
                KetQuaTongHop = "Đã hiểu luồng MVC cơ bản, cần luyện thêm CRUD và phân quyền.",
                NgayCapNhat = DateTime.Now.AddDays(-7)
            });

            context.DanhGiaHuongDans.Add(new DanhGiaHuongDan
            {
                MaHuongDan = mentorId,
                MaSinhVien = student.MaSinhVien,
                MaGhepNoi = match.MaGhepNoi,
                SoSao = 5,
                NhanXet = "Mentor hướng dẫn dễ hiểu, đưa ví dụ sát đồ án.",
                NgayDanhGia = DateTime.Now.AddDays(-6)
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task<TaiKhoan> EnsureAccountAsync(AppDbContext context, string email, string name, string role, string? phone)
    {
        var account = await context.TaiKhoans.FirstOrDefaultAsync(t => t.Email == email);
        if (account != null) return account;

        account = new TaiKhoan
        {
            HoTen = name,
            Email = email,
            MatKhau = PasswordService.Hash(SeedPassword),
            VaiTro = role,
            SoDienThoai = phone,
            TrangThai = "Hoạt động",
            NgayTao = DateTime.Now
        };

        context.TaiKhoans.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    private static async Task<SinhVien> EnsureStudentProfileAsync(AppDbContext context, TaiKhoan account, MentorSeed seed)
    {
        var student = await context.SinhViens.FirstOrDefaultAsync(s => s.MaTaiKhoan == account.MaTaiKhoan);
        if (student != null) return student;

        student = new SinhVien
        {
            MaTaiKhoan = account.MaTaiKhoan,
            Mssv = seed.StudentCode,
            ChuyenNganh = seed.Major,
            Lop = "D23-CNTT",
            Gpa = seed.Gpa,
            KyNang = seed.Skills,
            GioiThieu = seed.Goal
        };

        context.SinhViens.Add(student);
        await context.SaveChangesAsync();
        return student;
    }

    private static async Task<NguoiHuongDan> EnsureMentorProfileAsync(AppDbContext context, TaiKhoan account, MentorSeed seed)
    {
        var mentor = await context.NguoiHuongDans.FirstOrDefaultAsync(m => m.MaTaiKhoan == account.MaTaiKhoan);
        if (mentor != null) return mentor;

        mentor = new NguoiHuongDan
        {
            MaTaiKhoan = account.MaTaiKhoan,
            LoaiNguoiHuongDan = "Sinh viên mentor",
            DiemUyTin = seed.Reputation,
            DiemDanhGia = seed.Rating,
            SoLuotDanhGia = seed.RatingCount,
            TrangThai = "Hoạt động"
        };

        context.NguoiHuongDans.Add(mentor);
        await context.SaveChangesAsync();
        return mentor;
    }

    private static async Task EnsureClubMembershipAsync(AppDbContext context, SinhVien student, string? clubName)
    {
        if (clubName == null) return;

        var club = await context.CauLacBos.FirstOrDefaultAsync(c => c.TenClb == clubName);
        if (club == null) return;
        if (await context.ThanhVienClbs.AnyAsync(t => t.MaClb == club.MaClb && t.MaSinhVien == student.MaSinhVien)) return;

        context.ThanhVienClbs.Add(new ThanhVienClb
        {
            MaClb = club.MaClb,
            MaSinhVien = student.MaSinhVien,
            VaiTroClb = "Thành viên uy tín",
            NgayThamGia = new DateOnly(2024, 10, 1),
            TrangThai = "Hoạt động"
        });

        await context.SaveChangesAsync();
    }

    private static async Task EnsureSpecialtiesAsync(AppDbContext context, NguoiHuongDan mentor, IEnumerable<string> fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            var field = await context.LinhVucHocTaps.FirstOrDefaultAsync(l => l.TenLinhVuc == fieldName);
            if (field == null) continue;
            if (await context.ChuyenMonNguoiHuongDans.AnyAsync(c => c.MaHuongDan == mentor.MaHuongDan && c.MaLinhVuc == field.MaLinhVuc)) continue;

            context.ChuyenMonNguoiHuongDans.Add(new ChuyenMonNguoiHuongDan
            {
                MaHuongDan = mentor.MaHuongDan,
                MaLinhVuc = field.MaLinhVuc,
                MucDoThanhThao = 4,
                MoTaKinhNghiem = $"Có kinh nghiệm hướng dẫn {fieldName} trong học tập 1-1."
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureAvailabilityAsync(AppDbContext context, int accountId, int offset)
    {
        if (await context.LichRanhs.AnyAsync(l => l.MaTaiKhoan == accountId)) return;

        var slots = new[]
        {
            (Thu: 2 + offset % 5, Start: new TimeOnly(18, 0), End: new TimeOnly(20, 0)),
            (Thu: 3 + offset % 4, Start: new TimeOnly(19, 0), End: new TimeOnly(21, 0)),
            (Thu: 8, Start: new TimeOnly(8 + offset % 3, 0), End: new TimeOnly(10 + offset % 3, 0))
        };

        foreach (var slot in slots)
        {
            context.LichRanhs.Add(new LichRanh
            {
                MaTaiKhoan = accountId,
                Thu = slot.Thu,
                GioBatDau = slot.Start,
                GioKetThuc = slot.End
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureMentorRankingAsync(AppDbContext context, NguoiHuongDan mentor, MentorSeed seed, int rank)
    {
        var month = DateTime.Now.ToString("MM/yyyy");
        if (await context.XepHangMentors.AnyAsync(x => x.MaHuongDan == mentor.MaHuongDan && x.ThangNam == month)) return;

        context.XepHangMentors.Add(new XepHangMentor
        {
            MaHuongDan = mentor.MaHuongDan,
            DiemUyTin = seed.Reputation,
            HangTong = rank,
            HangTheoLinhVuc = Math.Max(1, rank % 4),
            ThangNam = month
        });

        await context.SaveChangesAsync();
    }

    private static async Task EnsureApprovedMentorApplicationAsync(AppDbContext context, SinhVien student, MentorSeed mentor, TaiKhoan coVanAccount)
    {
        var field = await context.LinhVucHocTaps.FirstOrDefaultAsync(l => l.TenLinhVuc == mentor.Fields.FirstOrDefault());
        if (field == null) return;
        if (await context.DangKyHuongDans.AnyAsync(d => d.MaSinhVien == student.MaSinhVien && d.MaLinhVuc == field.MaLinhVuc)) return;

        context.DangKyHuongDans.Add(new DangKyHuongDan
        {
            MaSinhVien = student.MaSinhVien,
            MaLinhVuc = field.MaLinhVuc,
            DiemMon = mentor.Gpa,
            MinhChung = "Hồ sơ mẫu đã được giảng viên/cố vấn duyệt.",
            LyDo = $"Được quản lý chuyên môn bởi {coVanAccount.HoTen}.",
            TrangThaiClb = mentor.ClubName == null ? "Không yêu cầu" : "Đã xác nhận",
            TrangThaiCoVan = "Đã duyệt",
            TrangThaiDuyet = "Đã duyệt",
            NgayDangKy = DateTime.Now.AddDays(-30)
        });

        await context.SaveChangesAsync();
    }

    private sealed record MentorSeed(
        string Email,
        string Name,
        string StudentCode,
        string Major,
        decimal Gpa,
        string Skills,
        string Goal,
        decimal Reputation,
        decimal Rating,
        int RatingCount,
        string? ClubName,
        IReadOnlyCollection<string> Fields);
}

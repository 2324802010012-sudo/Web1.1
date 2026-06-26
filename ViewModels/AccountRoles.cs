namespace StudyConnect.ViewModels;

public static class AccountRoles
{
    public const string QuanTri = "QuanTri";
    public const string SinhVien = "SinhVien";
    public const string Mentor = "Mentor";
    public const string ChuNhiemClb = "ChuNhiemCLB";
    public const string CoVan = "CoVan";

    public static readonly string[] All =
    [
        QuanTri,
        SinhVien,
        Mentor,
        ChuNhiemClb,
        CoVan
    ];

    public static string DisplayName(string role) => role switch
    {
        QuanTri => "Quản trị viên",
        SinhVien => "Sinh viên",
        Mentor => "Mentor",
        ChuNhiemClb => "Chủ nhiệm CLB",
        CoVan => "Giảng viên / Cố vấn",
        _ => role
    };

    public static bool IsValid(string role) => All.Contains(role);
}

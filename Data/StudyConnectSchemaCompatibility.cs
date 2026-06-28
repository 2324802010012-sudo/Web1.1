using Microsoft.EntityFrameworkCore;

namespace StudyConnect.Data;

public static class StudyConnectSchemaCompatibility
{
    public static async Task EnsureCompatibleAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await AddBridgeColumnsAsync(context);
        await FixNullableUniqueConstraintsAsync(context);
        await BackfillBridgeColumnsAsync(context);

        await EnsureDefaultAsync(context, "DotDeCuPhoChuNhiem", "DeCuBatDau", "SYSDATETIME()");
        await EnsureDefaultAsync(context, "DotDeCuPhoChuNhiem", "DeCuKetThuc", "DATEADD(day, 7, SYSDATETIME())");
        await EnsureDefaultAsync(context, "DotDeCuPhoChuNhiem", "BauChonBatDau", "DATEADD(day, 7, SYSDATETIME())");
        await EnsureDefaultAsync(context, "DotDeCuPhoChuNhiem", "BauChonKetThuc", "DATEADD(day, 14, SYSDATETIME())");

        await EnsureBridgeTablesAsync(context);
    }

    private static Task AddBridgeColumnsAsync(AppDbContext context)
    {
        return context.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH(N'dbo.TaiKhoan', N'MatKhauHash') IS NULL
    ALTER TABLE dbo.TaiKhoan ADD MatKhauHash NVARCHAR(255) NULL;
IF COL_LENGTH(N'dbo.TaiKhoan', N'VaiTro') IS NULL
    ALTER TABLE dbo.TaiKhoan ADD VaiTro NVARCHAR(50) NULL;

IF COL_LENGTH(N'dbo.NguoiHuongDan', N'MaTaiKhoan') IS NULL
    ALTER TABLE dbo.NguoiHuongDan ADD MaTaiKhoan INT NULL;

IF COL_LENGTH(N'dbo.GhepNoiHocTap', N'ChuyenMonScore') IS NULL
    ALTER TABLE dbo.GhepNoiHocTap ADD ChuyenMonScore DECIMAL(5,2) NOT NULL CONSTRAINT DF_GNHT_ChuyenMonScore_App DEFAULT (0);
IF COL_LENGTH(N'dbo.GhepNoiHocTap', N'LichRanhScore') IS NULL
    ALTER TABLE dbo.GhepNoiHocTap ADD LichRanhScore DECIMAL(5,2) NOT NULL CONSTRAINT DF_GNHT_LichRanhScore_App DEFAULT (0);
IF COL_LENGTH(N'dbo.GhepNoiHocTap', N'DiemUyTinScore') IS NULL
    ALTER TABLE dbo.GhepNoiHocTap ADD DiemUyTinScore DECIMAL(5,2) NOT NULL CONSTRAINT DF_GNHT_DiemUyTinScore_App DEFAULT (0);
IF COL_LENGTH(N'dbo.GhepNoiHocTap', N'DanhGiaScore') IS NULL
    ALTER TABLE dbo.GhepNoiHocTap ADD DanhGiaScore DECIMAL(5,2) NOT NULL CONSTRAINT DF_GNHT_DanhGiaScore_App DEFAULT (0);
IF COL_LENGTH(N'dbo.GhepNoiHocTap', N'TuongDongScore') IS NULL
    ALTER TABLE dbo.GhepNoiHocTap ADD TuongDongScore DECIMAL(5,2) NOT NULL CONSTRAINT DF_GNHT_TuongDongScore_App DEFAULT (0);
IF COL_LENGTH(N'dbo.GhepNoiHocTap', N'LaMentorChinh') IS NULL
    ALTER TABLE dbo.GhepNoiHocTap ADD LaMentorChinh BIT NOT NULL CONSTRAINT DF_GNHT_LaMentorChinh_App DEFAULT (0);
IF COL_LENGTH(N'dbo.GhepNoiHocTap', N'NgayMentorPhanHoi') IS NULL
    ALTER TABLE dbo.GhepNoiHocTap ADD NgayMentorPhanHoi DATETIME2 NULL;
IF COL_LENGTH(N'dbo.GhepNoiHocTap', N'GhiChuPhanHoi') IS NULL
    ALTER TABLE dbo.GhepNoiHocTap ADD GhiChuPhanHoi NVARCHAR(MAX) NULL;

IF COL_LENGTH(N'dbo.DanhGiaHuongDan', N'MaGhepNoi') IS NULL
    ALTER TABLE dbo.DanhGiaHuongDan ADD MaGhepNoi INT NULL;
IF COL_LENGTH(N'dbo.DanhGiaHuongDan', N'MaLichHoc') IS NULL
    ALTER TABLE dbo.DanhGiaHuongDan ADD MaLichHoc INT NULL;

IF COL_LENGTH(N'dbo.HoatDongCLB', N'ThoiGian') IS NULL
    ALTER TABLE dbo.HoatDongCLB ADD ThoiGian DATETIME2 NULL;

IF COL_LENGTH(N'dbo.DotDeCuPhoChuNhiem', N'ThoiGianBatDau') IS NULL
    ALTER TABLE dbo.DotDeCuPhoChuNhiem ADD ThoiGianBatDau DATETIME2 NULL;
IF COL_LENGTH(N'dbo.DotDeCuPhoChuNhiem', N'ThoiGianKetThuc') IS NULL
    ALTER TABLE dbo.DotDeCuPhoChuNhiem ADD ThoiGianKetThuc DATETIME2 NULL;
""");
    }

    private static async Task BackfillBridgeColumnsAsync(AppDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH(N'dbo.TaiKhoan', N'MatKhau') IS NOT NULL
    EXEC(N'UPDATE dbo.TaiKhoan SET MatKhauHash = MatKhau WHERE MatKhauHash IS NULL');

IF OBJECT_ID(N'dbo.TaiKhoanVaiTro', N'U') IS NOT NULL
BEGIN
    UPDATE tk
    SET VaiTro = CASE COALESCE(vt.MaVaiTro, N'SinhVien')
        WHEN N'Admin' THEN N'QuanTri'
        WHEN N'GiangVien' THEN N'CoVan'
        ELSE COALESCE(vt.MaVaiTro, N'SinhVien')
    END
    FROM dbo.TaiKhoan tk
    OUTER APPLY (
        SELECT TOP (1) MaVaiTro
        FROM dbo.TaiKhoanVaiTro r
        WHERE r.MaTaiKhoan = tk.MaTaiKhoan
        ORDER BY CASE r.MaVaiTro
            WHEN N'Admin' THEN 0
            WHEN N'GiangVien' THEN 1
            WHEN N'Mentor' THEN 2
            WHEN N'ChuNhiemCLB' THEN 3
            ELSE 4
        END
    ) vt
    WHERE tk.VaiTro IS NULL;
END;

UPDATE dbo.TaiKhoan SET VaiTro = N'SinhVien' WHERE VaiTro IS NULL;

UPDATE nhd
SET MaTaiKhoan = COALESCE(sv.MaTaiKhoan, gv.MaTaiKhoan)
FROM dbo.NguoiHuongDan nhd
LEFT JOIN dbo.SinhVien sv ON sv.MaSinhVien = nhd.MaSinhVien
LEFT JOIN dbo.GiangVien gv ON gv.MaGiangVien = nhd.MaGiangVien
WHERE nhd.MaTaiKhoan IS NULL;

UPDATE dg
SET MaGhepNoi = lh.MaGhepNoi
FROM dbo.DanhGiaHuongDan dg
JOIN dbo.LichHoc lh ON lh.MaLichHoc = dg.MaLichHoc
WHERE dg.MaGhepNoi IS NULL;
""");

        await context.Database.ExecuteSqlRawAsync("""
IF COL_LENGTH(N'dbo.HoatDongCLB', N'ThoiGianBatDau') IS NOT NULL
    EXEC(N'UPDATE dbo.HoatDongCLB SET ThoiGian = ThoiGianBatDau WHERE ThoiGian IS NULL');

IF COL_LENGTH(N'dbo.DotDeCuPhoChuNhiem', N'DeCuBatDau') IS NOT NULL
    EXEC(N'UPDATE dbo.DotDeCuPhoChuNhiem SET ThoiGianBatDau = DeCuBatDau WHERE ThoiGianBatDau IS NULL');
IF COL_LENGTH(N'dbo.DotDeCuPhoChuNhiem', N'BauChonKetThuc') IS NOT NULL
    EXEC(N'UPDATE dbo.DotDeCuPhoChuNhiem SET ThoiGianKetThuc = BauChonKetThuc WHERE ThoiGianKetThuc IS NULL');
""");
    }

    private static Task FixNullableUniqueConstraintsAsync(AppDbContext context)
    {
        return context.Database.ExecuteSqlRawAsync("""
DECLARE @dropSql NVARCHAR(MAX) = N'';

SELECT @dropSql = @dropSql + N'ALTER TABLE dbo.NguoiHuongDan DROP CONSTRAINT [' + kc.name + N'];'
FROM sys.key_constraints kc
JOIN sys.index_columns ic
    ON ic.object_id = kc.parent_object_id
   AND ic.index_id = kc.unique_index_id
JOIN sys.columns c
    ON c.object_id = ic.object_id
   AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID(N'dbo.NguoiHuongDan')
  AND kc.[type] = N'UQ'
  AND c.name IN (N'MaSinhVien', N'MaGiangVien');

IF LEN(@dropSql) > 0
    EXEC sp_executesql @dropSql;

IF COL_LENGTH(N'dbo.NguoiHuongDan', N'MaSinhVien') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NguoiHuongDan') AND name = N'UX_NHD_MaSinhVien_NotNull')
    CREATE UNIQUE INDEX UX_NHD_MaSinhVien_NotNull ON dbo.NguoiHuongDan(MaSinhVien) WHERE MaSinhVien IS NOT NULL;

IF COL_LENGTH(N'dbo.NguoiHuongDan', N'MaGiangVien') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NguoiHuongDan') AND name = N'UX_NHD_MaGiangVien_NotNull')
    CREATE UNIQUE INDEX UX_NHD_MaGiangVien_NotNull ON dbo.NguoiHuongDan(MaGiangVien) WHERE MaGiangVien IS NOT NULL;
""");
    }

    private static Task EnsureBridgeTablesAsync(AppDbContext context)
    {
        return context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.LichSuHocTap', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LichSuHocTap (
        MaLichSu INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MaSinhVien INT NOT NULL,
        MaGhepNoi INT NOT NULL,
        SoBuoiDaHoc INT NULL CONSTRAINT DF_LichSuHocTap_SoBuoi_App DEFAULT (0),
        TienDo NVARCHAR(100) NULL,
        KetQuaTongHop NVARCHAR(MAX) NULL,
        NgayCapNhat DATETIME2 NULL CONSTRAINT DF_LichSuHocTap_Ngay_App DEFAULT (SYSDATETIME())
    );
END;

IF OBJECT_ID(N'dbo.XepHangMentor', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.XepHangMentor (
        MaXepHang INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        MaHuongDan INT NOT NULL,
        DiemUyTin DECIMAL(5,2) NULL,
        HangTong INT NULL,
        HangTheoLinhVuc INT NULL,
        ThangNam NVARCHAR(20) NULL
    );
END;
""");
    }

    private static Task EnsureDefaultAsync(AppDbContext context, string table, string column, string expression)
    {
        var constraintName = $"DF_{table}_{column}_App";
#pragma warning disable EF1002
        return context.Database.ExecuteSqlRawAsync($"""
IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NOT NULL
AND NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.{table}')
      AND dc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.{table}'), N'{column}', 'ColumnId')
)
BEGIN
    ALTER TABLE dbo.{table} ADD CONSTRAINT {constraintName} DEFAULT ({expression}) FOR {column};
END;
""");
#pragma warning restore EF1002
    }
}

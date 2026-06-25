using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using StudyConnect.Models;

namespace StudyConnect.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BaoCaoBuoiHoc> BaoCaoBuoiHocs { get; set; }

    public virtual DbSet<CauLacBo> CauLacBos { get; set; }

    public virtual DbSet<ChuyenMonNguoiHuongDan> ChuyenMonNguoiHuongDans { get; set; }

    public virtual DbSet<DangKyHuongDan> DangKyHuongDans { get; set; }

    public virtual DbSet<DanhGiaHuongDan> DanhGiaHuongDans { get; set; }

    public virtual DbSet<DotDeCuPhoChuNhiem> DotDeCuPhoChuNhiems { get; set; }

    public virtual DbSet<GhepNoiHocTap> GhepNoiHocTaps { get; set; }

    public virtual DbSet<GiangVien> GiangViens { get; set; }

    public virtual DbSet<HoatDongClb> HoatDongClbs { get; set; }

    public virtual DbSet<LichHoc> LichHocs { get; set; }

    public virtual DbSet<LichRanh> LichRanhs { get; set; }

    public virtual DbSet<LichSuHocTap> LichSuHocTaps { get; set; }

    public virtual DbSet<LinhVucHocTap> LinhVucHocTaps { get; set; }

    public virtual DbSet<NguoiHuongDan> NguoiHuongDans { get; set; }

    public virtual DbSet<PhieuBauPhoChuNhiem> PhieuBauPhoChuNhiems { get; set; }

    public virtual DbSet<SinhVien> SinhViens { get; set; }

    public virtual DbSet<TaiKhoan> TaiKhoans { get; set; }

    public virtual DbSet<TaiLieuClb> TaiLieuClbs { get; set; }

    public virtual DbSet<ThanhVienClb> ThanhVienClbs { get; set; }

    public virtual DbSet<ThongBao> ThongBaos { get; set; }

    public virtual DbSet<UngVienPhoChuNhiem> UngVienPhoChuNhiems { get; set; }

    public virtual DbSet<XepHangMentor> XepHangMentors { get; set; }

    public virtual DbSet<YeuCauHoTroHocTap> YeuCauHoTroHocTaps { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=LAPTOP-JB4U48JA\\SQLEXPRESS01;Initial Catalog=StudyConnectDB;Integrated Security=True;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BaoCaoBuoiHoc>(entity =>
        {
            entity.HasKey(e => e.MaBaoCao).HasName("PK__BaoCaoBu__25A9188C7F36EC98");

            entity.Property(e => e.NgayBaoCao).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MaLichHocNavigation).WithOne(p => p.BaoCaoBuoiHoc)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__BaoCaoBuo__MaLic__19DFD96B");
        });

        modelBuilder.Entity<CauLacBo>(entity =>
        {
            entity.HasKey(e => e.MaClb).HasName("PK__CauLacBo__3DCE036991D09197");

            entity.Property(e => e.TrangThai).HasDefaultValue("Hoạt động");
        });

        modelBuilder.Entity<ChuyenMonNguoiHuongDan>(entity =>
        {
            entity.HasKey(e => e.MaChuyenMon).HasName("PK__ChuyenMo__9A6A2321535D5622");

            entity.HasOne(d => d.MaHuongDanNavigation).WithMany(p => p.ChuyenMonNguoiHuongDans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChuyenMon__MaHuo__00200768");

            entity.HasOne(d => d.MaLinhVucNavigation).WithMany(p => p.ChuyenMonNguoiHuongDans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChuyenMon__MaLin__01142BA1");
        });

        modelBuilder.Entity<DangKyHuongDan>(entity =>
        {
            entity.HasKey(e => e.MaDangKy).HasName("PK__DangKyHu__BA90F02D92CE459C");

            entity.Property(e => e.NgayDangKy).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThaiClb).HasDefaultValue("Không yêu cầu");
            entity.Property(e => e.TrangThaiCoVan).HasDefaultValue("Chờ duyệt");
            entity.Property(e => e.TrangThaiDuyet).HasDefaultValue("Chờ duyệt");

            entity.HasOne(d => d.MaLinhVucNavigation).WithMany(p => p.DangKyHuongDans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DangKyHuo__MaLin__73BA3083");

            entity.HasOne(d => d.MaSinhVienNavigation).WithMany(p => p.DangKyHuongDans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DangKyHuo__MaSin__72C60C4A");
        });

        modelBuilder.Entity<DanhGiaHuongDan>(entity =>
        {
            entity.HasKey(e => e.MaDanhGia).HasName("PK__DanhGiaH__AA9515BFB3430D2B");

            entity.Property(e => e.NgayDanhGia).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MaGhepNoiNavigation).WithMany(p => p.DanhGiaHuongDans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DanhGiaHu__MaGhe__208CD6FA");

            entity.HasOne(d => d.MaHuongDanNavigation).WithMany(p => p.DanhGiaHuongDans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DanhGiaHu__MaHuo__1EA48E88");

            entity.HasOne(d => d.MaSinhVienNavigation).WithMany(p => p.DanhGiaHuongDans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DanhGiaHu__MaSin__1F98B2C1");
        });

        modelBuilder.Entity<DotDeCuPhoChuNhiem>(entity =>
        {
            entity.HasKey(e => e.MaDot).HasName("PK__DotDeCuP__3D89F56E56A9537A");

            entity.Property(e => e.TrangThai).HasDefaultValue("Đang mở");

            entity.HasOne(d => d.MaClbNavigation).WithMany(p => p.DotDeCuPhoChuNhiems)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__DotDeCuPh__MaCLB__29221CFB");
        });

        modelBuilder.Entity<GhepNoiHocTap>(entity =>
        {
            entity.HasKey(e => e.MaGhepNoi).HasName("PK__GhepNoiH__0A31973A9B3861D7");

            entity.Property(e => e.DiemPhuHop).HasDefaultValue(0m);
            entity.Property(e => e.NgayGhep).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai).HasDefaultValue("Đề xuất");

            entity.HasOne(d => d.MaHuongDanNavigation).WithMany(p => p.GhepNoiHocTaps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GhepNoiHo__MaHuo__114A936A");

            entity.HasOne(d => d.MaYeuCauNavigation).WithMany(p => p.GhepNoiHocTaps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GhepNoiHo__MaYeu__10566F31");
        });

        modelBuilder.Entity<GiangVien>(entity =>
        {
            entity.HasKey(e => e.MaGiangVien).HasName("PK__GiangVie__C03BEEBAE2006769");

            entity.HasOne(d => d.MaTaiKhoanNavigation).WithOne(p => p.GiangVien)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__GiangVien__MaTai__5535A963");
        });

        modelBuilder.Entity<HoatDongClb>(entity =>
        {
            entity.HasKey(e => e.MaHoatDong).HasName("PK__HoatDong__BD808BE77A66F8FE");

            entity.Property(e => e.NgayDang).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MaClbNavigation).WithMany(p => p.HoatDongClbs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__HoatDongC__MaCLB__66603565");

            entity.HasOne(d => d.NguoiDangNavigation).WithMany(p => p.HoatDongClbs).HasConstraintName("FK__HoatDongC__Nguoi__6754599E");
        });

        modelBuilder.Entity<LichHoc>(entity =>
        {
            entity.HasKey(e => e.MaLichHoc).HasName("PK__LichHoc__150EBC211903F8B7");

            entity.Property(e => e.TrangThai).HasDefaultValue("Sắp diễn ra");

            entity.HasOne(d => d.MaGhepNoiNavigation).WithMany(p => p.LichHocs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LichHoc__MaGhepN__151B244E");
        });

        modelBuilder.Entity<LichRanh>(entity =>
        {
            entity.HasKey(e => e.MaLichRanh).HasName("PK__LichRanh__0942D64658375AC0");

            entity.HasOne(d => d.MaTaiKhoanNavigation).WithMany(p => p.LichRanhs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LichRanh__MaTaiK__0A9D95DB");
        });

        modelBuilder.Entity<LichSuHocTap>(entity =>
        {
            entity.HasKey(e => e.MaLichSu).HasName("PK__LichSuHo__C443222A32000AB0");

            entity.Property(e => e.NgayCapNhat).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.SoBuoiDaHoc).HasDefaultValue(0);

            entity.HasOne(d => d.MaGhepNoiNavigation).WithMany(p => p.LichSuHocTaps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LichSuHoc__MaGhe__3B40CD36");

            entity.HasOne(d => d.MaSinhVienNavigation).WithMany(p => p.LichSuHocTaps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__LichSuHoc__MaSin__3A4CA8FD");
        });

        modelBuilder.Entity<LinhVucHocTap>(entity =>
        {
            entity.HasKey(e => e.MaLinhVuc).HasName("PK__LinhVucH__318894A027BC2ADF");

            entity.Property(e => e.TrangThai).HasDefaultValue("Hoạt động");
        });

        modelBuilder.Entity<NguoiHuongDan>(entity =>
        {
            entity.HasKey(e => e.MaHuongDan).HasName("PK__NguoiHuo__3D465C43B64766A8");

            entity.Property(e => e.DiemDanhGia).HasDefaultValue(0m);
            entity.Property(e => e.DiemUyTin).HasDefaultValue(0m);
            entity.Property(e => e.SoLuotDanhGia).HasDefaultValue(0);
            entity.Property(e => e.TrangThai).HasDefaultValue("Hoạt động");

            entity.HasOne(d => d.MaTaiKhoanNavigation).WithOne(p => p.NguoiHuongDan)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__NguoiHuon__MaTai__7B5B524B");
        });

        modelBuilder.Entity<PhieuBauPhoChuNhiem>(entity =>
        {
            entity.HasKey(e => e.MaPhieu).HasName("PK__PhieuBau__2660BFE00CA4801B");

            entity.Property(e => e.ThoiGianBau).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MaDotNavigation).WithMany(p => p.PhieuBauPhoChuNhiems)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PhieuBauP__MaDot__339FAB6E");

            entity.HasOne(d => d.MaSinhVienBauNavigation).WithMany(p => p.PhieuBauPhoChuNhiems)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PhieuBauP__MaSin__3587F3E0");

            entity.HasOne(d => d.MaUngVienNavigation).WithMany(p => p.PhieuBauPhoChuNhiems)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PhieuBauP__MaUng__3493CFA7");
        });

        modelBuilder.Entity<SinhVien>(entity =>
        {
            entity.HasKey(e => e.MaSinhVien).HasName("PK__SinhVien__939AE775AA84D6DA");

            entity.HasOne(d => d.MaTaiKhoanNavigation).WithOne(p => p.SinhVien)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SinhVien__MaTaiK__5070F446");
        });

        modelBuilder.Entity<TaiKhoan>(entity =>
        {
            entity.HasKey(e => e.MaTaiKhoan).HasName("PK__TaiKhoan__AD7C6529D398164E");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai).HasDefaultValue("Hoạt động");
        });

        modelBuilder.Entity<TaiLieuClb>(entity =>
        {
            entity.HasKey(e => e.MaTaiLieu).HasName("PK__TaiLieuC__FD18A657699591A7");

            entity.Property(e => e.NgayDang).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MaClbNavigation).WithMany(p => p.TaiLieuClbs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TaiLieuCL__MaCLB__6B24EA82");

            entity.HasOne(d => d.NguoiDangNavigation).WithMany(p => p.TaiLieuClbs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TaiLieuCL__Nguoi__6C190EBB");
        });

        modelBuilder.Entity<ThanhVienClb>(entity =>
        {
            entity.HasKey(e => e.MaThanhVien).HasName("PK__ThanhVie__2BE0A0F08116DBA0");

            entity.Property(e => e.NgayThamGia).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai).HasDefaultValue("Hoạt động");
            entity.Property(e => e.VaiTroClb).HasDefaultValue("Thành viên");

            entity.HasOne(d => d.MaClbNavigation).WithMany(p => p.ThanhVienClbs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ThanhVien__MaCLB__619B8048");

            entity.HasOne(d => d.MaSinhVienNavigation).WithMany(p => p.ThanhVienClbs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ThanhVien__MaSin__628FA481");
        });

        modelBuilder.Entity<ThongBao>(entity =>
        {
            entity.HasKey(e => e.MaThongBao).HasName("PK__ThongBao__04DEB54EBDDCAA7A");

            entity.Property(e => e.DaDoc).HasDefaultValue(false);
            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.MaTaiKhoanNavigation).WithMany(p => p.ThongBaos)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ThongBao__MaTaiK__25518C17");
        });

        modelBuilder.Entity<UngVienPhoChuNhiem>(entity =>
        {
            entity.HasKey(e => e.MaUngVien).HasName("PK__UngVienP__8FDBA8A96EAD5251");

            entity.Property(e => e.TrangThai).HasDefaultValue("Hợp lệ");

            entity.HasOne(d => d.MaDotNavigation).WithMany(p => p.UngVienPhoChuNhiems)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UngVienPh__MaDot__2CF2ADDF");

            entity.HasOne(d => d.MaSinhVienNavigation).WithMany(p => p.UngVienPhoChuNhiemMaSinhVienNavigations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UngVienPh__MaSin__2DE6D218");

            entity.HasOne(d => d.NguoiDeCuNavigation).WithMany(p => p.UngVienPhoChuNhiemNguoiDeCuNavigations).HasConstraintName("FK__UngVienPh__Nguoi__2EDAF651");
        });

        modelBuilder.Entity<XepHangMentor>(entity =>
        {
            entity.HasKey(e => e.MaXepHang).HasName("PK__XepHangM__F42619F6D535610F");

            entity.HasOne(d => d.MaHuongDanNavigation).WithMany(p => p.XepHangMentors)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__XepHangMe__MaHuo__3E1D39E1");
        });

        modelBuilder.Entity<YeuCauHoTroHocTap>(entity =>
        {
            entity.HasKey(e => e.MaYeuCau).HasName("PK__YeuCauHo__CFA5DF4E81DEFCB9");

            entity.Property(e => e.NgayTao).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.TrangThai).HasDefaultValue("Đang chờ");

            entity.HasOne(d => d.MaLinhVucNavigation).WithMany(p => p.YeuCauHoTroHocTaps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__YeuCauHoT__MaLin__06CD04F7");

            entity.HasOne(d => d.MaSinhVienNavigation).WithMany(p => p.YeuCauHoTroHocTaps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__YeuCauHoT__MaSin__05D8E0BE");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

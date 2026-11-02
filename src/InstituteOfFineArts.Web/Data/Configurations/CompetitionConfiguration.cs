using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// Cấu hình EF Core cho bảng competition: kiểu cột, khóa, ràng buộc CHECK, index, quan hệ.
/// Thiết kế chi tiết: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md mục 6.2.
/// </summary>
public class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    /// <summary>Áp dụng cấu hình cho entity Competition.</summary>
    public void Configure(EntityTypeBuilder<Competition> b)
    {
        // Ràng buộc: ngày kết thúc ≥ ngày bắt đầu (BR-06); trạng thái hợp lệ
        b.ToTable("competition", t =>
        {
            t.HasCheckConstraint("CK_competition_dates", "[end_date] >= [start_date]");
            t.HasCheckConstraint("CK_competition_status", "[status] IN ('UPCOMING','ONGOING','CLOSED','GRADED')");
        });

        // Kiểu dữ liệu từng cột
        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.PublishedAt).HasPrecision(0);
        b.Property(x => x.CoverImagePath).HasMaxLength(500).IsUnicode(false);

        // Mã cuộc thi duy nhất; index phục vụ trang chủ (lọc theo trạng thái và ngày)
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_competition_code");
        b.HasIndex(x => new { x.Status, x.StartDate, x.EndDate }).HasDatabaseName("IX_competition_status_dates");

        // Người tạo và người công bố đều là nhân sự
        b.HasOne(x => x.CreatedByStaff).WithMany()
            .HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PublishedByStaff).WithMany()
            .HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

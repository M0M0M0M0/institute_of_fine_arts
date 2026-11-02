using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// Cấu hình EF Core cho bảng exhibition: kiểu cột, khóa, ràng buộc CHECK, index, quan hệ.
/// Thiết kế chi tiết: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md mục 6.2.
/// </summary>
public class ExhibitionConfiguration : IEntityTypeConfiguration<Exhibition>
{
    /// <summary>Áp dụng cấu hình cho entity Exhibition.</summary>
    public void Configure(EntityTypeBuilder<Exhibition> b)
    {
        b.ToTable("exhibition", t =>
        {
            t.HasCheckConstraint("CK_exhibition_dates", "[end_date] >= [start_date]");
            t.HasCheckConstraint("CK_exhibition_status", "[status] IN ('UPCOMING','ONGOING','FINISHED')");
        });

        // Kiểu dữ liệu từng cột
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Venue).HasMaxLength(255);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Organizer).HasMaxLength(200);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);

        // Người tạo (Staff hoặc Manager)
        b.HasOne(x => x.CreatedByStaff).WithMany()
            .HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

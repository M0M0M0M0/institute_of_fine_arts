using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// Cấu hình EF Core cho bảng award_issued: kiểu cột, khóa, ràng buộc CHECK, index, quan hệ.
/// Thiết kế chi tiết: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md mục 6.2.
/// </summary>
public class AwardIssuedConfiguration : IEntityTypeConfiguration<AwardIssued>
{
    /// <summary>Áp dụng cấu hình cho entity AwardIssued.</summary>
    public void Configure(EntityTypeBuilder<AwardIssued> b)
    {
        b.ToTable("award_issued");

        // Ngày trao mặc định là hôm nay
        b.Property(x => x.IssuedDate).HasDefaultValueSql("CAST(SYSDATETIME() AS date)");

        // Giải nào
        b.HasOne(x => x.CompetitionAward).WithMany(a => a.IssuedAwards)
            .HasForeignKey(x => x.CompetitionAwardId).OnDelete(DeleteBehavior.Restrict);

        // Bài nào – submission_id duy nhất ⇒ tối đa 1 giải / học viên / cuộc thi (BR-31)
        b.HasOne(x => x.Submission).WithOne(s => s.AwardIssued)
            .HasForeignKey<AwardIssued>(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SubmissionId).IsUnique().HasDatabaseName("UX_award_issued_submission");

        // Người trao giải
        b.HasOne(x => x.IssuedByStaff).WithMany()
            .HasForeignKey(x => x.IssuedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// Cấu hình EF Core cho bảng submission: kiểu cột, khóa, ràng buộc CHECK, index, quan hệ.
/// Thiết kế chi tiết: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md mục 6.2.
/// </summary>
public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    /// <summary>Áp dụng cấu hình cho entity Submission.</summary>
    public void Configure(EntityTypeBuilder<Submission> b)
    {
        b.ToTable("submission", t =>
        {
            t.HasCheckConstraint("CK_submission_size", "[file_size] > 0");
            t.HasCheckConstraint("CK_submission_text_type", "[text_type] IN ('POEM','QUOTATION','STORY','OTHER')");
        });

        // Kiểu dữ liệu và độ dài theo BR-19
        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.ImagePath).HasMaxLength(500).IsUnicode(false);
        b.Property(x => x.ThumbnailPath).HasMaxLength(500).IsUnicode(false);
        b.Property(x => x.OriginalFileName).HasMaxLength(255);
        b.Property(x => x.TextType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.TextContent).HasMaxLength(4000);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.SubmittedAt).HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");

        // Mỗi học viên tối đa 1 bài / 1 cuộc thi (BR-15)
        b.HasIndex(x => new { x.CompetitionId, x.StudentId }).IsUnique().HasDatabaseName("UX_submission_competition_student");
        // Báo cáo bài nộp theo khoảng thời gian cho 1 cuộc thi
        b.HasIndex(x => new { x.CompetitionId, x.SubmittedAt }).HasDatabaseName("IX_submission_competition_submitted");
        b.HasIndex(x => x.StudentId).HasDatabaseName("IX_submission_student");

        // Quan hệ tới cuộc thi và học viên
        b.HasOne(x => x.Competition).WithMany(c => c.Submissions)
            .HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Student).WithMany(s => s.Submissions)
            .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}

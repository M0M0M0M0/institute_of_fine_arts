using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// Cấu hình EF Core cho bảng evaluation: kiểu cột, khóa, ràng buộc CHECK, index, quan hệ.
/// Thiết kế chi tiết: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md mục 6.2.
/// </summary>
public class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    /// <summary>Áp dụng cấu hình cho entity Evaluation.</summary>
    public void Configure(EntityTypeBuilder<Evaluation> b)
    {
        // Xếp loại hợp lệ; bài bị loại bắt buộc có lý do (BR-23)
        b.ToTable("evaluation", t =>
        {
            t.HasCheckConstraint("CK_evaluation_rating", "[rating] IN ('BEST','BETTER','GOOD','MODERATE','NORMAL','DISQUALIFIED')");
            t.HasCheckConstraint("CK_evaluation_dq_reason", "[rating] <> 'DISQUALIFIED' OR [negative_points] IS NOT NULL");
        });

        // Kiểu dữ liệu từng cột
        b.Property(x => x.Rating).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.EvaluatedAt).HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");

        // Lọc bài theo xếp loại (yêu cầu đề bài)
        b.HasIndex(x => x.Rating).HasDatabaseName("IX_evaluation_rating");

        // Mỗi bài tối đa 1 đánh giá (BR-22)
        b.HasOne(x => x.Submission).WithOne(s => s.Evaluation)
            .HasForeignKey<Evaluation>(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SubmissionId).IsUnique().HasDatabaseName("UX_evaluation_submission");

        // Người chấm
        b.HasOne(x => x.EvaluatedByStaff).WithMany()
            .HasForeignKey(x => x.EvaluatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// Cấu hình EF Core cho bảng competition_award: kiểu cột, khóa, ràng buộc CHECK, index, quan hệ.
/// Thiết kế chi tiết: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md mục 6.2.
/// </summary>
public class CompetitionAwardConfiguration : IEntityTypeConfiguration<CompetitionAward>
{
    /// <summary>Áp dụng cấu hình cho entity CompetitionAward.</summary>
    public void Configure(EntityTypeBuilder<CompetitionAward> b)
    {
        // Ràng buộc giá trị giải (BR-09)
        b.ToTable("competition_award", t =>
        {
            t.HasCheckConstraint("CK_competition_award_rank", "[rank_no] >= 1");
            t.HasCheckConstraint("CK_competition_award_prize", "[prize_value] >= 0");
            t.HasCheckConstraint("CK_competition_award_quantity", "[quantity] > 0");
        });

        // Kiểu dữ liệu từng cột; Rank lưu ở cột rank_no
        b.Property(x => x.AwardName).HasMaxLength(100);
        b.Property(x => x.Rank).HasColumnName("rank_no");
        b.Property(x => x.PrizeValue).HasPrecision(18, 2);
        b.Property(x => x.PrizeDescription).HasMaxLength(500);

        // Trong 1 cuộc thi không có 2 giải trùng tên
        b.HasIndex(x => new { x.CompetitionId, x.AwardName }).IsUnique().HasDatabaseName("UX_competition_award_name");

        // Xóa cuộc thi (chỉ khi chưa có bài – BR-11) thì xóa luôn các giải dự kiến
        b.HasOne(x => x.Competition).WithMany(c => c.Awards)
            .HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

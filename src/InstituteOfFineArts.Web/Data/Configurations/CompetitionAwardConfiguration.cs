using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table competition_award: column types, keys, CHECK constraints, indexes and relationships.
/// Design: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md section 6.2.
/// </summary>
public class CompetitionAwardConfiguration : IEntityTypeConfiguration<CompetitionAward>
{
    /// <summary>Applies the configuration to the CompetitionAward entity.</summary>
    public void Configure(EntityTypeBuilder<CompetitionAward> b)
    {
        // Award value constraints (BR-09)
        b.ToTable("competition_award", t =>
        {
            t.HasCheckConstraint("CK_competition_award_rank", "[rank_no] >= 1");
            t.HasCheckConstraint("CK_competition_award_prize", "[prize_value] >= 0");
            t.HasCheckConstraint("CK_competition_award_quantity", "[quantity] > 0");
        });

        // Column types; Rank is stored in column rank_no
        b.Property(x => x.AwardName).HasMaxLength(100);
        b.Property(x => x.Rank).HasColumnName("rank_no");
        b.Property(x => x.PrizeValue).HasPrecision(18, 2);
        b.Property(x => x.PrizeDescription).HasMaxLength(500);

        // No two awards with the same name in one competition
        b.HasIndex(x => new { x.CompetitionId, x.AwardName }).IsUnique().HasDatabaseName("UX_competition_award_name");

        // Deleting a competition (only allowed without submissions – BR-11) also deletes its planned awards
        b.HasOne(x => x.Competition).WithMany(c => c.Awards)
            .HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

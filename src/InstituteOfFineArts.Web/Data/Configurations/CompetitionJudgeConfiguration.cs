using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>EF Core configuration of table competition_judge (jury of a competition).</summary>
public class CompetitionJudgeConfiguration : IEntityTypeConfiguration<CompetitionJudge>
{
    /// <summary>Applies the configuration.</summary>
    public void Configure(EntityTypeBuilder<CompetitionJudge> b)
    {
        b.ToTable("competition_judge");
        // A teacher sits once on a jury
        b.HasIndex(x => new { x.CompetitionId, x.JudgeId }).IsUnique().HasDatabaseName("UX_competition_judge");
        b.HasIndex(x => x.JudgeId).HasDatabaseName("IX_competition_judge_judge");
        b.HasOne(x => x.Competition).WithMany(c => c.Judges).HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Judge).WithMany().HasForeignKey(x => x.JudgeId).OnDelete(DeleteBehavior.Restrict);
    }
}

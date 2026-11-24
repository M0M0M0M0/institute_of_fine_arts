using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>EF Core configuration of table competition_milestone: removed with its competition.</summary>
public class CompetitionMilestoneConfiguration : IEntityTypeConfiguration<CompetitionMilestone>
{
    /// <summary>Applies the configuration.</summary>
    public void Configure(EntityTypeBuilder<CompetitionMilestone> b)
    {
        b.ToTable("competition_milestone");
        b.Property(x => x.Title).HasMaxLength(CompetitionMilestone_TitleMax).IsRequired();
        b.Property(x => x.Content).HasMaxLength(CompetitionMilestone_ContentMax);
        b.HasIndex(x => new { x.CompetitionId, x.Date, x.SortOrder }).HasDatabaseName("IX_competition_milestone_date");
        b.HasOne(x => x.Competition).WithMany(c => c.Milestones).HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>Longest title.</summary>
    public const int CompetitionMilestone_TitleMax = 100;

    /// <summary>Longest content.</summary>
    public const int CompetitionMilestone_ContentMax = 1000;
}

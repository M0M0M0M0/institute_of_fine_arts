using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table competition_criterion. "Weights add up to 100" spans several rows, so it is
/// checked by the service; a single row only needs a weight between 0 (excluded) and 100.
/// </summary>
public class CompetitionCriterionConfiguration : IEntityTypeConfiguration<CompetitionCriterion>
{
    /// <summary>Applies the configuration.</summary>
    public void Configure(EntityTypeBuilder<CompetitionCriterion> b)
    {
        b.ToTable("competition_criterion", t =>
            t.HasCheckConstraint("CK_competition_criterion_weight", "[weight_percent] > 0 AND [weight_percent] <= 100"));
        b.Property(x => x.WeightPercent).HasPrecision(5, 2);
        b.HasIndex(x => new { x.CompetitionId, x.CriterionId }).IsUnique().HasDatabaseName("UX_competition_criterion");
        b.HasOne(x => x.Competition).WithMany(c => c.Criteria).HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Criterion).WithMany().HasForeignKey(x => x.CriterionId).OnDelete(DeleteBehavior.Restrict);
    }
}

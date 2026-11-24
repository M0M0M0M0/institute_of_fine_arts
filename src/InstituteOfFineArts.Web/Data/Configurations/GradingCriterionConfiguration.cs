using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>EF Core configuration of table grading_criterion (library of grading criteria).</summary>
public class GradingCriterionConfiguration : IEntityTypeConfiguration<GradingCriterion>
{
    /// <summary>Applies the configuration.</summary>
    public void Configure(EntityTypeBuilder<GradingCriterion> b)
    {
        b.ToTable("grading_criterion");
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Active).HasDefaultValue(true);
        b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UX_grading_criterion_name");
    }
}

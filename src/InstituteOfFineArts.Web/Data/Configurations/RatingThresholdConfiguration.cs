using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>EF Core configuration of table rating_threshold (one row per rating above NORMAL).</summary>
public class RatingThresholdConfiguration : IEntityTypeConfiguration<RatingThreshold>
{
    /// <summary>Applies the configuration.</summary>
    public void Configure(EntityTypeBuilder<RatingThreshold> b)
    {
        b.ToTable("rating_threshold", t =>
        {
            t.HasCheckConstraint("CK_rating_threshold_rating", "[rating] IN ('BEST','BETTER','GOOD','MODERATE')");
            t.HasCheckConstraint("CK_rating_threshold_score", "[min_score] >= 0 AND [min_score] <= 100");
        });
        b.Property(x => x.Rating).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.MinScore).HasPrecision(5, 2);
        b.HasIndex(x => x.Rating).IsUnique().HasDatabaseName("UX_rating_threshold_rating");
    }
}

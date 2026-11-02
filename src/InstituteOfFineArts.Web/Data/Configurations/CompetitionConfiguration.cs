using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table competition: column types, keys, CHECK constraints, indexes and relationships.
/// Design: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md section 6.2.
/// </summary>
public class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    /// <summary>Applies the configuration to the Competition entity.</summary>
    public void Configure(EntityTypeBuilder<Competition> b)
    {
        // Constraints: end date ≥ start date (BR-06); valid status
        b.ToTable("competition", t =>
        {
            t.HasCheckConstraint("CK_competition_dates", "[end_date] >= [start_date]");
            t.HasCheckConstraint("CK_competition_status", "[status] IN ('UPCOMING','ONGOING','CLOSED','GRADED')");
        });

        // Column types
        b.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.PublishedAt).HasPrecision(0);
        b.Property(x => x.CoverImagePath).HasMaxLength(500).IsUnicode(false);

        // Unique code; index used by the home page (filter by status and dates)
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_competition_code");
        b.HasIndex(x => new { x.Status, x.StartDate, x.EndDate }).HasDatabaseName("IX_competition_status_dates");

        // Creator and publisher are staff members
        b.HasOne(x => x.CreatedByStaff).WithMany()
            .HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PublishedByStaff).WithMany()
            .HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

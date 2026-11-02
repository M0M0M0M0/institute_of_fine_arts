using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table award_issued: column types, keys, CHECK constraints, indexes and relationships.
/// Design: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md section 6.2.
/// </summary>
public class AwardIssuedConfiguration : IEntityTypeConfiguration<AwardIssued>
{
    /// <summary>Applies the configuration to the AwardIssued entity.</summary>
    public void Configure(EntityTypeBuilder<AwardIssued> b)
    {
        b.ToTable("award_issued");

        // Issue date defaults to today
        b.Property(x => x.IssuedDate).HasDefaultValueSql("CAST(SYSDATETIME() AS date)");

        // Which award
        b.HasOne(x => x.CompetitionAward).WithMany(a => a.IssuedAwards)
            .HasForeignKey(x => x.CompetitionAwardId).OnDelete(DeleteBehavior.Restrict);

        // Which submission – submission_id unique ⇒ at most one award per student per competition (BR-31)
        b.HasOne(x => x.Submission).WithOne(s => s.AwardIssued)
            .HasForeignKey<AwardIssued>(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SubmissionId).IsUnique().HasDatabaseName("UX_award_issued_submission");

        // Staff member who issued the award
        b.HasOne(x => x.IssuedByStaff).WithMany()
            .HasForeignKey(x => x.IssuedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

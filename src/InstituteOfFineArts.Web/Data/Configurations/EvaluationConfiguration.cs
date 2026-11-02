using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table evaluation: column types, keys, CHECK constraints, indexes and relationships.
/// Design: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md section 6.2.
/// </summary>
public class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    /// <summary>Applies the configuration to the Evaluation entity.</summary>
    public void Configure(EntityTypeBuilder<Evaluation> b)
    {
        // Valid rating; a disqualified entry must have a reason (BR-23)
        b.ToTable("evaluation", t =>
        {
            t.HasCheckConstraint("CK_evaluation_rating", "[rating] IN ('BEST','BETTER','GOOD','MODERATE','NORMAL','DISQUALIFIED')");
            t.HasCheckConstraint("CK_evaluation_dq_reason", "[rating] <> 'DISQUALIFIED' OR [negative_points] IS NOT NULL");
        });

        // Column types
        b.Property(x => x.Rating).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.EvaluatedAt).HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");

        // Filter submissions by rating (problem statement requirement)
        b.HasIndex(x => x.Rating).HasDatabaseName("IX_evaluation_rating");

        // At most one evaluation per submission (BR-22)
        b.HasOne(x => x.Submission).WithOne(s => s.Evaluation)
            .HasForeignKey<Evaluation>(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SubmissionId).IsUnique().HasDatabaseName("UX_evaluation_submission");

        // Grader
        b.HasOne(x => x.EvaluatedByStaff).WithMany()
            .HasForeignKey(x => x.EvaluatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

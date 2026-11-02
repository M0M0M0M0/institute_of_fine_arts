using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table submission: column types, keys, CHECK constraints, indexes and relationships.
/// Design: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md section 6.2.
/// </summary>
public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    /// <summary>Applies the configuration to the Submission entity.</summary>
    public void Configure(EntityTypeBuilder<Submission> b)
    {
        b.ToTable("submission", t =>
        {
            t.HasCheckConstraint("CK_submission_size", "[file_size] > 0");
            t.HasCheckConstraint("CK_submission_text_type", "[text_type] IN ('POEM','QUOTATION','STORY','OTHER')");
        });

        // Column types and lengths according to BR-19
        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.ImagePath).HasMaxLength(500).IsUnicode(false);
        b.Property(x => x.ThumbnailPath).HasMaxLength(500).IsUnicode(false);
        b.Property(x => x.OriginalFileName).HasMaxLength(255);
        b.Property(x => x.TextType).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.TextContent).HasMaxLength(4000);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.SubmittedAt).HasPrecision(0).HasDefaultValueSql("SYSDATETIME()");

        // At most one submission per student per competition (BR-15)
        b.HasIndex(x => new { x.CompetitionId, x.StudentId }).IsUnique().HasDatabaseName("UX_submission_competition_student");
        // Report of submissions within a date range for one competition
        b.HasIndex(x => new { x.CompetitionId, x.SubmittedAt }).HasDatabaseName("IX_submission_competition_submitted");
        b.HasIndex(x => x.StudentId).HasDatabaseName("IX_submission_student");

        // Relationships to competition and student
        b.HasOne(x => x.Competition).WithMany(c => c.Submissions)
            .HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Student).WithMany(s => s.Submissions)
            .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}

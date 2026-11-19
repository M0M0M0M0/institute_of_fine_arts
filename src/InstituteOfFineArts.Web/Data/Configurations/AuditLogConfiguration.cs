using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table audit_log (F-M17-01, optional feature M17).
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <summary>Applies the configuration to the AuditLog entity.</summary>
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_log");

        // Column types
        b.Property(x => x.Username).HasMaxLength(50).IsUnicode(false);
        b.Property(x => x.Action).HasMaxLength(50).IsUnicode(false);
        b.Property(x => x.Entity).HasMaxLength(50).IsUnicode(false);
        b.Property(x => x.Details).HasMaxLength(1000);
        b.Property(x => x.Summary).HasMaxLength(300);
        // No database default: LOGIN is the enum's first value, so EF would replace it with the default.
        // The entity starts at OTHER; old rows get OTHER from the migration.
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.ToTable(t => t.HasCheckConstraint("CK_audit_log_category",
            "[category] IN ('LOGIN','ACCOUNT','COMPETITION','SUBMISSION','GRADING','AWARD','EXHIBITION','SALE','OTHER')"));
        b.Property(x => x.IpAddress).HasMaxLength(45).IsUnicode(false);

        // The log page is read newest first and filtered by user / action
        b.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_audit_log_created_at");
        b.HasIndex(x => new { x.AccountId, x.CreatedAt }).HasDatabaseName("IX_audit_log_account");

        // History tabs: one competition / exhibition / entry / account, newest first
        b.HasIndex(x => new { x.CompetitionId, x.CreatedAt }).HasDatabaseName("IX_audit_log_competition");
        b.HasIndex(x => new { x.ExhibitionId, x.CreatedAt }).HasDatabaseName("IX_audit_log_exhibition");
        b.HasIndex(x => new { x.SubmissionId, x.CreatedAt }).HasDatabaseName("IX_audit_log_submission");
        b.HasIndex(x => new { x.TargetAccountId, x.CreatedAt }).HasDatabaseName("IX_audit_log_target_account");

        // The log outlives the account: the user name column keeps who it was
        b.HasOne(x => x.Account).WithMany()
            .HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.SetNull);
    }
}

using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table exhibition_entry: column types, keys, CHECK constraints, indexes and relationships.
/// Design: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md section 6.2.
/// </summary>
public class ExhibitionEntryConfiguration : IEntityTypeConfiguration<ExhibitionEntry>
{
    /// <summary>Applies the configuration to the ExhibitionEntry entity.</summary>
    public void Configure(EntityTypeBuilder<ExhibitionEntry> b)
    {
        // Money, status and sale rules (BR-41, BR-44, BR-45)
        b.ToTable("exhibition_entry", t =>
        {
            t.HasCheckConstraint("CK_entry_status", "[status] IN ('PENDING','ACCEPTED','REJECTED','SOLD','UNSOLD')");
            t.HasCheckConstraint("CK_entry_asking_price", "[asking_price] > 0");
            t.HasCheckConstraint("CK_entry_sold_price", "[sold_price] IS NULL OR [sold_price] > 0");
            t.HasCheckConstraint("CK_entry_paid_amount", "[paid_amount] IS NULL OR [paid_amount] >= 0");
            t.HasCheckConstraint("CK_entry_paid_date", "[paid_date] IS NULL OR [sold_date] IS NULL OR [paid_date] >= [sold_date]");
            // A sold painting must have a sale price, sale date and buyer
            t.HasCheckConstraint("CK_entry_sold", "[status] <> 'SOLD' OR ([sold_price] IS NOT NULL AND [sold_date] IS NOT NULL AND [customer_id] IS NOT NULL)");
            // Payment to the student can only be recorded for a sold painting, with amount and date
            t.HasCheckConstraint("CK_entry_paid", "[paid_to_student] = 0 OR ([status] = 'SOLD' AND [paid_amount] IS NOT NULL AND [paid_date] IS NOT NULL)");
            // A rejected entry must have a reason
            t.HasCheckConstraint("CK_entry_reject", "[status] <> 'REJECTED' OR [reject_reason] IS NOT NULL");
        });

        // Column types
        b.Property(x => x.AskingPrice).HasPrecision(18, 2);
        b.Property(x => x.SoldPrice).HasPrecision(18, 2);
        b.Property(x => x.PaidAmount).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.RejectReason).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(500);

        // BR-43: only one active entry (PENDING/ACCEPTED) per submission – filtered unique index
        b.HasIndex(x => x.SubmissionId).IsUnique()
            .HasFilter("[status] IN ('PENDING','ACCEPTED')")
            .HasDatabaseName("UX_entry_active_submission");
        b.HasIndex(x => new { x.ExhibitionId, x.Status }).HasDatabaseName("IX_entry_exhibition_status");

        // Relationships to exhibition, submission, buyer, proposer and reviewer
        b.HasOne(x => x.Exhibition).WithMany(e => e.Entries)
            .HasForeignKey(x => x.ExhibitionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Submission).WithMany(s => s.ExhibitionEntries)
            .HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Customer).WithMany(c => c.Purchases)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ProposedByAccount).WithMany()
            .HasForeignKey(x => x.ProposedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReviewedByStaff).WithMany()
            .HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

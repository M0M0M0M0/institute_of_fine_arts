using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table staff_profile: column types, keys, CHECK constraints, indexes and relationships.
/// Design: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md section 6.2.
/// </summary>
public class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    /// <summary>Applies the configuration to the StaffProfile entity.</summary>
    public void Configure(EntityTypeBuilder<StaffProfile> b)
    {
        b.ToTable("staff_profile");

        // Column types
        b.Property(x => x.StaffCode).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Position).HasMaxLength(100);
        b.Property(x => x.ClassesInCharge).HasMaxLength(255);
        b.Property(x => x.SubjectsInCharge).HasMaxLength(255);
        b.Property(x => x.Qualification).HasMaxLength(255);

        // Unique staff code
        b.HasIndex(x => x.StaffCode).IsUnique().HasDatabaseName("UX_staff_profile_staff_code");

        // One-to-one with account (account_id unique); no cascade delete because accounts are only locked
        b.HasOne(x => x.Account)
            .WithOne(a => a.StaffProfile)
            .HasForeignKey<StaffProfile>(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AccountId).IsUnique().HasDatabaseName("UX_staff_profile_account");
    }
}

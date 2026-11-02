using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table account: column types, keys, CHECK constraints, indexes and relationships.
/// Design: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md section 6.2.
/// </summary>
public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    /// <summary>Applies the configuration to the Account entity.</summary>
    public void Configure(EntityTypeBuilder<Account> b)
    {
        // Table name + CHECK constraints for the enum columns
        b.ToTable("account", t =>
        {
            t.HasCheckConstraint("CK_account_role", "[role] IN ('ADMIN','MANAGER','STAFF','STUDENT')");
            t.HasCheckConstraint("CK_account_gender", "[gender] IS NULL OR [gender] IN ('M','F','O')");
        });

        // Column types and lengths (VARCHAR = IsUnicode(false), NVARCHAR = default)
        b.Property(x => x.Username).HasMaxLength(50).IsUnicode(false);
        b.Property(x => x.PasswordHash).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.FullName).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Address).HasMaxLength(255);
        b.Property(x => x.Gender).HasConversion<string>().HasColumnType("char(1)");
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.LastLoginAt).HasPrecision(0);

        // Indexes: unique username and email (BR-01); filter by role
        b.HasIndex(x => x.Username).IsUnique().HasDatabaseName("UX_account_username");
        b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_account_email");
        b.HasIndex(x => x.Role).HasDatabaseName("IX_account_role");
    }
}

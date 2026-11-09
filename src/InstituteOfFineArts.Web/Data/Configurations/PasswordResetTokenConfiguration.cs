using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table password_reset_token (F-M16-01, optional feature M16).
/// </summary>
public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    /// <summary>Applies the configuration to the PasswordResetToken entity.</summary>
    public void Configure(EntityTypeBuilder<PasswordResetToken> b)
    {
        b.ToTable("password_reset_token");

        // SHA-256 in hex = 64 characters
        b.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false).IsFixedLength();

        // The reset link is looked up by its hash
        b.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UX_password_reset_token_hash");

        // Tokens disappear with their account
        b.HasOne(x => x.Account).WithMany()
            .HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>EF Core configuration of table device_token (push notifications to the mobile app).</summary>
public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    /// <summary>Applies the configuration to the DeviceToken entity.</summary>
    public void Configure(EntityTypeBuilder<DeviceToken> b)
    {
        b.ToTable("device_token");

        // FCM tokens are ASCII, up to about 200 characters today; room to spare
        b.Property(x => x.Token).HasMaxLength(512).IsUnicode(false);
        b.Property(x => x.Platform).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.LastSeenAt).HasPrecision(0);

        // A phone is registered once; looking up the phones of the receiving accounts
        b.HasIndex(x => x.Token).IsUnique().HasDatabaseName("UX_device_token_token");
        b.HasIndex(x => x.AccountId).HasDatabaseName("IX_device_token_account");

        // Phones disappear with their account
        b.HasOne(x => x.Account).WithMany()
            .HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

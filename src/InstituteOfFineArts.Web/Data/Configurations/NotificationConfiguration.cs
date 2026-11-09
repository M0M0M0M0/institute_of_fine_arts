using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table notification (F-M12-02, optional feature M12).
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <summary>Applies the configuration to the Notification entity.</summary>
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notification");

        // Column types
        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.Message).HasMaxLength(1000);
        b.Property(x => x.Link).HasMaxLength(255).IsUnicode(false);

        // The bell shows the newest unread notifications of one account
        b.HasIndex(x => new { x.AccountId, x.IsRead, x.CreatedAt }).HasDatabaseName("IX_notification_account_read");

        // Notifications disappear with their account
        b.HasOne(x => x.Account).WithMany()
            .HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

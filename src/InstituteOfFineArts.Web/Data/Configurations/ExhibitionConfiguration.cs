using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// EF Core configuration of table exhibition: column types, keys, CHECK constraints, indexes and relationships.
/// Design: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md section 6.2.
/// </summary>
public class ExhibitionConfiguration : IEntityTypeConfiguration<Exhibition>
{
    /// <summary>Applies the configuration to the Exhibition entity.</summary>
    public void Configure(EntityTypeBuilder<Exhibition> b)
    {
        b.ToTable("exhibition", t =>
        {
            t.HasCheckConstraint("CK_exhibition_dates", "[end_date] >= [start_date]");
            t.HasCheckConstraint("CK_exhibition_status", "[status] IN ('UPCOMING','ONGOING','FINISHED')");
        });

        // Column types
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Venue).HasMaxLength(255);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Organizer).HasMaxLength(200);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);

        // Creator (staff or manager)
        b.HasOne(x => x.CreatedByStaff).WithMany()
            .HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

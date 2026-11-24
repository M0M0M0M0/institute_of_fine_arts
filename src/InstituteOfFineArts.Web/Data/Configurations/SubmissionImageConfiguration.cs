using InstituteOfFineArts.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>EF Core configuration of table submission_image (pictures of an entry, BR-16/17/18).</summary>
public class SubmissionImageConfiguration : IEntityTypeConfiguration<SubmissionImage>
{
    /// <summary>Applies the configuration.</summary>
    public void Configure(EntityTypeBuilder<SubmissionImage> b)
    {
        b.ToTable("submission_image", t =>
            t.HasCheckConstraint("CK_submission_image_type", "[content_type] IN ('image/jpeg','image/png','image/webp')"));
        b.Property(x => x.ImagePath).HasMaxLength(500).IsUnicode(false);
        b.Property(x => x.ThumbnailPath).HasMaxLength(500).IsUnicode(false);
        b.Property(x => x.OriginalFileName).HasMaxLength(255);
        b.Property(x => x.ContentType).HasMaxLength(20).IsUnicode(false);
        b.HasIndex(x => new { x.SubmissionId, x.SortOrder }).HasDatabaseName("IX_submission_image_order");
        b.HasOne(x => x.Submission).WithMany(s => s.Images).HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

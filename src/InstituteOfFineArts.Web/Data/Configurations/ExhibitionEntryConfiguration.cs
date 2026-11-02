using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// Cấu hình EF Core cho bảng exhibition_entry: kiểu cột, khóa, ràng buộc CHECK, index, quan hệ.
/// Thiết kế chi tiết: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md mục 6.2.
/// </summary>
public class ExhibitionEntryConfiguration : IEntityTypeConfiguration<ExhibitionEntry>
{
    /// <summary>Áp dụng cấu hình cho entity ExhibitionEntry.</summary>
    public void Configure(EntityTypeBuilder<ExhibitionEntry> b)
    {
        // Ràng buộc tiền, trạng thái và luật bán tranh (BR-41, BR-44, BR-45)
        b.ToTable("exhibition_entry", t =>
        {
            t.HasCheckConstraint("CK_entry_status", "[status] IN ('PENDING','ACCEPTED','REJECTED','SOLD','UNSOLD')");
            t.HasCheckConstraint("CK_entry_asking_price", "[asking_price] > 0");
            t.HasCheckConstraint("CK_entry_sold_price", "[sold_price] IS NULL OR [sold_price] > 0");
            t.HasCheckConstraint("CK_entry_paid_amount", "[paid_amount] IS NULL OR [paid_amount] >= 0");
            t.HasCheckConstraint("CK_entry_paid_date", "[paid_date] IS NULL OR [sold_date] IS NULL OR [paid_date] >= [sold_date]");
            // Đã bán thì bắt buộc có giá bán, ngày bán và khách mua
            t.HasCheckConstraint("CK_entry_sold", "[status] <> 'SOLD' OR ([sold_price] IS NOT NULL AND [sold_date] IS NOT NULL AND [customer_id] IS NOT NULL)");
            // Chỉ ghi nhận đã trả tiền khi tranh đã bán, phải có số tiền và ngày trả
            t.HasCheckConstraint("CK_entry_paid", "[paid_to_student] = 0 OR ([status] = 'SOLD' AND [paid_amount] IS NOT NULL AND [paid_date] IS NOT NULL)");
            // Từ chối thì phải có lý do
            t.HasCheckConstraint("CK_entry_reject", "[status] <> 'REJECTED' OR [reject_reason] IS NOT NULL");
        });

        // Kiểu dữ liệu từng cột
        b.Property(x => x.AskingPrice).HasPrecision(18, 2);
        b.Property(x => x.SoldPrice).HasPrecision(18, 2);
        b.Property(x => x.PaidAmount).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.RejectReason).HasMaxLength(500);
        b.Property(x => x.Notes).HasMaxLength(500);

        // BR-43: mỗi bài chỉ có 1 entry đang hiệu lực (PENDING/ACCEPTED) – filtered unique index
        b.HasIndex(x => x.SubmissionId).IsUnique()
            .HasFilter("[status] IN ('PENDING','ACCEPTED')")
            .HasDatabaseName("UX_entry_active_submission");
        b.HasIndex(x => new { x.ExhibitionId, x.Status }).HasDatabaseName("IX_entry_exhibition_status");

        // Quan hệ tới triển lãm, bài dự thi, khách mua, người đề xuất, người duyệt
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

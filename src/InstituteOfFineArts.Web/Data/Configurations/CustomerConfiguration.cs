using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// Cấu hình EF Core cho bảng customer: kiểu cột, khóa, ràng buộc CHECK, index, quan hệ.
/// Thiết kế chi tiết: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md mục 6.2.
/// </summary>
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    /// <summary>Áp dụng cấu hình cho entity Customer.</summary>
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customer");

        // Kiểu dữ liệu từng cột
        b.Property(x => x.FullName).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Address).HasMaxLength(255);
        b.Property(x => x.Notes).HasMaxLength(500);

        // Tìm khách theo số điện thoại
        b.HasIndex(x => x.Phone).HasDatabaseName("IX_customer_phone");
    }
}

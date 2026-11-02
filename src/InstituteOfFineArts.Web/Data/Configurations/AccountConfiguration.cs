using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// Cấu hình EF Core cho bảng account: kiểu cột, khóa, ràng buộc CHECK, index, quan hệ.
/// Thiết kế chi tiết: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md mục 6.2.
/// </summary>
public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    /// <summary>Áp dụng cấu hình cho entity Account.</summary>
    public void Configure(EntityTypeBuilder<Account> b)
    {
        // Tên bảng + ràng buộc CHECK cho các cột enum
        b.ToTable("account", t =>
        {
            t.HasCheckConstraint("CK_account_role", "[role] IN ('ADMIN','MANAGER','STAFF','STUDENT')");
            t.HasCheckConstraint("CK_account_gender", "[gender] IS NULL OR [gender] IN ('M','F','O')");
        });

        // Kiểu dữ liệu và độ dài từng cột (VARCHAR = IsUnicode(false), NVARCHAR = mặc định)
        b.Property(x => x.Username).HasMaxLength(50).IsUnicode(false);
        b.Property(x => x.PasswordHash).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.FullName).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Address).HasMaxLength(255);
        b.Property(x => x.Gender).HasConversion<string>().HasColumnType("char(1)");
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.LastLoginAt).HasPrecision(0);

        // Index: username, email duy nhất (BR-01); lọc theo vai trò
        b.HasIndex(x => x.Username).IsUnique().HasDatabaseName("UX_account_username");
        b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_account_email");
        b.HasIndex(x => x.Role).HasDatabaseName("IX_account_role");
    }
}

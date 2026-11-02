using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// Cấu hình EF Core cho bảng staff_profile: kiểu cột, khóa, ràng buộc CHECK, index, quan hệ.
/// Thiết kế chi tiết: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md mục 6.2.
/// </summary>
public class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    /// <summary>Áp dụng cấu hình cho entity StaffProfile.</summary>
    public void Configure(EntityTypeBuilder<StaffProfile> b)
    {
        b.ToTable("staff_profile");

        // Kiểu dữ liệu từng cột
        b.Property(x => x.StaffCode).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Position).HasMaxLength(100);
        b.Property(x => x.ClassesInCharge).HasMaxLength(255);
        b.Property(x => x.SubjectsInCharge).HasMaxLength(255);
        b.Property(x => x.Qualification).HasMaxLength(255);

        // Mã nhân viên duy nhất
        b.HasIndex(x => x.StaffCode).IsUnique().HasDatabaseName("UX_staff_profile_staff_code");

        // Quan hệ 1–1 với account (account_id duy nhất); không xóa dây chuyền vì tài khoản chỉ bị khóa
        b.HasOne(x => x.Account)
            .WithOne(a => a.StaffProfile)
            .HasForeignKey<StaffProfile>(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AccountId).IsUnique().HasDatabaseName("UX_staff_profile_account");
    }
}

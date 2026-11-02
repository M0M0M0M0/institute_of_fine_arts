using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InstituteOfFineArts.Web.Data.Configurations;

/// <summary>
/// Cấu hình EF Core cho bảng student_profile: kiểu cột, khóa, ràng buộc CHECK, index, quan hệ.
/// Thiết kế chi tiết: docs/plan/02_Kien_truc_Cong_nghe_CSDL.md mục 6.2.
/// </summary>
public class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    /// <summary>Áp dụng cấu hình cho entity StudentProfile.</summary>
    public void Configure(EntityTypeBuilder<StudentProfile> b)
    {
        b.ToTable("student_profile", t =>
            t.HasCheckConstraint("CK_student_profile_status", "[enrollment_status] IN ('STUDYING','GRADUATED','SUSPENDED','DROPPED')"));

        // Kiểu dữ liệu từng cột
        b.Property(x => x.StudentCode).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.CourseName).HasMaxLength(100);
        b.Property(x => x.ClassName).HasMaxLength(50);
        b.Property(x => x.EnrollmentStatus).HasConversion<string>().HasMaxLength(20).IsUnicode(false);

        // Mã học viên duy nhất; index lọc theo lớp
        b.HasIndex(x => x.StudentCode).IsUnique().HasDatabaseName("UX_student_profile_student_code");
        b.HasIndex(x => x.ClassName).HasDatabaseName("IX_student_profile_class_name");

        // Quan hệ 1–1 với account
        b.HasOne(x => x.Account)
            .WithOne(a => a.StudentProfile)
            .HasForeignKey<StudentProfile>(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AccountId).IsUnique().HasDatabaseName("UX_student_profile_account");
    }
}

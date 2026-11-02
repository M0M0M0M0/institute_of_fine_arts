using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Phần dữ liệu mẫu: 15 tài khoản của 4 vai trò (task BE1-01).
/// Danh sách và mật khẩu theo docs/plan/04 mục 4.1 và 4.5 – CHỈ dùng cho môi trường phát triển / demo.
/// </summary>
public static partial class DbSeeder
{
    /// <summary>Mật khẩu demo của tài khoản Quản trị viên.</summary>
    private const string AdminPassword = "Admin@123";

    /// <summary>Mật khẩu demo của tài khoản Quản lý.</summary>
    private const string ManagerPassword = "Manager@123";

    /// <summary>Mật khẩu demo chung của các tài khoản Giáo viên.</summary>
    private const string StaffPassword = "Staff@123";

    /// <summary>Mật khẩu demo chung của các tài khoản Học viên.</summary>
    private const string StudentPassword = "Student@123";

    /// <summary>
    /// Thêm tài khoản mẫu kèm hồ sơ: 1 ADMIN, 1 MANAGER, 3 STAFF (có staff_profile)
    /// và 10 STUDENT (có student_profile); student10 bị khóa để test đăng nhập tài khoản khóa.
    /// </summary>
    /// <param name="db">DbContext dùng để thêm dữ liệu</param>
    private static async Task SeedAccountsAsync(AppDbContext db)
    {
        var today = DateTimeHelper.Today;

        // Băm mỗi mật khẩu một lần rồi dùng lại (BCrypt chậm có chủ đích, băm 15 lần sẽ làm khởi động lâu)
        var adminHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword);
        var managerHash = BCrypt.Net.BCrypt.HashPassword(ManagerPassword);
        var staffHash = BCrypt.Net.BCrypt.HashPassword(StaffPassword);
        var studentHash = BCrypt.Net.BCrypt.HashPassword(StudentPassword);

        // ---- (1) Nhân sự: Quản trị viên, Quản lý, 3 Giáo viên (đều có hồ sơ staff_profile)
        var staffMembers = new[]
        {
            NewStaff("admin01", adminHash, "Nguyễn Văn An", Role.ADMIN, Gender.M, "NV0001",
                "Quản trị hệ thống", today.AddYears(-4), classes: null, subjects: null),
            NewStaff("manager01", managerHash, "Trần Thị Bích Ngọc", Role.MANAGER, Gender.F, "NV0002",
                "Giám đốc học viện", today.AddYears(-5), classes: null, subjects: null),
            NewStaff("staff01", staffHash, "Lê Minh Hoàng", Role.STAFF, Gender.M, "NV0003",
                "Giảng viên", today.AddYears(-3), classes: "HH01, HH02", subjects: "Hội họa sơn dầu, Ký họa"),
            NewStaff("staff02", staffHash, "Phạm Thu Hà", Role.STAFF, Gender.F, "NV0004",
                "Giảng viên", today.AddYears(-2), classes: "HT01", subjects: "Hoạt hình 2D, Thiết kế nhân vật"),
            NewStaff("staff03", staffHash, "Võ Quốc Bảo", Role.STAFF, Gender.M, "NV0005",
                "Trưởng bộ môn Mỹ thuật số", today.AddYears(-6), classes: "MT01", subjects: "Mỹ thuật số, Màu sắc"),
        };
        db.StaffProfiles.AddRange(staffMembers);

        // ---- (2) Học viên: 10 người chia vào 3 lớp; student10 bị khóa
        var studentNames = new[]
        {
            "Nguyễn Minh Khoa", "Trần Ngọc Ánh", "Lê Hoàng Phúc", "Phạm Gia Hân", "Hoàng Đức Tài",
            "Đặng Thảo Vy", "Bùi Thanh Tùng", "Đỗ Khánh Linh", "Ngô Bảo Châu", "Huỳnh Quang Vinh",
        };
        var classes = new[]
        {
            ("Hội họa", "HH01"), ("Hội họa", "HH02"), ("Hoạt hình", "HT01"), ("Mỹ thuật số", "MT01"),
        };

        for (var i = 0; i < studentNames.Length; i++)
        {
            // Số thứ tự 01..10 dùng cho username, mã học viên, email
            var no = i + 1;
            var (course, className) = classes[i % classes.Length];

            var student = new StudentProfile
            {
                StudentCode = $"HV{no:0000}",
                EnrollmentDate = today.AddMonths(-(6 + i)),
                CourseName = course,
                ClassName = className,
                EnrollmentStatus = EnrollmentStatus.STUDYING,
                Account = new Account
                {
                    Username = $"student{no:00}",
                    PasswordHash = studentHash,
                    FullName = studentNames[i],
                    Email = $"student{no:00}@ifa.local",
                    Phone = $"09120000{no:00}",
                    DateOfBirth = today.AddYears(-(19 + i % 5)).AddDays(-37 * no),
                    Gender = i % 2 == 0 ? Gender.M : Gender.F,
                    Address = "TP. Hồ Chí Minh",
                    Role = Role.STUDENT,
                    // student10 bị khóa để test TC-M01-07 (tài khoản khóa không đăng nhập được)
                    Active = no != 10,
                },
            };
            db.StudentProfiles.Add(student);
        }

        // Lưu một lần: EF Core tự thêm account trước rồi mới thêm hồ sơ (do quan hệ khóa ngoại)
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Tạo hồ sơ nhân sự kèm tài khoản đăng nhập (chưa lưu vào CSDL).
    /// </summary>
    /// <param name="username">Tên đăng nhập</param>
    /// <param name="passwordHash">Mật khẩu đã băm BCrypt</param>
    /// <param name="fullName">Họ tên</param>
    /// <param name="role">Vai trò: ADMIN, MANAGER hoặc STAFF</param>
    /// <param name="gender">Giới tính</param>
    /// <param name="staffCode">Mã nhân viên</param>
    /// <param name="position">Chức vụ</param>
    /// <param name="joinedDate">Ngày vào làm</param>
    /// <param name="classes">Các lớp phụ trách (null nếu không dạy)</param>
    /// <param name="subjects">Các môn phụ trách (null nếu không dạy)</param>
    private static StaffProfile NewStaff(string username, string passwordHash, string fullName, Role role,
        Gender gender, string staffCode, string position, DateOnly joinedDate, string? classes, string? subjects)
    {
        return new StaffProfile
        {
            StaffCode = staffCode,
            Position = position,
            JoinedDate = joinedDate,
            ClassesInCharge = classes,
            SubjectsInCharge = subjects,
            Account = new Account
            {
                Username = username,
                PasswordHash = passwordHash,
                FullName = fullName,
                Email = $"{username}@ifa.local",
                Phone = $"0903000{staffCode[^3..]}",
                Gender = gender,
                Address = "TP. Hồ Chí Minh",
                Role = role,
                Active = true,
            },
        };
    }
}

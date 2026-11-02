using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Sample data part: 15 accounts for the 4 roles (task BE1-01).
/// Accounts and passwords follow docs/plan/04 sections 4.1 and 4.5 – for development / demo ONLY.
/// Data is in English; person names are Vietnamese without diacritics.
/// </summary>
public static partial class DbSeeder
{
    /// <summary>Demo password of the administrator account.</summary>
    private const string AdminPassword = "Admin@123";

    /// <summary>Demo password of the manager account.</summary>
    private const string ManagerPassword = "Manager@123";

    /// <summary>Shared demo password of the staff accounts.</summary>
    private const string StaffPassword = "Staff@123";

    /// <summary>Shared demo password of the student accounts.</summary>
    private const string StudentPassword = "Student@123";

    /// <summary>
    /// Inserts sample accounts with profiles: 1 ADMIN, 1 MANAGER, 3 STAFF (with staff_profile)
    /// and 10 STUDENT (with student_profile); student10 is locked to test login of a locked account.
    /// </summary>
    /// <param name="db">DbContext used to insert the data</param>
    private static async Task SeedAccountsAsync(AppDbContext db)
    {
        var today = DateTimeHelper.Today;

        // Hash each password once and reuse it (BCrypt is slow on purpose; hashing 15 times would slow down startup)
        var adminHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword);
        var managerHash = BCrypt.Net.BCrypt.HashPassword(ManagerPassword);
        var staffHash = BCrypt.Net.BCrypt.HashPassword(StaffPassword);
        var studentHash = BCrypt.Net.BCrypt.HashPassword(StudentPassword);

        // ---- (1) Staff: administrator, manager and 3 teachers (all have a staff_profile)
        var staffMembers = new[]
        {
            NewStaff("admin01", adminHash, "Nguyen Van An", Role.ADMIN, Gender.M, "NV0001",
                "System Administrator", today.AddYears(-4), classes: null, subjects: null),
            NewStaff("manager01", managerHash, "Tran Bich Ngoc", Role.MANAGER, Gender.F, "NV0002",
                "Academy Director", today.AddYears(-5), classes: null, subjects: null),
            NewStaff("staff01", staffHash, "Le Minh Hoang", Role.STAFF, Gender.M, "NV0003",
                "Lecturer", today.AddYears(-3), classes: "PT01, PT02", subjects: "Oil Painting, Watercolor"),
            NewStaff("staff02", staffHash, "Pham Thu Ha", Role.STAFF, Gender.F, "NV0004",
                "Lecturer", today.AddYears(-2), classes: "AN01", subjects: "2D Animation, Character Design"),
            NewStaff("staff03", staffHash, "Vo Quoc Bao", Role.STAFF, Gender.M, "NV0005",
                "Head of Digital Art", today.AddYears(-6), classes: "DA01", subjects: "Digital Art, Color Theory"),
        };
        db.StaffProfiles.AddRange(staffMembers);

        // ---- (2) Students: 10 people spread over 4 classes; student10 is locked
        var studentNames = new[]
        {
            "Nguyen Minh Khoa", "Tran Ngoc Anh", "Le Hoang Phuc", "Pham Gia Han", "Hoang Duc Tai",
            "Dang Thao Vy", "Bui Thanh Tung", "Do Khanh Linh", "Ngo Bao Chau", "Huynh Quang Vinh",
        };
        var classes = new[]
        {
            ("Painting", "PT01"), ("Painting", "PT02"), ("Animation", "AN01"), ("Digital Art", "DA01"),
        };

        for (var i = 0; i < studentNames.Length; i++)
        {
            // Sequence number 01..10 used for username, student code and email
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
                    Address = "Ho Chi Minh City",
                    Role = Role.STUDENT,
                    // student10 is locked to test TC-M01-07 (a locked account cannot log in)
                    Active = no != 10,
                },
            };
            db.StudentProfiles.Add(student);
        }

        // Save once: EF Core inserts the accounts first, then the profiles (foreign key order)
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a staff profile together with its login account (not saved yet).
    /// </summary>
    /// <param name="username">Login name</param>
    /// <param name="passwordHash">BCrypt password hash</param>
    /// <param name="fullName">Full name</param>
    /// <param name="role">Role: ADMIN, MANAGER or STAFF</param>
    /// <param name="gender">Gender</param>
    /// <param name="staffCode">Staff code</param>
    /// <param name="position">Position</param>
    /// <param name="joinedDate">Date joined</param>
    /// <param name="classes">Classes handled (null if not teaching)</param>
    /// <param name="subjects">Subjects handled (null if not teaching)</param>
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
                Address = "Ho Chi Minh City",
                Role = role,
                Active = true,
            },
        };
    }
}

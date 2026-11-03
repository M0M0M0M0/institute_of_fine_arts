using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// Unit tests for ProfileService (task BE1-03): view profile, edit phone/address, change password (TC-M01-09).
/// </summary>
public class ProfileServiceTests
{
    /// <summary>Password of the test accounts.</summary>
    private const string Password = "Student@123";

    /// <summary>In-memory database with one student and one staff member; returns their account ids.</summary>
    private static (AppDbContext Db, long StudentId, long StaffId) CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var hash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);

        var student = new StudentProfile
        {
            StudentCode = "HV0002",
            EnrollmentDate = new DateOnly(2025, 9, 1),
            CourseName = "Painting",
            ClassName = "PT01",
            Account = new Account
            {
                Username = "student02", PasswordHash = hash, FullName = "Tran Thi Mai",
                Email = "student02@ifa.local", Role = Role.STUDENT, Active = true,
                Phone = "0901234567", Address = "Ho Chi Minh City",
            },
        };
        var staff = new StaffProfile
        {
            StaffCode = "NV0003",
            Position = "Lecturer",
            JoinedDate = new DateOnly(2024, 1, 1),
            ClassesInCharge = "PT01, PT02",
            SubjectsInCharge = "Oil Painting, Watercolor",
            Account = new Account
            {
                Username = "staff01", PasswordHash = hash, FullName = "Le Minh Hoang",
                Email = "staff01@ifa.local", Role = Role.STAFF, Active = true,
            },
        };
        db.StudentProfiles.Add(student);
        db.StaffProfiles.Add(staff);
        db.SaveChanges();
        return (db, student.AccountId, staff.AccountId);
    }

    /// <summary>Creates the service with an empty logger.</summary>
    private static ProfileService CreateService(AppDbContext db) => new(db, NullLogger<ProfileService>.Instance);

    /// <summary>A student sees their account data and student profile fields.</summary>
    [Fact]
    public async Task GetProfile_Student_ReturnsStudentFields()
    {
        var (db, studentId, _) = CreateDb();
        using var _ = db;

        var vm = await CreateService(db).GetProfileAsync(studentId);

        Assert.Equal("student02", vm.Username);
        Assert.Equal("Tran Thi Mai", vm.FullName);
        Assert.Equal(Role.STUDENT, vm.Role);
        Assert.Equal("HV0002", vm.Code);
        Assert.Equal("Painting", vm.CourseOrPosition);
        Assert.Equal("PT01", vm.Classes);
        Assert.Null(vm.Subjects);
        Assert.Equal(new DateOnly(2025, 9, 1), vm.SinceDate);
    }

    /// <summary>A staff member sees position, classes and subjects.</summary>
    [Fact]
    public async Task GetProfile_Staff_ReturnsStaffFields()
    {
        var (db, _, staffId) = CreateDb();
        using var _ = db;

        var vm = await CreateService(db).GetProfileAsync(staffId);

        Assert.Equal("NV0003", vm.Code);
        Assert.Equal("Lecturer", vm.CourseOrPosition);
        Assert.Equal("PT01, PT02", vm.Classes);
        Assert.Equal("Oil Painting, Watercolor", vm.Subjects);
    }

    /// <summary>Unknown account id → NotFoundException.</summary>
    [Fact]
    public async Task GetProfile_UnknownAccount_Throws()
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(db).GetProfileAsync(9999));
    }

    /// <summary>Phone and address are trimmed and saved; nothing else changes.</summary>
    [Fact]
    public async Task UpdateProfile_SavesPhoneAndAddressOnly()
    {
        var (db, studentId, _) = CreateDb();
        using var _ = db;

        await CreateService(db).UpdateProfileAsync(studentId,
            new EditProfileVm { Phone = "  +84 912 345 678 ", Address = " 12 Nguyen Hue, District 1 " });

        var account = await db.Accounts.SingleAsync(a => a.Id == studentId);
        Assert.Equal("+84 912 345 678", account.Phone);
        Assert.Equal("12 Nguyen Hue, District 1", account.Address);
        Assert.Equal("student02", account.Username);
        Assert.Equal(Role.STUDENT, account.Role);
    }

    /// <summary>Blank values clear the fields (stored as null).</summary>
    [Fact]
    public async Task UpdateProfile_BlankValues_StoredAsNull()
    {
        var (db, studentId, _) = CreateDb();
        using var _ = db;

        await CreateService(db).UpdateProfileAsync(studentId, new EditProfileVm { Phone = "   ", Address = "" });

        var account = await db.Accounts.SingleAsync(a => a.Id == studentId);
        Assert.Null(account.Phone);
        Assert.Null(account.Address);
    }

    /// <summary>Correct current password → new BCrypt hash; old password no longer works (TC-M01-09).</summary>
    [Fact]
    public async Task ChangePassword_Correct_ReplacesHash()
    {
        var (db, studentId, _) = CreateDb();
        using var _ = db;

        await CreateService(db).ChangePasswordAsync(studentId, Password, "NewPass2026");

        var hash = (await db.Accounts.SingleAsync(a => a.Id == studentId)).PasswordHash;
        Assert.StartsWith("$2", hash);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPass2026", hash));
        Assert.False(BCrypt.Net.BCrypt.Verify(Password, hash));
    }

    /// <summary>Wrong current password → BusinessException on the CurrentPassword field; hash unchanged.</summary>
    [Fact]
    public async Task ChangePassword_WrongCurrent_ThrowsAndKeepsHash()
    {
        var (db, studentId, _) = CreateDb();
        using var _ = db;
        var before = (await db.Accounts.SingleAsync(a => a.Id == studentId)).PasswordHash;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService(db).ChangePasswordAsync(studentId, "wrong123", "NewPass2026"));

        Assert.Equal(ProfileService.WrongCurrentPasswordMessage, ex.Message);
        Assert.Equal(nameof(ChangePasswordVm.CurrentPassword), ex.Field);
        Assert.Equal(before, (await db.Accounts.SingleAsync(a => a.Id == studentId)).PasswordHash);
    }

    /// <summary>Weak new password is rejected by the service too (BR-02).</summary>
    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public async Task ChangePassword_WeakNewPassword_Throws(string newPassword)
    {
        var (db, studentId, _) = CreateDb();
        using var _ = db;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService(db).ChangePasswordAsync(studentId, Password, newPassword));
        Assert.Equal(nameof(ChangePasswordVm.NewPassword), ex.Field);
    }

    /// <summary>New password equal to the current one → rejected.</summary>
    [Fact]
    public async Task ChangePassword_SameAsCurrent_Throws()
    {
        var (db, studentId, _) = CreateDb();
        using var _ = db;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateService(db).ChangePasswordAsync(studentId, Password, Password));
        Assert.Equal(ProfileService.SamePasswordMessage, ex.Message);
    }
}

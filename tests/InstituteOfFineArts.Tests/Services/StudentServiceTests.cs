using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Admin.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// StudentService (task BE2-03, TC-M04-01..05): create, search, details, edit, reset password, lock, delete (BR-01, BR-02, BR-04).
/// </summary>
public class StudentServiceTests
{
    /// <summary>
    /// Database with student01 (PT01, has a submission with an award and an exhibition entry), student02 (PT02, no data)
    /// and student03 (AN01, locked, GRADUATED, no data).
    /// </summary>
    private static (AppDbContext Db, Dictionary<string, StudentProfile> Students) CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var hash = BCrypt.Net.BCrypt.HashPassword("Student@123", workFactor: 4);
        StudentProfile New(string user, string name, string code, string course, string className, int month,
            EnrollmentStatus status = EnrollmentStatus.STUDYING, bool active = true) => new()
        {
            StudentCode = code, EnrollmentDate = new DateOnly(2025, month, 1), CourseName = course, ClassName = className, EnrollmentStatus = status,
            Account = new Account { Username = user, FullName = name, Email = $"{user}@ifa.local", Role = Role.STUDENT, PasswordHash = hash, Active = active },
        };
        var students = new Dictionary<string, StudentProfile>
        {
            ["student01"] = New("student01", "Nguyen Minh Khoa", "HV0001", "Painting", "PT01", 1),
            ["student02"] = New("student02", "Tran Ngoc Anh", "HV0002", "Painting", "PT02", 3),
            ["student03"] = New("student03", "Le Hoang Phuc", "HV0007", "Animation", "AN01", 6, EnrollmentStatus.GRADUATED, active: false),
        };
        db.StudentProfiles.AddRange(students.Values);
        db.SaveChanges();

        // student01: one submission, which won an award and is in an exhibition
        var submission = new Submission { StudentId = students["student01"].Id, CompetitionId = 1, Title = "Sunset", ImagePath = "x.jpg" };
        db.Submissions.Add(submission);
        db.SaveChanges();
        db.AwardsIssued.Add(new AwardIssued { SubmissionId = submission.Id, CompetitionAwardId = 1, IssuedBy = 1 });
        db.ExhibitionEntries.Add(new ExhibitionEntry { SubmissionId = submission.Id, ExhibitionId = 1, ProposedBy = students["student01"].AccountId, AskingPrice = 100 });
        db.SaveChanges();
        return (db, students);
    }

    /// <summary>Service under test.</summary>
    private static StudentService Service(AppDbContext db) => new(db, NullLogger<StudentService>.Instance);

    /// <summary>Valid create form (TC-M04-01 data).</summary>
    private static CreateStudentVm NewForm(string username = "student11", string email = "student11@ifa.local") => new()
    {
        NewUsername = username, Password = "Welcome2026", ConfirmPassword = "Welcome2026",
        FullName = "Dang Thi Mai", Email = email, Gender = Gender.F, DateOfBirth = new DateOnly(2004, 5, 1), Phone = "0901234567",
        EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting", ClassName = "PT03", EnrollmentStatus = EnrollmentStatus.STUDYING,
    };

    // ------------------------------------------------------------------ create

    /// <summary>TC-M04-01: STUDENT account + profile created, username lower-cased, next code after the highest (HV0008), BCrypt password.</summary>
    [Fact]
    public async Task Create_Valid()
    {
        var (db, _) = CreateDb();
        using var _ = db;

        var id = await Service(db).CreateAsync(NewForm("Student11"));
        var s = await db.StudentProfiles.Include(x => x.Account).SingleAsync(x => x.Id == id);
        Assert.Equal("student11", s.Account.Username);
        Assert.Equal("HV0008", s.StudentCode);
        Assert.Equal(Role.STUDENT, s.Account.Role);
        Assert.True(s.Account.Active);
        Assert.Equal("PT03", s.ClassName);
        Assert.Equal(new DateOnly(2025, 9, 1), s.EnrollmentDate);
        Assert.True(BCrypt.Net.BCrypt.Verify("Welcome2026", s.Account.PasswordHash));
    }

    /// <summary>A typed student code is upper-cased and kept.</summary>
    [Fact]
    public async Task Create_WithCode()
    {
        var (db, _) = CreateDb();
        using var _ = db;
        var form = NewForm();
        form.StudentCode = " hv0100 ";
        var id = await Service(db).CreateAsync(form);
        Assert.Equal("HV0100", (await db.StudentProfiles.SingleAsync(x => x.Id == id)).StudentCode);
    }

    /// <summary>Duplicate username (any case) is refused and nothing is created (BR-01).</summary>
    [Theory]
    [InlineData("student01")]
    [InlineData("STUDENT01")]
    public async Task Create_DuplicateUsername(string username)
    {
        var (db, _) = CreateDb();
        using var _ = db;
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(NewForm(username)));
        Assert.Equal(StudentService.UsernameTakenMessage, ex.Message);
        Assert.Equal(nameof(CreateStudentVm.NewUsername), ex.Field);
        Assert.Equal(3, await db.StudentProfiles.CountAsync());
    }

    /// <summary>Duplicate email (ignoring case) and duplicate student code are refused (BR-01).</summary>
    [Fact]
    public async Task Create_DuplicateEmailOrCode()
    {
        var (db, _) = CreateDb();
        using var _ = db;
        var service = Service(db);
        var email = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(NewForm(email: "STUDENT02@ifa.local")));
        Assert.Equal(StudentService.EmailTakenMessage, email.Message);
        Assert.Equal(nameof(StudentFormVm.Email), email.Field);

        var form = NewForm();
        form.StudentCode = "hv0002";
        var code = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(form));
        Assert.Equal(StudentService.CodeTakenMessage, code.Message);
        Assert.Equal(nameof(StudentFormVm.StudentCode), code.Field);
        Assert.Equal(3, await db.StudentProfiles.CountAsync());
    }

    /// <summary>BR-02 and TC-M04-02 are checked by the service too: weak password, date of birth in the future.</summary>
    [Fact]
    public async Task Create_WeakPasswordOrFutureBirthDate()
    {
        var (db, _) = CreateDb();
        using var _ = db;
        var service = Service(db);
        var weak = NewForm();
        weak.Password = "abcdefgh";
        Assert.Equal(nameof(CreateStudentVm.Password), (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(weak))).Field);

        var future = NewForm();
        future.DateOfBirth = new DateOnly(2099, 1, 1);
        Assert.Equal(nameof(StudentFormVm.DateOfBirth), (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(future))).Field);
        Assert.Equal(3, await db.StudentProfiles.CountAsync());
    }

    /// <summary>TC-M04-02: the form validation refuses a date of birth in the future.</summary>
    [Fact]
    public void Form_FutureBirthDateInvalid()
    {
        var form = NewForm();
        form.DateOfBirth = new DateOnly(2099, 1, 1);
        var errors = form.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(form)).ToList();
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(StudentFormVm.DateOfBirth)));
    }

    // ------------------------------------------------------------------ create many / check (CSV import, BE2-OPT 4)

    /// <summary>F-M04-08: CheckNewAsync lists every BR-01 / BR-02 violation without saving.</summary>
    [Fact]
    public async Task CheckNew_ListsAllViolations()
    {
        var (db, _) = CreateDb();
        using var _ = db;
        var form = NewForm("student01", "STUDENT02@ifa.local");
        form.StudentCode = "hv0007";
        form.Password = "weak";

        var errors = await Service(db).CheckNewAsync(form);

        Assert.Equal(new[] { StudentService.UsernameTakenMessage, StudentService.EmailTakenMessage, StudentService.CodeTakenMessage },
            errors.Take(3).Select(e => e.Message));
        Assert.Equal(nameof(CreateStudentVm.Password), errors[3].Field);
        Assert.Empty(await Service(db).CheckNewAsync(NewForm()));
        Assert.Equal(3, await db.StudentProfiles.CountAsync());
    }

    /// <summary>F-M04-08: CreateManyAsync creates every student with generated codes in order (HV0008, HV0009).</summary>
    [Fact]
    public async Task CreateMany_AllSaved()
    {
        var (db, _) = CreateDb();
        using var _ = db;

        var count = await Service(db).CreateManyAsync([NewForm("student11", "s11@ifa.local"), NewForm("student12", "s12@ifa.local")]);

        Assert.Equal(2, count);
        var codes = await db.StudentProfiles.Where(s => s.Account.Username.StartsWith("student1")).OrderBy(s => s.StudentCode)
            .Select(s => s.StudentCode).ToListAsync();
        Assert.Equal(new[] { "HV0008", "HV0009" }, codes);
    }

    /// <summary>F-M04-08: one invalid student (BR-01) → BusinessException naming the username, nothing saved (all or nothing).</summary>
    [Fact]
    public async Task CreateMany_OneInvalid_NothingSaved()
    {
        var (db, _) = CreateDb();
        using var _ = db;

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            Service(db).CreateManyAsync([NewForm("student11", "s11@ifa.local"), NewForm("student02", "s99@ifa.local")]));

        Assert.Equal($"student02: {StudentService.UsernameTakenMessage}", ex.Message);
        Assert.Equal(3, await db.StudentProfiles.CountAsync());
        Assert.Equal(3, await db.Accounts.CountAsync());
    }

    /// <summary>F-M04-08: the same username or email twice inside one batch is refused (BR-01), nothing saved.</summary>
    [Fact]
    public async Task CreateMany_DuplicateInBatch_Refused()
    {
        var (db, _) = CreateDb();
        using var _ = db;

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            Service(db).CreateManyAsync([NewForm("student11", "same@ifa.local"), NewForm("student12", "SAME@ifa.local")]));

        Assert.Equal($"student12: {StudentService.EmailTakenMessage}", ex.Message);
        Assert.Equal(3, await db.StudentProfiles.CountAsync());
    }

    // ------------------------------------------------------------------ search / details

    /// <summary>Search by part of the name, code, course, class, study status, enrollment date and account status.</summary>
    [Fact]
    public async Task Search_Filters()
    {
        var (db, _) = CreateDb();
        using var _ = db;
        var service = Service(db);

        async Task<string[]> Users(StudentSearchVm s) => (await service.SearchAsync(s)).Items.Select(i => i.Username).ToArray();
        Assert.Equal(new[] { "student02" }, await Users(new StudentSearchVm { Keyword = "Ngoc" }));
        Assert.Equal(new[] { "student03" }, await Users(new StudentSearchVm { Keyword = "HV0007" }));
        Assert.Equal(new[] { "student01", "student02" }, await Users(new StudentSearchVm { Course = "Paint" }));
        Assert.Equal(new[] { "student01" }, await Users(new StudentSearchVm { ClassName = "PT01" }));
        Assert.Equal(new[] { "student03" }, await Users(new StudentSearchVm { Status = EnrollmentStatus.GRADUATED }));
        Assert.Equal(new[] { "student02", "student03" }, await Users(new StudentSearchVm { EnrolledFrom = new DateOnly(2025, 2, 1) }));
        Assert.Equal(new[] { "student01" }, await Users(new StudentSearchVm { EnrolledTo = new DateOnly(2025, 2, 1) }));
        Assert.Equal(new[] { "student03" }, await Users(new StudentSearchVm { Active = false }));
        Assert.Empty(await Users(new StudentSearchVm { Keyword = "nobody" }));
        Assert.Equal(new[] { "student01", "student02", "student03" }, await Users(new StudentSearchVm()));   // by code
    }

    /// <summary>Paging: 2 rows per page.</summary>
    [Fact]
    public async Task Search_Paging()
    {
        var (db, _) = CreateDb();
        using var _ = db;
        var page2 = await Service(db).SearchAsync(new StudentSearchVm { Page = 2 }, pageSize: 2);
        Assert.Equal(3, page2.TotalCount);
        Assert.Equal("HV0007", Assert.Single(page2.Items).StudentCode);
    }

    /// <summary>Details show the study information and related data counts; unknown id → NotFoundException.</summary>
    [Fact]
    public async Task Details_RelatedData()
    {
        var (db, students) = CreateDb();
        using var _ = db;
        var service = Service(db);
        var vm = await service.GetDetailsAsync(students["student01"].Id);
        Assert.Equal("student01", vm.Username);
        Assert.Equal("PT01", vm.ClassName);
        Assert.Equal(1, vm.SubmissionCount);
        Assert.Equal(1, vm.ExhibitionEntryCount);
        Assert.Equal(1, vm.AwardCount);
        Assert.True(vm.HasRelatedData);
        Assert.False((await service.GetDetailsAsync(students["student02"].Id)).HasRelatedData);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailsAsync(999));
    }

    // ------------------------------------------------------------------ update

    /// <summary>TC-M04-03: edit class and phone; username and role unchanged.</summary>
    [Fact]
    public async Task Update_KeepsUsername()
    {
        var (db, students) = CreateDb();
        using var _ = db;
        var service = Service(db);
        var id = students["student02"].Id;
        var form = await service.GetForEditAsync(id);
        form.ClassName = "PT05";
        form.Phone = "0909999999";
        form.EnrollmentStatus = EnrollmentStatus.SUSPENDED;
        form.Username = "hacked";

        await service.UpdateAsync(id, form);
        var s = await db.StudentProfiles.Include(x => x.Account).SingleAsync(x => x.Id == id);
        Assert.Equal("PT05", s.ClassName);
        Assert.Equal("0909999999", s.Account.Phone);
        Assert.Equal(EnrollmentStatus.SUSPENDED, s.EnrollmentStatus);
        Assert.Equal("student02", s.Account.Username);
        Assert.Equal(Role.STUDENT, s.Account.Role);
        Assert.Equal("HV0002", s.StudentCode);
    }

    /// <summary>Email or code of another student is refused on edit; keeping your own is fine.</summary>
    [Fact]
    public async Task Update_Unique()
    {
        var (db, students) = CreateDb();
        using var _ = db;
        var service = Service(db);
        var id = students["student02"].Id;
        var form = await service.GetForEditAsync(id);
        await service.UpdateAsync(id, form);   // unchanged → ok

        form.Email = "Student01@ifa.local";
        Assert.Equal(StudentService.EmailTakenMessage, (await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(id, form))).Message);
        form = await service.GetForEditAsync(id);
        form.StudentCode = "HV0001";
        Assert.Equal(StudentService.CodeTakenMessage, (await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(id, form))).Message);
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(999, form));
    }

    // ------------------------------------------------------------------ password / lock / delete

    /// <summary>Reset password stores a new BCrypt hash (old one stops working); weak passwords are refused.</summary>
    [Fact]
    public async Task ResetPassword()
    {
        var (db, students) = CreateDb();
        using var _ = db;
        var service = Service(db);
        await service.ResetPasswordAsync(students["student02"].Id, "TempPass99");
        var hash = (await db.Accounts.SingleAsync(a => a.Username == "student02")).PasswordHash;
        Assert.True(BCrypt.Net.BCrypt.Verify("TempPass99", hash));
        Assert.False(BCrypt.Net.BCrypt.Verify("Student@123", hash));
        await Assert.ThrowsAsync<BusinessException>(() => service.ResetPasswordAsync(students["student02"].Id, "short"));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ResetPasswordAsync(999, "TempPass99"));
    }

    /// <summary>Lock then unlock.</summary>
    [Fact]
    public async Task LockUnlock()
    {
        var (db, students) = CreateDb();
        using var _ = db;
        var service = Service(db);
        await service.SetActiveAsync(students["student02"].Id, false);
        Assert.False((await db.Accounts.SingleAsync(a => a.Username == "student02")).Active);
        await service.SetActiveAsync(students["student02"].Id, true);
        Assert.True((await db.Accounts.SingleAsync(a => a.Username == "student02")).Active);
        await Assert.ThrowsAsync<NotFoundException>(() => service.SetActiveAsync(999, false));
    }

    /// <summary>BR-04 / TC-M04-04: without related data → deleted; with submissions → locked, submission kept.</summary>
    [Fact]
    public async Task Delete_HardOrDeactivate()
    {
        var (db, students) = CreateDb();
        using var _ = db;
        var service = Service(db);

        Assert.Equal(StudentDeleteResult.Deleted, await service.DeleteAsync(students["student02"].Id));
        Assert.False(await db.Accounts.AnyAsync(a => a.Username == "student02"));
        Assert.False(await db.StudentProfiles.AnyAsync(s => s.StudentCode == "HV0002"));

        Assert.Equal(StudentDeleteResult.Deactivated, await service.DeleteAsync(students["student01"].Id));
        Assert.False((await db.Accounts.SingleAsync(a => a.Username == "student01")).Active);
        Assert.Single(db.Submissions.Where(x => x.StudentId == students["student01"].Id));

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(999));
    }

    /// <summary>An exhibition entry proposed by the student (without own submissions) also prevents the hard delete.</summary>
    [Fact]
    public async Task Delete_ProposedEntryOnlyLocks()
    {
        var (db, students) = CreateDb();
        using var _ = db;
        db.ExhibitionEntries.Add(new ExhibitionEntry { SubmissionId = 1, ExhibitionId = 2, ProposedBy = students["student02"].AccountId, AskingPrice = 50 });
        db.SaveChanges();
        Assert.Equal(StudentDeleteResult.Deactivated, await Service(db).DeleteAsync(students["student02"].Id));
    }
}

using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Admin.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// StaffService (task BE2-02, TC-M03-01..06): create, search, edit, reset password, lock, delete (BR-01..BR-04).
/// </summary>
public class StaffServiceTests
{
    /// <summary>Database with admin01 (ADMIN), manager01, staff01 (has an evaluation) and staff02 (no data).</summary>
    private static (AppDbContext Db, Dictionary<string, StaffProfile> Staff) CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var hash = BCrypt.Net.BCrypt.HashPassword("Staff@123", workFactor: 4);
        StaffProfile New(string user, string name, Role role, string code, string? subjects = null, string? classes = null) => new()
        {
            StaffCode = code, JoinedDate = new DateOnly(2024, 1, 1), SubjectsInCharge = subjects, ClassesInCharge = classes,
            Account = new Account { Username = user, FullName = name, Email = $"{user}@ifa.local", Role = role, PasswordHash = hash, Active = true },
        };
        var staff = new Dictionary<string, StaffProfile>
        {
            ["admin01"] = New("admin01", "Nguyen Van An", Role.ADMIN, "NV0001"),
            ["manager01"] = New("manager01", "Tran Bich Ngoc", Role.MANAGER, "NV0002"),
            ["staff01"] = New("staff01", "Le Minh Hoang", Role.STAFF, "NV0003", "Oil Painting, Watercolor", "PT01, PT02"),
            ["staff02"] = New("staff02", "Pham Thu Ha", Role.STAFF, "NV0004", "2D Animation", "AN01"),
        };
        db.StaffProfiles.AddRange(staff.Values);
        db.SaveChanges();
        db.Evaluations.Add(new Evaluation { SubmissionId = 1, EvaluatedBy = staff["staff01"].Id, Rating = Rating.GOOD });
        db.SaveChanges();
        return (db, staff);
    }

    /// <summary>Service under test.</summary>
    private static StaffService Service(AppDbContext db) => new(db, NullLogger<StaffService>.Instance);

    /// <summary>Valid create form (TC-M03-01 data).</summary>
    private static CreateStaffVm NewForm(string username = "staff09", string email = "staff09@ifa.local") => new()
    {
        NewUsername = username, Password = "Welcome2026", ConfirmPassword = "Welcome2026",
        FullName = "Dang Thi Mai", Email = email, Role = Role.STAFF, JoinedDate = new DateOnly(2025, 9, 1),
        ClassesInCharge = "Painting K12", SubjectsInCharge = "Watercolor", Phone = "0901234567",
    };

    /// <summary>Edit form copied from an existing profile.</summary>
    private static async Task<StaffFormVm> EditForm(StaffService service, StaffProfile s) => await service.GetForEditAsync(s.Id);

    // ------------------------------------------------------------------ create

    /// <summary>TC-M03-01: account + profile created, username lower-cased, next code NV0005, BCrypt password.</summary>
    [Fact]
    public async Task Create_Valid()
    {
        var (db, _) = CreateDb();
        using var _ = db;
        var form = NewForm("Staff09");

        var id = await Service(db).CreateAsync(form);
        var s = await db.StaffProfiles.Include(x => x.Account).SingleAsync(x => x.Id == id);
        Assert.Equal("staff09", s.Account.Username);
        Assert.Equal("NV0005", s.StaffCode);
        Assert.Equal(Role.STAFF, s.Account.Role);
        Assert.True(s.Account.Active);
        Assert.Equal("Painting K12", s.ClassesInCharge);
        Assert.True(BCrypt.Net.BCrypt.Verify("Welcome2026", s.Account.PasswordHash));
    }

    /// <summary>TC-M03-02: duplicate username (any case) is refused and nothing is created.</summary>
    [Theory]
    [InlineData("staff01")]
    [InlineData("STAFF01")]
    public async Task Create_DuplicateUsername(string username)
    {
        var (db, _) = CreateDb();
        using var _ = db;
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(NewForm(username)));
        Assert.Equal(StaffService.UsernameTakenMessage, ex.Message);
        Assert.Equal(nameof(CreateStaffVm.NewUsername), ex.Field);
        Assert.Equal(4, await db.StaffProfiles.CountAsync());
    }

    /// <summary>Duplicate email (ignoring case) and duplicate staff code are refused (BR-01).</summary>
    [Fact]
    public async Task Create_DuplicateEmailOrCode()
    {
        var (db, _) = CreateDb();
        using var _ = db;
        var service = Service(db);
        Assert.Equal(StaffService.EmailTakenMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(NewForm(email: "STAFF01@ifa.local")))).Message);
        var form = NewForm();
        form.StaffCode = "nv0003";
        Assert.Equal(StaffService.CodeTakenMessage, (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(form))).Message);
    }

    /// <summary>STUDENT role and weak passwords are refused by the service too.</summary>
    [Fact]
    public async Task Create_StudentRoleOrWeakPassword()
    {
        var (db, _) = CreateDb();
        using var _ = db;
        var service = Service(db);
        var student = NewForm();
        student.Role = Role.STUDENT;
        Assert.Equal(StaffService.RoleMessage, (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(student))).Message);
        var weak = NewForm();
        weak.Password = "abc";
        Assert.Equal(nameof(CreateStaffVm.Password), (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(weak))).Field);
    }

    // ------------------------------------------------------------------ search / details

    /// <summary>TC-M03-05: search by part of the name, by subject, by role, by status.</summary>
    [Fact]
    public async Task Search_Filters()
    {
        var (db, staff) = CreateDb();
        using var _ = db;
        staff["staff02"].Account.Active = false;
        db.SaveChanges();
        var service = Service(db);

        async Task<string[]> Users(StaffSearchVm s) => (await service.SearchAsync(s)).Items.Select(i => i.Username).ToArray();
        Assert.Equal(new[] { "manager01" }, await Users(new StaffSearchVm { Keyword = "Ngoc" }));
        Assert.Equal(new[] { "staff01" }, await Users(new StaffSearchVm { Subject = "Watercolor" }));
        Assert.Equal(new[] { "staff02" }, await Users(new StaffSearchVm { ClassName = "AN01" }));
        Assert.Equal(new[] { "staff01", "staff02" }, await Users(new StaffSearchVm { Role = Role.STAFF }));
        Assert.Equal(new[] { "staff02" }, await Users(new StaffSearchVm { Active = false }));
        Assert.Empty(await Users(new StaffSearchVm { Keyword = "nobody" }));
        Assert.Equal(new[] { "admin01", "manager01", "staff01", "staff02" }, await Users(new StaffSearchVm()));   // by code
    }

    /// <summary>Details show related data counts and whether it is the current user.</summary>
    [Fact]
    public async Task Details_RelatedDataAndSelf()
    {
        var (db, staff) = CreateDb();
        using var _ = db;
        var vm = await Service(db).GetDetailsAsync(staff["staff01"].Id, staff["admin01"].AccountId);
        Assert.Equal(1, vm.EvaluationCount);
        Assert.True(vm.HasRelatedData);
        Assert.False(vm.IsCurrentUser);
        Assert.True((await Service(db).GetDetailsAsync(staff["admin01"].Id, staff["admin01"].AccountId)).IsCurrentUser);
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetDetailsAsync(999, 1));
    }

    // ------------------------------------------------------------------ update

    /// <summary>TC-M03-04: edit classes in charge; username unchanged.</summary>
    [Fact]
    public async Task Update_Classes()
    {
        var (db, staff) = CreateDb();
        using var _ = db;
        var service = Service(db);
        var form = await EditForm(service, staff["staff02"]);
        form.ClassesInCharge = "Animation K5";
        form.Username = "hacked";

        await service.UpdateAsync(staff["staff02"].Id, form, staff["admin01"].AccountId);
        var s = await db.StaffProfiles.Include(x => x.Account).SingleAsync(x => x.Id == staff["staff02"].Id);
        Assert.Equal("Animation K5", s.ClassesInCharge);
        Assert.Equal("staff02", s.Account.Username);
    }

    /// <summary>BR-03: an admin cannot change their own role; the last admin cannot be demoted.</summary>
    [Fact]
    public async Task Update_RoleRules()
    {
        var (db, staff) = CreateDb();
        using var _ = db;
        var service = Service(db);
        var own = await EditForm(service, staff["admin01"]);
        own.Role = Role.STAFF;
        Assert.Equal(StaffService.OwnRoleMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(staff["admin01"].Id, own, staff["admin01"].AccountId))).Message);

        // With a second admin (manager01 promoted), admin01 can be demoted by that other admin
        var mgr = await EditForm(service, staff["manager01"]);
        mgr.Role = Role.ADMIN;
        await service.UpdateAsync(staff["manager01"].Id, mgr, staff["admin01"].AccountId);
        var admin = await EditForm(service, staff["admin01"]);
        admin.Role = Role.STAFF;
        await service.UpdateAsync(staff["admin01"].Id, admin, staff["manager01"].AccountId);
        Assert.Equal(Role.STAFF, (await db.Accounts.SingleAsync(a => a.Username == "admin01")).Role);

        // Now manager01 is the only active admin, so nobody can change their role
        var last = await EditForm(service, staff["manager01"]);
        last.Role = Role.MANAGER;
        Assert.Equal(StaffService.LastAdminMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(staff["manager01"].Id, last, staff["staff01"].AccountId))).Message);
    }

    /// <summary>Email of another account is refused on edit; keeping your own email is fine.</summary>
    [Fact]
    public async Task Update_EmailUnique()
    {
        var (db, staff) = CreateDb();
        using var _ = db;
        var service = Service(db);
        var form = await EditForm(service, staff["staff02"]);
        await service.UpdateAsync(staff["staff02"].Id, form, staff["admin01"].AccountId);   // own email unchanged → ok
        form.Email = "staff01@ifa.local";
        Assert.Equal(StaffService.EmailTakenMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(staff["staff02"].Id, form, staff["admin01"].AccountId))).Message);
    }

    // ------------------------------------------------------------------ password / lock / delete

    /// <summary>Reset password stores a new BCrypt hash; weak passwords are refused.</summary>
    [Fact]
    public async Task ResetPassword()
    {
        var (db, staff) = CreateDb();
        using var _ = db;
        var service = Service(db);
        await service.ResetPasswordAsync(staff["staff02"].Id, "TempPass99");
        Assert.True(BCrypt.Net.BCrypt.Verify("TempPass99", (await db.Accounts.SingleAsync(a => a.Username == "staff02")).PasswordHash));
        await Assert.ThrowsAsync<BusinessException>(() => service.ResetPasswordAsync(staff["staff02"].Id, "short"));
    }

    /// <summary>Lock / unlock; not yourself; not the last active admin (BR-03).</summary>
    [Fact]
    public async Task Lock_Rules()
    {
        var (db, staff) = CreateDb();
        using var _ = db;
        var service = Service(db);
        var admin = staff["admin01"].AccountId;

        await service.SetActiveAsync(staff["staff02"].Id, false, admin);
        Assert.False((await db.Accounts.SingleAsync(a => a.Username == "staff02")).Active);
        await service.SetActiveAsync(staff["staff02"].Id, true, admin);
        Assert.True((await db.Accounts.SingleAsync(a => a.Username == "staff02")).Active);

        Assert.Equal(StaffService.SelfMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.SetActiveAsync(staff["admin01"].Id, false, admin))).Message);
        Assert.Equal(StaffService.LastAdminMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.SetActiveAsync(staff["admin01"].Id, false, staff["manager01"].AccountId))).Message);
    }

    /// <summary>BR-04: without related data → deleted; with data (TC-M03-06) → locked, evaluation kept.</summary>
    [Fact]
    public async Task Delete_HardOrDeactivate()
    {
        var (db, staff) = CreateDb();
        using var _ = db;
        var service = Service(db);
        var admin = staff["admin01"].AccountId;

        Assert.Equal(StaffDeleteResult.Deleted, await service.DeleteAsync(staff["staff02"].Id, admin));
        Assert.False(await db.Accounts.AnyAsync(a => a.Username == "staff02"));

        Assert.Equal(StaffDeleteResult.Deactivated, await service.DeleteAsync(staff["staff01"].Id, admin));
        var staff01 = await db.Accounts.SingleAsync(a => a.Username == "staff01");
        Assert.False(staff01.Active);
        Assert.Single(db.Evaluations.Where(e => e.EvaluatedBy == staff["staff01"].Id));
    }

    /// <summary>Deleting yourself or the last admin is refused.</summary>
    [Fact]
    public async Task Delete_SelfOrLastAdmin()
    {
        var (db, staff) = CreateDb();
        using var _ = db;
        var service = Service(db);
        await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(staff["admin01"].Id, staff["admin01"].AccountId));
        Assert.Equal(StaffService.LastAdminMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(staff["admin01"].Id, staff["manager01"].AccountId))).Message);
    }
}

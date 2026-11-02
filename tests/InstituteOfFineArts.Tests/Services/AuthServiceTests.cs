using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// Unit test cho AuthService (task BE1-02): đăng nhập đúng/sai, tài khoản khóa, Claims, trang chính theo vai trò.
/// Dùng CSDL giả trong bộ nhớ (EF Core InMemory) nên không cần SQL Server.
/// </summary>
public class AuthServiceTests
{
    /// <summary>Tên scheme giả dùng khi tạo ClaimsIdentity trong test.</summary>
    private const string Scheme = "TestCookie";

    /// <summary>Mật khẩu chung của các tài khoản test.</summary>
    private const string Password = "Student@123";

    /// <summary>
    /// Tạo AppDbContext dùng CSDL trong bộ nhớ (mỗi test một CSDL riêng) và thêm sẵn 2 học viên + 1 giáo viên.
    /// </summary>
    private static AppDbContext CreateDbWithAccounts()
    {
        // Tên CSDL ngẫu nhiên để các test không ảnh hưởng nhau
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);

        // Băm mật khẩu 1 lần dùng chung cho các tài khoản
        var hash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);

        // Học viên đang hoạt động, học viên bị khóa, giáo viên
        db.StudentProfiles.Add(NewStudent("student01", hash, active: true, code: "HV0001"));
        db.StudentProfiles.Add(NewStudent("student10", hash, active: false, code: "HV0010"));
        db.StaffProfiles.Add(new StaffProfile
        {
            StaffCode = "NV0003",
            JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account
            {
                Username = "staff01", PasswordHash = hash, FullName = "Lê Minh Hoàng",
                Email = "staff01@ifa.local", Role = Role.STAFF, Active = true,
            },
        });
        db.SaveChanges();
        return db;
    }

    /// <summary>Tạo hồ sơ học viên kèm tài khoản cho dữ liệu test.</summary>
    private static StudentProfile NewStudent(string username, string hash, bool active, string code) => new()
    {
        StudentCode = code,
        EnrollmentDate = new DateOnly(2025, 1, 1),
        CourseName = "Hội họa",
        Account = new Account
        {
            Username = username, PasswordHash = hash, FullName = "Nguyễn Minh Khoa",
            Email = $"{username}@ifa.local", Role = Role.STUDENT, Active = active,
        },
    };

    /// <summary>Tạo AuthService với logger rỗng.</summary>
    private static AuthService CreateService(AppDbContext db) => new(db, NullLogger<AuthService>.Instance);

    /// <summary>Đúng tài khoản và mật khẩu → thành công, có đủ Claims, cập nhật last_login_at.</summary>
    [Fact]
    public async Task Login_DungMatKhau_ThanhCongVaCoClaims()
    {
        using var db = CreateDbWithAccounts();
        var service = CreateService(db);

        var result = await service.LoginAsync("student01", Password, Scheme);

        // Kết quả thành công kèm thông tin người dùng
        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.NotNull(result.Principal);
        var user = result.Principal!;
        Assert.Equal("student01", user.Identity!.Name);
        Assert.True(user.IsInRole("STUDENT"));

        // Id tài khoản và Id hồ sơ học viên đọc được bằng hàm tiện ích
        var account = await db.Accounts.Include(a => a.StudentProfile).SingleAsync(a => a.Username == "student01");
        Assert.Equal(account.Id, user.GetAccountId());
        Assert.Equal(account.StudentProfile!.Id, user.GetProfileId());
        Assert.Equal("Nguyễn Minh Khoa", user.GetFullName());

        // Thời điểm đăng nhập đã được ghi lại
        Assert.NotNull(account.LastLoginAt);
    }

    /// <summary>Tên đăng nhập có khoảng trắng thừa vẫn đăng nhập được.</summary>
    [Fact]
    public async Task Login_TenCoKhoangTrang_VanThanhCong()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("  student01 ", Password, Scheme);
        Assert.Equal(LoginStatus.Success, result.Status);
    }

    /// <summary>Sai mật khẩu → báo sai thông tin, không có Principal (TC-M01-03).</summary>
    [Fact]
    public async Task Login_SaiMatKhau_BaoSaiThongTin()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("student01", "sai123", Scheme);
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Principal);
    }

    /// <summary>Tài khoản không tồn tại → cùng kết quả với sai mật khẩu (TC-M01-04).</summary>
    [Fact]
    public async Task Login_TaiKhoanKhongTonTai_GiongSaiMatKhau()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("khongtontai", Password, Scheme);
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }

    /// <summary>Chuỗi SQL injection ở tên đăng nhập → đăng nhập thất bại, không lỗi (TC-M01-06).</summary>
    [Fact]
    public async Task Login_SqlInjection_ThatBai()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("' OR '1'='1' --", "x", Scheme);
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }

    /// <summary>Tài khoản bị khóa, mật khẩu đúng → báo bị khóa, không cho đăng nhập (TC-M01-07).</summary>
    [Fact]
    public async Task Login_TaiKhoanBiKhoa_BaoBiKhoa()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("student10", Password, Scheme);
        Assert.Equal(LoginStatus.Locked, result.Status);
        Assert.Null(result.Principal);
    }

    /// <summary>Tài khoản bị khóa nhưng nhập sai mật khẩu → chỉ báo sai thông tin (không lộ tài khoản đang bị khóa).</summary>
    [Fact]
    public async Task Login_TaiKhoanBiKhoaSaiMatKhau_KhongLoTrangThaiKhoa()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("student10", "sai123", Scheme);
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }

    /// <summary>Giáo viên đăng nhập → ProfileId là id của staff_profile.</summary>
    [Fact]
    public async Task Login_GiaoVien_ProfileIdLaStaffProfile()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("staff01", Password, Scheme);
        var staff = await db.StaffProfiles.SingleAsync(s => s.StaffCode == "NV0003");
        Assert.Equal(staff.Id, result.Principal!.GetProfileId());
    }

    /// <summary>Mỗi vai trò chuyển về đúng trang chính (TC-M01-02).</summary>
    [Theory]
    [InlineData("ADMIN", "/admin")]
    [InlineData("MANAGER", "/manager")]
    [InlineData("STAFF", "/staff")]
    [InlineData("STUDENT", "/student")]
    public void GetHomePath_TheoVaiTro(string role, string expected)
    {
        using var db = CreateDbWithAccounts();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, Scheme));
        Assert.Equal(expected, CreateService(db).GetHomePath(user));
    }
}

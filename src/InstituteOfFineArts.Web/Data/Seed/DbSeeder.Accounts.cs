namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Phần dữ liệu mẫu: tài khoản 4 vai trò (task BE1-01 – làm đầu tiên để thử đăng nhập).
/// </summary>
public static partial class DbSeeder
{
    /// <summary>
    /// Thêm tài khoản mẫu kèm hồ sơ nhân sự / học viên.
    /// </summary>
    /// <param name="db">DbContext dùng để thêm dữ liệu</param>
    private static async Task SeedAccountsAsync(AppDbContext db)
    {
        // TODO (BE1-01): theo docs/plan/04 mục 4.1 và 4.5
        //   (1) admin01, manager01, staff01–staff03 kèm StaffProfile
        //   (2) student01–student10 kèm StudentProfile; student10 có Active = false
        //   (3) mật khẩu băm bằng BCrypt.Net.BCrypt.HashPassword(...)
        await db.SaveChangesAsync();
    }
}

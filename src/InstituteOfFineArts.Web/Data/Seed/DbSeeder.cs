using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Tạo CSDL và dữ liệu mẫu khi chạy ở môi trường Development (được gọi sẵn trong Program.cs).
/// Lớp được tách thành nhiều file (partial) để 2 người viết song song không đụng nhau:
/// <list type="bullet">
/// <item><c>DbSeeder.Accounts.cs</c> – tài khoản mẫu (task BE1-01)</item>
/// <item><c>DbSeeder.SampleData.cs</c> – cuộc thi, bài nộp, chấm, giải, triển lãm (task BE2-01)</item>
/// </list>
/// </summary>
public static partial class DbSeeder
{
    /// <summary>
    /// Áp dụng migration còn thiếu, sau đó thêm dữ liệu mẫu nếu CSDL còn trống.
    /// </summary>
    /// <param name="services">Service provider của ứng dụng để lấy AppDbContext</param>
    public static async Task SeedAsync(IServiceProvider services)
    {
        // Tạo scope riêng vì AppDbContext được đăng ký dạng Scoped
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Tạo CSDL (nếu chưa có) và áp dụng các migration mới
        await db.Database.MigrateAsync();

        // Bước 1: tài khoản (chỉ thêm khi bảng account còn trống)
        if (!await db.Accounts.AnyAsync())
        {
            await SeedAccountsAsync(db);
        }

        // Bước 2: dữ liệu nghiệp vụ (chỉ thêm khi chưa có cuộc thi nào)
        if (!await db.Competitions.AnyAsync())
        {
            await SeedSampleDataAsync(db);
        }
    }
}

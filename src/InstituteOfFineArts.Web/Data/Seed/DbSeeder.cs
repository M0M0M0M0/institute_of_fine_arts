using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Tạo CSDL và dữ liệu mẫu khi chạy ở môi trường Development.
/// Được gọi sẵn trong Program.cs – người phụ trách (task BE2-01) chỉ cần viết phần thêm dữ liệu trong file này.
/// </summary>
public static class DbSeeder
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

        // Đã có tài khoản thì coi như đã seed, không thêm lại
        if (await db.Accounts.AnyAsync())
        {
            return;
        }

        // TODO (BE2-01): thêm dữ liệu mẫu theo docs/plan/04 mục 4
        //   - 15 tài khoản (mật khẩu băm bằng BCrypt.Net.BCrypt.HashPassword)
        //   - 7 cuộc thi với ngày tương đối theo DateTimeHelper.Today
        //   - 27 bài nộp + chép ảnh từ database/seed-images vào thư mục uploads
        //   - đánh giá, giải đã trao, 2 triển lãm / 8 tranh, 3 khách mua
        //   - gọi await db.SaveChangesAsync() sau mỗi nhóm dữ liệu
    }
}

using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Data.Seed;
using InstituteOfFineArts.Web.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ===== Đăng ký dịch vụ (Dependency Injection) =====

// MVC: Controller + Razor View
builder.Services.AddControllersWithViews();

// CSDL: EF Core + SQL Server; tên bảng/cột tự chuyển sang snake_case (account, password_hash…)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// Service nghiệp vụ: tự đăng ký mọi lớp XxxService trong Services/Implementations
// → thêm Service mới KHÔNG cần sửa file này
builder.Services.AddApplicationServices();

// Job chạy nền: tự đăng ký mọi BackgroundService trong Infrastructure/BackgroundJobs
builder.Services.AddBackgroundJobs();

// TODO (M01): đăng ký Cookie Authentication – xem docs/plan/02 mục 7.1

var app = builder.Build();

// ===== Cấu hình pipeline xử lý request =====

// Môi trường phát triển: tạo CSDL + dữ liệu mẫu (Data/Seed/DbSeeder.cs)
// Môi trường thật: trang lỗi thân thiện + HSTS
if (app.Environment.IsDevelopment())
{
    await DbSeeder.SeedAsync(app.Services);
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// TODO (M01): app.UseAuthentication(); – phải đặt trước UseAuthorization
app.UseAuthorization();

// File tĩnh trong wwwroot (css, js, lib, img)
app.MapStaticAssets();

// Route cho các Area (Admin, Staff, Student, Manager) – đặt trước route mặc định
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

// Route mặc định cho các controller dùng chung (Home, Account, Files…)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Data.Seed;
using InstituteOfFineArts.Web.Infrastructure.DependencyInjection;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

// ===== Đăng ký dịch vụ (Dependency Injection) =====

// MVC: Controller + Razor View; bật kiểm tra anti-forgery token cho mọi form POST (chống CSRF)
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Giữ nguyên chữ tiếng Việt có dấu khi Razor xuất HTML (mặc định bị đổi thành &#x...;).
// Các ký tự nguy hiểm < > & " ' vẫn được mã hóa nên vẫn chống XSS.
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Đăng nhập bằng cookie (M01) – thiết kế: docs/plan/02 mục 7.1
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Chưa đăng nhập → về trang đăng nhập; sai vai trò → trang 403
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/error/403";

        // Hết hạn sau 30 phút không hoạt động; mỗi lần dùng sẽ gia hạn
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;

        // Cookie chỉ server đọc được (chống đánh cắp bằng JavaScript)
        options.Cookie.Name = "IFA.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// CSDL: EF Core + SQL Server; tên bảng/cột tự chuyển sang snake_case (account, password_hash…)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// Service nghiệp vụ: tự đăng ký mọi lớp XxxService trong Services/Implementations
// → thêm Service mới KHÔNG cần sửa file này
builder.Services.AddApplicationServices();

// Job chạy nền: tự đăng ký mọi BackgroundService trong Infrastructure/BackgroundJobs
builder.Services.AddBackgroundJobs();

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

// Đọc cookie đăng nhập → User; phải đặt trước UseAuthorization
app.UseAuthentication();
// Kiểm tra [Authorize(Roles = ...)] của controller
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

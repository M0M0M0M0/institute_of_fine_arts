using System.Reflection;

namespace InstituteOfFineArts.Web.Infrastructure.DependencyInjection;

/// <summary>
/// Tự động đăng ký các Service vào Dependency Injection, để khi thêm Service mới
/// không ai phải sửa Program.cs (tránh xung đột Git giữa các thành viên).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Quét namespace <c>InstituteOfFineArts.Web.Services.Implementations</c>:
    /// mỗi lớp <c>XxxService</c> cài đặt interface <c>IXxxService</c> sẽ được đăng ký dạng Scoped.
    /// </summary>
    /// <param name="services">Danh sách dịch vụ của ứng dụng</param>
    /// <returns>Chính <paramref name="services"/> để gọi nối tiếp</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        const string implementationNamespace = "InstituteOfFineArts.Web.Services.Implementations";

        // Lấy mọi lớp cụ thể (không abstract) nằm trong namespace Implementations
        var implementations = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.Namespace != null
                        && t.Namespace.StartsWith(implementationNamespace));

        foreach (var implementation in implementations)
        {
            // Chỉ đăng ký theo quy ước tên: XxxService ↔ IXxxService
            var contract = implementation.GetInterfaces()
                .FirstOrDefault(i => i.Name == "I" + implementation.Name);

            if (contract != null)
            {
                services.AddScoped(contract, implementation);
            }
        }

        return services;
    }

    /// <summary>
    /// Tự động đăng ký các job chạy nền: mọi lớp kế thừa <see cref="BackgroundService"/>
    /// trong namespace <c>InstituteOfFineArts.Web.Infrastructure.BackgroundJobs</c>.
    /// </summary>
    /// <param name="services">Danh sách dịch vụ của ứng dụng</param>
    /// <returns>Chính <paramref name="services"/> để gọi nối tiếp</returns>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        const string jobNamespace = "InstituteOfFineArts.Web.Infrastructure.BackgroundJobs";

        // Tìm các lớp job cụ thể kế thừa BackgroundService
        var jobs = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.Namespace == jobNamespace
                        && typeof(BackgroundService).IsAssignableFrom(t));

        // Đăng ký từng job như một hosted service (chạy cùng ứng dụng)
        foreach (var job in jobs)
        {
            services.AddSingleton(typeof(IHostedService), job);
        }

        return services;
    }
}

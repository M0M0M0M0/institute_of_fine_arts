using System.Reflection;

namespace InstituteOfFineArts.Web.Infrastructure.DependencyInjection;

/// <summary>
/// Registers services in Dependency Injection automatically, so adding a new service
/// never requires editing Program.cs (avoids Git conflicts between team members).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Scans namespace <c>InstituteOfFineArts.Web.Services.Implementations</c>:
    /// every class <c>XxxService</c> implementing interface <c>IXxxService</c> is registered as Scoped.
    /// </summary>
    /// <param name="services">Application service collection</param>
    /// <returns>The same <paramref name="services"/> for chaining</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        const string implementationNamespace = "InstituteOfFineArts.Web.Services.Implementations";

        // All concrete (non-abstract) classes in the Implementations namespace
        var implementations = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.Namespace != null
                        && t.Namespace.StartsWith(implementationNamespace));

        foreach (var implementation in implementations)
        {
            // Register only by naming convention: XxxService ↔ IXxxService
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
    /// Registers background jobs automatically: every class deriving from <see cref="BackgroundService"/>
    /// in namespace <c>InstituteOfFineArts.Web.Infrastructure.BackgroundJobs</c>.
    /// </summary>
    /// <param name="services">Application service collection</param>
    /// <returns>The same <paramref name="services"/> for chaining</returns>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        const string jobNamespace = "InstituteOfFineArts.Web.Infrastructure.BackgroundJobs";

        // Find concrete job classes deriving from BackgroundService
        var jobs = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.Namespace == jobNamespace
                        && typeof(BackgroundService).IsAssignableFrom(t));

        // Register each job as a hosted service (runs together with the application)
        foreach (var job in jobs)
        {
            services.AddSingleton(typeof(IHostedService), job);
        }

        return services;
    }
}

using InstituteOfFineArts.Web.Services.Interfaces;

namespace InstituteOfFineArts.Web.Infrastructure.BackgroundJobs;

/// <summary>
/// Hourly job (task BE2-05, CN37): runs <see cref="IStatusSyncService"/> once at start-up and then every hour.
/// Registered automatically by <c>AddBackgroundJobs</c> because it lives in this namespace.
/// </summary>
public class StatusSyncBackgroundService : BackgroundService
{
    /// <summary>Time between two runs.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>Creates a DI scope per run (the service and AppDbContext are scoped, the job is a singleton).</summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Logs failures and shutdown.</summary>
    private readonly ILogger<StatusSyncBackgroundService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public StatusSyncBackgroundService(IServiceScopeFactory scopeFactory, ILogger<StatusSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            // First run right away, then wait for each tick of the timer
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown of the application
            _logger.LogInformation("Status sync job stopped");
        }
    }

    /// <summary>One synchronisation in its own DI scope; errors are logged so the next run still happens.</summary>
    /// <param name="stoppingToken">Cancelled when the application shuts down</param>
    public async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var sync = scope.ServiceProvider.GetRequiredService<IStatusSyncService>();
            await sync.SyncAsync(cancellationToken: stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown during a run: let ExecuteAsync stop the loop
            throw;
        }
        catch (Exception ex)
        {
            // e.g. database not reachable: log and try again at the next tick
            _logger.LogError(ex, "Status sync failed; it will run again in {Interval}", Interval);
        }
    }
}

using InstituteOfFineArts.Web.Services.Interfaces;

namespace InstituteOfFineArts.Web.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily job (optional feature M12, F-M12-01; tasks BE1-OPT 3 + BE2-OPT 5): runs <see cref="IDeadlineReminderService"/>
/// once at start-up and then once a day. Running again after a restart is harmless because the service never reminds
/// a student twice about the same competition.
/// Registered automatically by <c>AddBackgroundJobs</c> because it lives in this namespace.
/// </summary>
public class DeadlineReminderBackgroundService : BackgroundService
{
    /// <summary>Time between two runs.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    /// <summary>Creates a DI scope per run (the services and AppDbContext are scoped, the job is a singleton).</summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Logs failures and shutdown.</summary>
    private readonly ILogger<DeadlineReminderBackgroundService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public DeadlineReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<DeadlineReminderBackgroundService> logger)
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
            _logger.LogInformation("Deadline reminder job stopped");
        }
    }

    /// <summary>One run in its own DI scope; errors are logged so the next run still happens.</summary>
    /// <param name="stoppingToken">Cancelled when the application shuts down</param>
    public async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var reminders = scope.ServiceProvider.GetRequiredService<IDeadlineReminderService>();
            await reminders.SendRemindersAsync(cancellationToken: stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown during a run: let ExecuteAsync stop the loop
            throw;
        }
        catch (Exception ex)
        {
            // e.g. database not reachable: log and try again at the next tick
            _logger.LogError(ex, "Deadline reminders failed; the job will run again in {Interval}", Interval);
        }
    }
}

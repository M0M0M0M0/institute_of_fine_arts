namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Automatic status update (task BE2-05, CN37, BR-07, BR-42): writes the status calculated from the dates
/// into the stored <c>status</c> columns of competitions and exhibitions, and closes the entries of finished exhibitions.
/// Called by the hourly background job; safe to run any number of times.
/// </summary>
public interface IStatusSyncService
{
    /// <summary>Synchronises all statuses and returns how many rows were changed.</summary>
    /// <param name="today">Current date (default: today in the institute time zone); tests pass a fixed date</param>
    /// <param name="cancellationToken">Stops the work when the application shuts down</param>
    Task<StatusSyncResult> SyncAsync(DateOnly? today = null, CancellationToken cancellationToken = default);
}

/// <summary>Number of rows changed by one synchronisation run.</summary>
/// <param name="Competitions">Competitions whose stored status changed</param>
/// <param name="Exhibitions">Exhibitions whose stored status changed</param>
/// <param name="EntriesUnsold">ACCEPTED entries of finished exhibitions moved to UNSOLD</param>
public record StatusSyncResult(int Competitions, int Exhibitions, int EntriesUnsold)
{
    /// <summary>Total number of changed rows.</summary>
    public int Total => Competitions + Exhibitions + EntriesUnsold;
}

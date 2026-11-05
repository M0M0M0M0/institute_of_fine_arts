using InstituteOfFineArts.Web.ViewModels.Admin;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Admin – dashboard numbers (task BE2-04): staff accounts, students, competitions, submissions,
/// exhibitions and the most recent logins.
/// </summary>
public interface IAdminDashboardService
{
    /// <summary>Builds all dashboard numbers. Competition and exhibition statuses are calculated from the dates.</summary>
    /// <param name="today">Current date (default: today in the institute time zone); tests pass a fixed date</param>
    Task<AdminDashboardVm> GetAsync(DateOnly? today = null);
}

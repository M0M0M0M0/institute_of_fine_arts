using InstituteOfFineArts.Web.ViewModels.Manager;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Manager – dashboard numbers (task BE2-13; F-M11-01, US-15 AC3): students, staff, competitions by status,
/// submissions, awards issued, paintings sold and total sales. Read only.
/// </summary>
public interface IManagerDashboardService
{
    /// <summary>Builds all dashboard numbers. Competition statuses are calculated from the dates.</summary>
    /// <param name="today">Current date (default: today in the institute time zone); tests pass a fixed date</param>
    Task<ManagerDashboardVm> GetAsync(DateOnly? today = null);
}

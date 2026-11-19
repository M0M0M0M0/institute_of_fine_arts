using InstituteOfFineArts.Web.ViewModels.Staff;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Staff – dashboard (/staff; task BE2-13, F-M11-01, US-15 AC3): the work waiting, students, staff and teachers,
/// competitions by status, submissions, awards issued, paintings sold and total sales.
/// </summary>
public interface IStaffDashboardService
{
    /// <summary>Builds all dashboard numbers. Competition statuses are calculated from the dates.</summary>
    /// <param name="today">Current date (default: today in the institute time zone); tests pass a fixed date</param>
    /// <param name="period">Range of the activity figures (submissions, awards, sales); All = since the start</param>
    Task<StaffDashboardVm> GetAsync(DateOnly? today = null, DashboardPeriod period = DashboardPeriod.All);
}

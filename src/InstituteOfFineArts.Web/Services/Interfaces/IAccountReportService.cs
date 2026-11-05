using InstituteOfFineArts.Web.ViewModels.Reports;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Account statistics report (task BE2-12; F-M10-07; BA §7 RPT-07): accounts per role and status,
/// students per course / class and enrollment status. Read-only.
/// </summary>
public interface IAccountReportService
{
    /// <summary>RPT-07 with the given filters.</summary>
    /// <param name="search">Filters</param>
    Task<AccountStatisticsReportVm> GetAccountStatisticsAsync(AccountStatisticsSearchVm search);
}

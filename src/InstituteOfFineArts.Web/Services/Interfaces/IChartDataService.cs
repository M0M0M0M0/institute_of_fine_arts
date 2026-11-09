using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.ViewModels.Charts;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Data of the dashboard charts (task BE2-OPT 2; F-M15-01): submissions per competition, rating distribution and
/// sales per exhibition. Read only; competition / exhibition statuses come from CompetitionStatusResolver.
/// </summary>
public interface IChartDataService
{
    /// <summary>Submissions and evaluated submissions per competition, oldest competition first.</summary>
    /// <param name="status">Only competitions with this actual status (null = all)</param>
    /// <param name="today">Day used for the status (default: today)</param>
    Task<ChartDataVm> GetSubmissionsByCompetitionAsync(CompetitionStatus? status = null, DateOnly? today = null);

    /// <summary>Number of submissions per rating plus "Not graded". Unknown competition → NotFoundException.</summary>
    /// <param name="competitionId">Only this competition (null = all)</param>
    /// <param name="status">Only competitions with this actual status (null = all)</param>
    /// <param name="today">Day used for the status (default: today)</param>
    Task<ChartDataVm> GetRatingsAsync(long? competitionId = null, CompetitionStatus? status = null, DateOnly? today = null);

    /// <summary>Revenue (sum of sold prices) and number of paintings sold per exhibition, oldest exhibition first.</summary>
    /// <param name="status">Only exhibitions with this actual status (null = all)</param>
    /// <param name="today">Day used for the status (default: today)</param>
    Task<ChartDataVm> GetSalesByExhibitionAsync(ExhibitionStatus? status = null, DateOnly? today = null);
}

using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// Derives the status of a competition / exhibition from its dates (BR-07, BR-42).
/// Every place that needs to know "is this competition still accepting entries" uses this class,
/// so the rule is calculated in exactly one way.
/// </summary>
public static class CompetitionStatusResolver
{
    /// <summary>
    /// Actual status of the competition on <paramref name="today"/>.
    /// GRADED is set by staff when publishing results, so it is kept; the other statuses come from the dates.
    /// </summary>
    /// <param name="competition">Competition to evaluate</param>
    /// <param name="today">Current date (default: today in the institute time zone)</param>
    public static CompetitionStatus Resolve(Competition competition, DateOnly? today = null)
    {
        var day = today ?? DateTimeHelper.Today;

        // Results already published: never recalculate
        if (competition.Status == CompetitionStatus.GRADED)
        {
            return CompetitionStatus.GRADED;
        }

        // Before the start date → upcoming
        if (day < competition.StartDate)
        {
            return CompetitionStatus.UPCOMING;
        }

        // From the start date until the end of the end date (23:59:59) → ongoing
        return day <= competition.EndDate ? CompetitionStatus.ONGOING : CompetitionStatus.CLOSED;
    }

    /// <summary>
    /// Submission deadline of a competition: 23:59:59 on its end date, institute time (BR-06).
    /// </summary>
    /// <param name="endDate">Last day of the competition</param>
    public static DateTime SubmissionDeadline(DateOnly endDate) => endDate.ToDateTime(new TimeOnly(23, 59, 59));

    /// <summary>
    /// True when an entry was submitted after the deadline, i.e. on or after 00:00 of the day after the end date
    /// (BR-14, TC-M07-04). Used by the staff submission pages and the evaluation rules.
    /// </summary>
    /// <param name="submittedAt">Submission time (institute time zone)</param>
    /// <param name="endDate">Last day of the competition</param>
    public static bool IsLateSubmission(DateTime submittedAt, DateOnly endDate)
        => submittedAt >= DateTimeHelper.StartOfNextDay(endDate);

    /// <summary>Whether the competition still allows submitting / editing / deleting entries (BR-13).</summary>
    public static bool IsOpenForSubmission(Competition competition, DateOnly? today = null)
        => Resolve(competition, today) == CompetitionStatus.ONGOING;

    /// <summary>Actual status of the exhibition on <paramref name="today"/>.</summary>
    /// <param name="exhibition">Exhibition to evaluate</param>
    /// <param name="today">Current date (default: today in the institute time zone)</param>
    public static ExhibitionStatus Resolve(Exhibition exhibition, DateOnly? today = null)
    {
        var day = today ?? DateTimeHelper.Today;

        // Compare today with the exhibition date range
        if (day < exhibition.StartDate)
        {
            return ExhibitionStatus.UPCOMING;
        }

        return day <= exhibition.EndDate ? ExhibitionStatus.ONGOING : ExhibitionStatus.FINISHED;
    }
}

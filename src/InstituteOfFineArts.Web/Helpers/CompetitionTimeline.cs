using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Helpers;

/// <summary>Where a milestone stands compared with today.</summary>
public enum MilestoneState
{
    /// <summary>Already happened.</summary>
    Past,

    /// <summary>The latest milestone that has happened (where the competition is now).</summary>
    Current,

    /// <summary>Still to come.</summary>
    Upcoming,
}

/// <summary>One line of a competition's timeline.</summary>
/// <param name="Date">Day</param>
/// <param name="Title">Title</param>
/// <param name="Content">Optional text</param>
/// <param name="IsSystem">True for the dates the system works with (submissions open / close, results published)</param>
/// <param name="State">Past, current or upcoming</param>
public record TimelineItem(DateOnly Date, string Title, string? Content, bool IsSystem, MilestoneState State);

/// <summary>
/// The timeline of a competition (step 7, 11/2026): the system dates – submissions open (start date), submissions
/// close (end date, 23:59:59), results published (when published) – merged with the milestones written by the staff,
/// in date order. The "current" item is the last one on or before today.
/// </summary>
public static class CompetitionTimeline
{
    /// <summary>Title of the start date.</summary>
    public const string OpenTitle = "Submissions open";

    /// <summary>Title of the end date.</summary>
    public const string CloseTitle = "Submissions close";

    /// <summary>Title of the publish date.</summary>
    public const string PublishedTitle = "Results published";

    /// <summary>Builds the timeline.</summary>
    /// <param name="start">Start date</param>
    /// <param name="end">End date (last day to submit)</param>
    /// <param name="publishedAt">When the results were published, or null</param>
    /// <param name="milestones">Milestones written by the staff</param>
    /// <param name="today">Today in the institute time zone</param>
    public static IReadOnlyList<TimelineItem> Build(DateOnly start, DateOnly end, DateTime? publishedAt,
        IEnumerable<CompetitionMilestone> milestones, DateOnly today)
    {
        var rows = new List<(DateOnly Date, int Order, string Title, string? Content, bool System)>
        {
            (start, -1, OpenTitle, "Students can submit their entry from this day.", true),
            // The deadline is the end of the day, so a milestone written for the same day comes before it
            (end, int.MaxValue, CloseTitle, "Last day to submit or edit an entry (23:59:59). The jury grades after it.", true),
        };
        if (publishedAt is { } published)
        {
            rows.Add((DateOnly.FromDateTime(published), int.MaxValue, PublishedTitle, "Ratings, comments and awards are public.", true));
        }
        rows.AddRange(milestones.Select(m => (m.Date, m.SortOrder, m.Title, m.Content, false)));

        var ordered = rows.OrderBy(r => r.Date).ThenBy(r => r.Order).ToList();
        var current = ordered.FindLastIndex(r => r.Date <= today);
        return ordered.Select((r, i) => new TimelineItem(r.Date, r.Title, string.IsNullOrWhiteSpace(r.Content) ? null : r.Content,
                r.System, i < current ? MilestoneState.Past : i == current ? MilestoneState.Current : MilestoneState.Upcoming))
            .ToList();
    }

    /// <summary>Timeline of a loaded competition (its milestones must be loaded).</summary>
    public static IReadOnlyList<TimelineItem> Of(Competition c, DateOnly today)
        => Build(c.StartDate, c.EndDate, c.Status == CompetitionStatus.GRADED ? c.PublishedAt : null, c.Milestones, today);
}

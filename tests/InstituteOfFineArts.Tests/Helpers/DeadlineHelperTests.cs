using InstituteOfFineArts.Web.Helpers;

namespace InstituteOfFineArts.Tests.Helpers;

/// <summary>
/// Shared deadline and date-range helpers used by the student, staff and evaluation pages (BR-06, BR-14, TC-M10-02).
/// </summary>
public class DeadlineHelperTests
{
    /// <summary>The deadline is 23:59:59 on the end date.</summary>
    [Fact]
    public void SubmissionDeadline_IsEndOfLastDay()
    {
        Assert.Equal(new DateTime(2026, 11, 14, 23, 59, 59), CompetitionStatusResolver.SubmissionDeadline(new DateOnly(2026, 11, 14)));
    }

    /// <summary>Submitting at 23:59:59 on the end date is on time; from 00:00 the next day it is late.</summary>
    [Fact]
    public void IsLateSubmission_Boundary()
    {
        var end = new DateOnly(2026, 11, 14);
        Assert.False(CompetitionStatusResolver.IsLateSubmission(new DateTime(2026, 11, 14, 23, 59, 59), end));
        Assert.True(CompetitionStatusResolver.IsLateSubmission(new DateTime(2026, 11, 15, 0, 0, 0), end));
        Assert.False(CompetitionStatusResolver.IsLateSubmission(new DateTime(2026, 11, 1, 9, 0, 0), end));
    }

    /// <summary>Date filters include the whole "To" day: [From 00:00, To+1 00:00).</summary>
    [Fact]
    public void DateRangeBounds()
    {
        var day = new DateOnly(2026, 11, 4);
        Assert.Equal(new DateTime(2026, 11, 4, 0, 0, 0), DateTimeHelper.StartOfDay(day));
        Assert.Equal(new DateTime(2026, 11, 5, 0, 0, 0), DateTimeHelper.StartOfNextDay(day));
    }
}

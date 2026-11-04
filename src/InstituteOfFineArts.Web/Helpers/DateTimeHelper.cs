namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// Returns the date/time in the institute's time zone (Vietnam, UTC+7) so that the
/// 23:59:59 submission deadline is evaluated on the correct day regardless of the server time zone.
/// </summary>
public static class DateTimeHelper
{
    /// <summary>Institute time zone (UTC+7).</summary>
    private static readonly TimeZoneInfo VietnamZone = FindVietnamZone();

    /// <summary>Current time in the institute time zone.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone);

    /// <summary>Current date in the institute time zone.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>00:00 of the given day – inclusive lower bound of a date filter.</summary>
    /// <param name="day">Day</param>
    public static DateTime StartOfDay(DateOnly day) => day.ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// 00:00 of the day after the given day – exclusive upper bound, so a "To" date filter includes the whole day
    /// up to 23:59:59 (TC-M10-02).
    /// </summary>
    /// <param name="day">Day</param>
    public static DateTime StartOfNextDay(DateOnly day) => day.AddDays(1).ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// Finds the institute time zone: tries the IANA id first, then falls back to the Windows id.
    /// </summary>
    private static TimeZoneInfo FindVietnamZone()
    {
        // .NET 6+ understands IANA ids on Windows as well (when ICU is available)
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Ho_Chi_Minh", out var zone))
        {
            return zone;
        }

        // Fallback: the Windows name of the UTC+7 time zone
        return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    }
}

namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// Lấy ngày giờ theo múi giờ Việt Nam để hạn nộp 23:59:59 tính đúng ngày,
/// không phụ thuộc múi giờ của máy chủ.
/// </summary>
public static class DateTimeHelper
{
    /// <summary>Múi giờ Việt Nam (UTC+7).</summary>
    private static readonly TimeZoneInfo VietnamZone = FindVietnamZone();

    /// <summary>Thời điểm hiện tại theo giờ Việt Nam.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone);

    /// <summary>Ngày hiện tại theo giờ Việt Nam.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>
    /// Tìm múi giờ Việt Nam: thử tên chuẩn IANA trước, nếu không có thì dùng tên của Windows.
    /// </summary>
    private static TimeZoneInfo FindVietnamZone()
    {
        // .NET 6+ hiểu tên IANA trên cả Windows (khi có ICU)
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Ho_Chi_Minh", out var zone))
        {
            return zone;
        }

        // Dự phòng: tên múi giờ UTC+7 trên Windows
        return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    }
}

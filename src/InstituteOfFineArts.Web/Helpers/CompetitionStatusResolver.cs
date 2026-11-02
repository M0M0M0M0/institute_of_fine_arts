using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// Tính trạng thái cuộc thi / triển lãm theo ngày (BR-07, BR-42).
/// Mọi nơi cần biết "cuộc thi còn nhận bài không" đều dùng lớp này để thống nhất một cách tính.
/// </summary>
public static class CompetitionStatusResolver
{
    /// <summary>
    /// Trạng thái thực tế của cuộc thi tại ngày <paramref name="today"/>.
    /// GRADED do giáo viên công bố nên được giữ nguyên; các trạng thái còn lại tính từ ngày.
    /// </summary>
    /// <param name="competition">Cuộc thi cần tính</param>
    /// <param name="today">Ngày hiện tại (mặc định: hôm nay theo giờ Việt Nam)</param>
    public static CompetitionStatus Resolve(Competition competition, DateOnly? today = null)
    {
        var day = today ?? DateTimeHelper.Today;

        // Đã công bố kết quả thì không tính lại
        if (competition.Status == CompetitionStatus.GRADED)
        {
            return CompetitionStatus.GRADED;
        }

        // Trước ngày bắt đầu → sắp diễn ra
        if (day < competition.StartDate)
        {
            return CompetitionStatus.UPCOMING;
        }

        // Từ ngày bắt đầu đến hết ngày kết thúc (23:59:59) → đang diễn ra
        return day <= competition.EndDate ? CompetitionStatus.ONGOING : CompetitionStatus.CLOSED;
    }

    /// <summary>Cuộc thi còn cho phép nộp / sửa / xóa bài không (BR-13).</summary>
    public static bool IsOpenForSubmission(Competition competition, DateOnly? today = null)
        => Resolve(competition, today) == CompetitionStatus.ONGOING;

    /// <summary>Trạng thái thực tế của triển lãm tại ngày <paramref name="today"/>.</summary>
    /// <param name="exhibition">Triển lãm cần tính</param>
    /// <param name="today">Ngày hiện tại (mặc định: hôm nay theo giờ Việt Nam)</param>
    public static ExhibitionStatus Resolve(Exhibition exhibition, DateOnly? today = null)
    {
        var day = today ?? DateTimeHelper.Today;

        // So sánh ngày hiện tại với khoảng ngày diễn ra triển lãm
        if (day < exhibition.StartDate)
        {
            return ExhibitionStatus.UPCOMING;
        }

        return day <= exhibition.EndDate ? ExhibitionStatus.ONGOING : ExhibitionStatus.FINISHED;
    }
}

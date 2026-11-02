namespace InstituteOfFineArts.Web.Exceptions;

/// <summary>
/// Lỗi vi phạm luật nghiệp vụ (ví dụ: "Đã quá hạn nộp bài").
/// Service ném lỗi này; Controller bắt và hiển thị thông báo cho người dùng.
/// </summary>
public class BusinessException : Exception
{
    /// <summary>Tạo lỗi nghiệp vụ với thông báo tiếng Việt hiển thị cho người dùng.</summary>
    public BusinessException(string message) : base(message)
    {
    }
}

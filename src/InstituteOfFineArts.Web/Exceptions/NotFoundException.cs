namespace InstituteOfFineArts.Web.Exceptions;

/// <summary>
/// Không tìm thấy dữ liệu (ví dụ: id cuộc thi không tồn tại). Controller trả về trang 404.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>Tạo lỗi không tìm thấy với thông báo mô tả.</summary>
    public NotFoundException(string message) : base(message)
    {
    }
}

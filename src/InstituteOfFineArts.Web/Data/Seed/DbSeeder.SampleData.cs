namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Phần dữ liệu mẫu: cuộc thi, bài nộp, đánh giá, giải, triển lãm, khách mua (task BE2-01).
/// Chạy sau SeedAccountsAsync nên đã có sẵn tài khoản giáo viên và học viên.
/// </summary>
public static partial class DbSeeder
{
    /// <summary>
    /// Thêm dữ liệu nghiệp vụ mẫu để các module làm việc song song mà không phải chờ nhau.
    /// </summary>
    /// <param name="db">DbContext dùng để thêm dữ liệu</param>
    private static async Task SeedSampleDataAsync(AppDbContext db)
    {
        // TODO (BE2-01): theo docs/plan/04 mục 4.2 – 4.4
        //   (1) 7 cuộc thi, ngày tương đối theo DateTimeHelper.Today, kèm giải thưởng
        //   (2) 27 bài nộp; chép ảnh từ database/seed-images vào thư mục uploads
        //   (3) đánh giá đủ 6 xếp loại; giải đã trao; công bố các cuộc thi GRADED
        //   (4) 3 khách mua, 2 triển lãm / 8 tranh
        //   Gọi await db.SaveChangesAsync() sau mỗi nhóm dữ liệu
        await db.SaveChangesAsync();
    }
}

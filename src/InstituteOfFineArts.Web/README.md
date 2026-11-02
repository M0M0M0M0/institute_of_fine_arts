# InstituteOfFineArts.Web – cấu trúc thư mục

| Thư mục | Nội dung | Module | Phụ trách |
|---|---|---|---|
| `Program.cs` | Cấu hình DI, middleware, xác thực cookie, route Areas | – | BE |
| `Areas/Admin` | Controller + View phân hệ Quản trị viên: quản lý nhân viên, học viên | M03, M04 | BE + FE |
| `Areas/Staff` | Phân hệ Giáo viên: cuộc thi, chấm bài, trao giải, triển lãm, bán tranh | M05, M07–M09 | BE + FE |
| `Areas/Student` | Phân hệ Học viên: nộp bài, kết quả, triển lãm của tôi | M06 | BE + FE |
| `Areas/Manager` | Phân hệ Quản lý: các màn hình xem, quản lý triển lãm | M11 | FE + BE |
| `Controllers/` | Controller dùng chung: Home (M02), Account (M01), Profile, Files (tải ảnh), Reports (M10), Error | M01, M02, M10 | BE |
| `Views/` | View dùng chung; `Shared/` chứa `_Layout.cshtml`, partial header/sidebar/footer | – | FE |
| `Models/Entities` | 11 entity: Account, StaffProfile, StudentProfile, Competition, CompetitionAward, Submission, Evaluation, AwardIssued, Exhibition, ExhibitionEntry, Customer (+ BaseEntity) | – | BE |
| `Models/Enums` | Role, Gender, EnrollmentStatus, CompetitionStatus, TextType, Rating, ExhibitionStatus, EntryStatus | – | BE |
| `ViewModels/` | `XxxFormVm` (dữ liệu nhập, có DataAnnotations), `XxxVm` (dữ liệu hiển thị) | – | BE + FE |
| `Data/` | `AppDbContext`, `Configurations/` (cấu hình EF từng bảng), `Migrations/` (dotnet ef), `Seed/` (DbSeeder) | – | BE |
| `Services/` | `Interfaces/` + `Implementations/`: luật nghiệp vụ BR-xx | – | BE |
| `Infrastructure/FileStorage` | Lưu/đọc/xóa ảnh bằng File I/O, tạo thumbnail | M06 | BE |
| `Infrastructure/Security` | Cấu hình cookie, hàm lấy Id người dùng từ Claims | M01 | BE |
| `Infrastructure/BackgroundJobs` | `StatusSyncBackgroundService` – tự đổi trạng thái cuộc thi/triển lãm theo ngày | M05, M09 | BE |
| `Helpers/` | JpegValidator, DateTimeHelper, CompetitionStatusResolver, Constants | – | BE |
| `Validation/` | Attribute tự viết: `[DateRange]`, `[JpegFile]` | – | BE |
| `Exceptions/` | BusinessException, NotFoundException, FileStorageException | – | BE |
| `Mappers/` | Hàm map tay Entity ↔ ViewModel | – | BE |
| `wwwroot/` | `css/`, `js/`, `img/` (ảnh giao diện), `lib/` (Bootstrap, jQuery, Chart.js, SweetAlert2) | – | FE |
| `uploads/` | Ảnh dự thi lưu bằng File I/O – **không commit** | M06 | – |

Chi tiết thiết kế: `docs/plan/02_Kien_truc_Cong_nghe_CSDL.md` mục 4–5.

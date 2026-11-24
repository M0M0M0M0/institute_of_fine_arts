# InstituteOfFineArts.Web – cấu trúc thư mục

| Thư mục | Nội dung | Module | Phụ trách |
|---|---|---|---|
| `Program.cs` | Cấu hình DI, middleware, xác thực cookie, route Areas | – | BE |
| `Areas/Admin` | Controller + View phân hệ Quản trị viên: nhân viên, giáo viên, học viên, nhật ký, thư viện tiêu chí & ngưỡng | M03, M04, M17, M21 | BE + FE |
| `Areas/Staff` | Phân hệ Vận hành: cuộc thi (giải, ban giám khảo, tiêu chí), công bố kết quả, giải, triển lãm, bán tranh, dashboard tổng quan (trước đây Manager), Team | M05, M08, M09, M11 | BE + FE |
| `Areas/Teacher` | Phân hệ Giám khảo: chấm bài theo tiêu chí, xem cuộc thi / triển lãm | M07 | BE + FE |
| `Areas/Student` | Phân hệ Học viên: nộp bài nhiều ảnh, bảng điểm, triển lãm của tôi | M06 | BE + FE |
| `Controllers/` | Controller dùng chung: Home (M02), Account (M01), Profile, Files (tải ảnh), Reports (M10), Charts, Error; `Controllers/Api` cho app mobile | M01, M02, M10 | BE |
| `Views/` | View dùng chung; `Shared/` chứa `_Layout.cshtml`, partial header/sidebar/footer | – | FE |
| `Models/Entities` | Account, StaffProfile, StudentProfile, Competition, CompetitionAward, CompetitionJudge, GradingCriterion, CompetitionCriterion, Submission, SubmissionImage, Evaluation, JudgeEvaluation, JudgeScore, RatingThreshold, AwardIssued, Exhibition, ExhibitionEntry, Customer, AuditLog… (+ BaseEntity) | – | BE |
| `Models/Enums` | Role (ADMIN, STAFF, TEACHER, STUDENT), Gender, EnrollmentStatus, CompetitionStatus, TextType, Rating, ExhibitionStatus, EntryStatus, AuditCategory | – | BE |
| `ViewModels/` | `XxxFormVm` (dữ liệu nhập, có DataAnnotations), `XxxVm` (dữ liệu hiển thị) | – | BE + FE |
| `Data/` | `AppDbContext`, `Configurations/` (cấu hình EF từng bảng), `Migrations/` (dotnet ef), `Seed/` (DbSeeder) | – | BE |
| `Services/` | `Interfaces/` + `Implementations/`: luật nghiệp vụ BR-xx | – | BE |
| `Infrastructure/FileStorage` | Lưu/đọc/xóa ảnh bằng File I/O, tạo thumbnail | M06 | BE |
| `Infrastructure/Security` | Cấu hình cookie, hàm lấy Id người dùng từ Claims | M01 | BE |
| `Infrastructure/BackgroundJobs` | `StatusSyncBackgroundService` – tự đổi trạng thái cuộc thi/triển lãm theo ngày | M05, M09 | BE |
| `Helpers/` | ImageValidator (JPG/PNG/WebP), GradingMath (điểm, ngưỡng, xếp hạng, trao giải), DateTimeHelper, CompetitionStatusResolver, RoleNames | – | BE |
| `Infrastructure/Audit`, `Infrastructure/Filters` | AuditEntry, `[AuditView]`, AuditActionFilter – nhật ký gắn theo đối tượng | M17 | BE |
| `Validation/` | Attribute tự viết: `[DateRange]` | – | BE |
| `Exceptions/` | BusinessException, NotFoundException, FileStorageException | – | BE |
| `Mappers/` | Hàm map tay Entity ↔ ViewModel | – | BE |
| `wwwroot/` | `css/`, `js/`, `img/` (ảnh giao diện), `lib/` (Bootstrap, jQuery, Chart.js, SweetAlert2) | – | FE |
| `uploads/` | Ảnh dự thi lưu bằng File I/O – **không commit** | M06 | – |

Chi tiết thiết kế: `docs/plan/02_Kien_truc_Cong_nghe_CSDL.md` mục 4–5.

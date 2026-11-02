# KẾ HOẠCH DỰ ÁN – INSTITUTE OF FINE ARTS (Học viện Mỹ thuật)

> eProject Semester 3 · 4 tuần (28 ngày) · Nhóm 4 người
> Đây là file tổng quan, nên **đọc file này trước**, rồi mới đến 4 file chi tiết bên dưới.
>
> **Cập nhật 30/10/2026:** theo yêu cầu của giảng viên, dự án chuyển từ Java/Spring Boot sang **.NET (ASP.NET Core MVC)**. Toàn bộ kế hoạch, khung project và tài liệu đã được cập nhật theo.

---

## 1. Bộ kế hoạch gồm những file nào

| File | Nội dung | Ai đọc kỹ nhất |
|---|---|---|
| [01_Phan_tich_nghiep_vu_BA.md](01_Phan_tich_nghiep_vu_BA.md) | Phân tích nghiệp vụ: phạm vi, ma trận phân quyền, **danh sách chức năng F-Mxx-yy**, **quy tắc nghiệp vụ BR-xx**, use case, user story, báo cáo RPT-01..07, màn hình SCR-01..56, giả định & câu hỏi DOUBT, bảng truy vết 35 yêu cầu đề bài | Cả nhóm (BE1, BE2 đọc toàn bộ) |
| [02_Kien_truc_Cong_nghe_CSDL.md](02_Kien_truc_Cong_nghe_CSDL.md) | Lý do chọn công nghệ, tech stack, công nghệ optional (free), kiến trúc, xử lý File I/O, cấu trúc solution, **thiết kế CSDL 11 bảng**, bảo mật, bảng URL, quy ước code, setup môi trường, seed & backup | BE1, BE2, FE1, FE2 |
| [03_Ke_hoach_Tien_do_Phan_cong.md](03_Ke_hoach_Tien_do_Phan_cong.md) | Quy trình các giai đoạn, Scrum-lite 4 sprint, **lịch theo ngày Day 1–28**, Gantt, phân công từng người, mốc, 2 lần STATUS, rủi ro, Git cho người mới, checklist ngày nộp | Cả nhóm |
| [04_Kiem_thu_Tai_lieu_Nop_bai.md](04_Kiem_thu_Tai_lieu_Nop_bai.md) | Chiến lược test, hướng dẫn tester mới, **83 test case**, dữ liệu mẫu, Unit Testing / Final Check List, kế hoạch 13 tài liệu bắt buộc, gợi ý Algorithms, đóng gói ZIP, kịch bản demo | QA (và cả nhóm phần tài liệu được giao) |

---

## 2. Công nghệ đã chốt

**Đề bài yêu cầu gì?** Đặc tả cho phép dùng một hoặc kết hợp các công nghệ sau:
- j2sdk (hoặc mới hơn) / .Net / J2EE
- JSP / Servlets, EJB / Struts
- JDBC / JDBC Driver
- SQL Server 2000 (hoặc mới hơn) / Oracle / MS Access

Đề bài **bắt buộc dùng File I/O** cho việc tải lên, sửa và tải xuống tranh.

**Chọn: ASP.NET Core MVC (.NET 10) + Entity Framework Core + SQL Server.** Lý do:
- Giảng viên yêu cầu làm bằng **.NET**; đặc tả cũng cho phép .Net và SQL Server (“hoặc mới hơn”).
- ASP.NET Core MVC đúng mô hình MVC học ở Sem 3; mỗi phân hệ (Admin, Staff, Student, Manager) là một **Area** riêng.
- Máy đã có sẵn .NET SDK 10 và SQL Server.

| Lớp | Công nghệ |
|---|---|
| Backend | ASP.NET Core MVC (.NET 10, C#), Cookie Authentication + BCrypt, DataAnnotations validation |
| Giao diện | Razor View (`.cshtml`) + Bootstrap 5.3 + JavaScript (jQuery Validation có sẵn) |
| CSDL | SQL Server (Developer/Express) + SSMS, tên DB **`IFA_DB`**, truy cập bằng **Entity Framework Core**, tạo bảng bằng **EF Core Migrations** |
| Upload ảnh | `System.IO` (File I/O – `FileStream`): lưu vào `uploads/submissions/{competitionId}/{guid}.jpg`; DB chỉ lưu đường dẫn |
| Build & chạy | `dotnet build` / `dotnet run`, hoặc F5 trong Visual Studio Community |

**Công nghệ optional, 100% miễn phí** (chi tiết ở mục 3 của file 02):
- **Nên dùng:** EFCore.NamingConventions (giữ tên bảng snake_case), BackgroundService (tự đổi trạng thái cuộc thi), SkiaSharp (ảnh thu nhỏ), Serilog (ghi log), Chart.js (biểu đồ dashboard), SweetAlert2, GitHub Projects, draw.io, xUnit + Moq, Selenium IDE (để QA ghi lại test), SonarQube for IDE.
- **Làm nếu còn thời gian (tuần 4):** ClosedXML (xuất Excel), QuestPDF (xuất PDF), MailKit + Mailtrap (gửi email thông báo), DataTables.

> Lý do không dùng ASP.NET Core Identity, AutoMapper, Repository riêng… và phiên bản từng gói: xem mục 1–3 của file 02.

---

## 3. Các quyết định thống nhất giữa 4 phần

Phần BA (file 01) là **nguồn chuẩn cho quy tắc nghiệp vụ**. Nếu các file khác ghi khác, làm theo bảng dưới đây.

| Chủ đề | Quyết định |
|---|---|
| Vai trò | `ADMIN`, `MANAGER`, `STAFF`, `STUDENT` (+ `GUEST` chưa đăng nhập, chỉ xem trang chủ) |
| 11 thực thể / bảng | Account `account`, StaffProfile `staff_profile`, StudentProfile `student_profile`, Competition `competition`, CompetitionAward `competition_award`, Submission `submission`, Evaluation `evaluation`, AwardIssued `award_issued`, Exhibition `exhibition`, ExhibitionEntry `exhibition_entry`, Customer `customer` |
| Trạng thái cuộc thi | `UPCOMING` → `ONGOING` → `CLOSED` được tính theo ngày (hạn nộp là 23:59:59 của endDate). Trạng thái `GRADED` chỉ có khi Staff bấm **công bố kết quả** và mọi bài đã được chấm |
| Nộp bài | Mỗi học viên **1 bài/1 cuộc thi**. Chỉ nhận JPEG (kiểm tra đuôi file, MIME và magic bytes `FFD8FF`), **tối đa 5 MB**. Chỉ được nộp, sửa, xóa khi cuộc thi đang `ONGOING`; quá hạn thì server từ chối |
| Xếp loại | `BEST`, `BETTER`, `GOOD`, `MODERATE`, `NORMAL`, `DISQUALIFIED`, kèm điểm mạnh / điểm yếu / cần cải thiện. Mỗi bài 1 Evaluation. Chỉ chấm khi cuộc thi đã `CLOSED`. Học viên chỉ thấy kết quả sau `GRADED` |
| Trao giải | Chỉ trao cho bài `BEST`. Mỗi học viên tối đa 1 giải/cuộc thi. Số giải không vượt `quantity` |
| Tranh triển lãm | Chỉ nhận bài `BEST`/`BETTER` của cuộc thi đã `GRADED`. Trạng thái: `PENDING` (học viên đề xuất giá `asking_price`) → `ACCEPTED`/`REJECTED` (Staff duyệt; nếu Staff tự tạo thì vào thẳng `ACCEPTED`) → `SOLD`/`UNSOLD`. Khi SOLD phải có giá bán, ngày bán và khách mua. Trả học viên 100% tiền bán, ghi vào `paid_to_student`, `paid_amount`, `paid_date` |
| Tài khoản | Chỉ Admin tạo tài khoản. Tài khoản đã có dữ liệu thì **khóa** (`active = 0`), không xóa hẳn |
| Dữ liệu mẫu | Theo **file 04 mục 4**: 15 tài khoản, 7 cuộc thi, 27 bài nộp, 2 triển lãm/8 tranh, 3 khách mua. Bảng seed ở file 02 chỉ là bản tối thiểu |
| CRS | **Day 3** chốt nội dung yêu cầu (file 03). Tài liệu CRS hoàn chỉnh: nháp Day 4, bản chốt **Day 6** (file 04) |

---

## 4. Nhóm & phân vai

| Mã | Người | Vai trò chính |
|---|---|---|
| **BE1** | Bạn (trưởng nhóm) | Kiến trúc, CSDL, đăng nhập, File I/O, cuộc thi, nộp bài, chấm bài, trao giải, bán tranh; review & gộp mọi PR; gửi email STATUS / DOUBT / nộp bài |
| **BE2** | Thành viên backend (đang học) | Dữ liệu mẫu, quản lý nhân viên & học viên, các trang chỉ đọc, triển lãm, khách mua, trang chủ, báo cáo, Manager – chủ yếu CRUD, BE1 kèm |
| **FE1** | Thành viên frontend | Layout chung, GUI Standards, màn hình cho phần của BE1 |
| **FE2** | Thành viên frontend | Khung Area & menu, màn hình cho phần của BE2, Interface Design |
| **QA** | Thành viên kiểm thử (đang học) | Ảnh mẫu, test case, 3 vòng kiểm thử, chụp màn hình, tài liệu tổng hợp; khi rảnh làm trang FE nhỏ (trang lỗi, trang giới thiệu, CSS in). **Không nằm trên đường găng** |

Chi tiết từng task: `docs/Phan_cong_task.xlsx`. Ai viết tài liệu nào: file 04 mục 6.2.


---

## 5. Lộ trình 4 tuần (tóm tắt)

| Tuần | Mục tiêu | Mốc |
|---|---|---|
| **Tuần 1 (Day 1–7)**<br>Phân tích & nền tảng | Day 1 kickoff → Day 2–3 BA, chốt CRS → Day 2–5 email **DOUBT:** nếu cần → Day 4–5 ERD + tầng dữ liệu → Day 6: tài khoản mẫu, layout, khung Area → Day 7 xong **đăng nhập/phân quyền** + dữ liệu mẫu | **MS1 – Day 7** (tag v0.1) |
| **Tuần 2 (Day 8–14)**<br>Tài khoản, cuộc thi, nộp bài | BE1: File I/O, cuộc thi & giải thưởng, **nộp bài**; BE2: quản lý nhân viên & học viên, danh sách bài nộp; QA kiểm thử vòng 1. **Day 9 gửi STATUS #1** | **MS3 – Day 14** (v0.2) |
| **Tuần 3 (Day 15–21)**<br>Chấm bài, giải, triển lãm | BE1: chấm bài → trao giải & công bố (**Day 17 chạy trọn luồng cuộc thi → nộp → chấm → trao giải**) → đề xuất & bán tranh; BE2: triển lãm, khách mua, kết quả học viên, trang chủ, báo cáo, Manager; QA kiểm thử vòng 2 | **MS5 – Day 21: đủ chức năng** (v0.9) |
| **Tuần 4 (Day 22–28)**<br>Hoàn thiện & nộp | Day 22–23 tùy chọn (M12+) + kiểm thử vòng 3 → **Day 24 code freeze 18:00** (v1.0-rc) → **Day 25 STATUS #2** → Day 26 cài thử máy sạch, ghép tài liệu, backup DB bản cuối → Day 27 Final Check List, đóng gói → **Day 28 nộp PROJECT SUBMISSION (chỉ 1 lần)** | v1.0 |

### Việc cần làm ngay (Day 1)
1. BE1 tạo repo GitHub (nhánh `main`, `develop`) và bảng GitHub Projects. Mời cả nhóm vào.
2. Cả nhóm cài .NET SDK 10, Visual Studio Community (hoặc VS Code + C# Dev Kit), công cụ `dotnet-ef`, SQL Server + SSMS, Git. Làm theo mục 10 của file 02.
3. Cả nhóm đọc đề tiếng Việt và file 01. Ghi lại thắc mắc. BE1 lọc thắc mắc và gửi email **DOUBT:** (có sẵn câu hỏi mẫu ở mục 10 của file 01).
4. QA bắt đầu sưu tầm ảnh mẫu có giấy phép free (hoặc tự vẽ) và đọc bộ test case (`docs/testing/TestCases_FineArts.xlsx`).

---

## 6. Thứ tự làm và lý do

```
Phân tích BA  →  ERD/CSDL  →  Khung project + Đăng nhập/phân quyền (M01)
   →  Dữ liệu gốc (M03, M04, M05)  →  Nộp bài bằng File I/O (M06)
   →  Chấm bài (M07)  →  Trao giải (M08)  →  Triển lãm & bán tranh (M09)
   →  Báo cáo, Manager, trang chủ (M10, M11, M02)  →  Optional (M12+)
```

- Mỗi bước dùng dữ liệu do bước trước tạo ra:
  - Không có cuộc thi thì không có bài nộp.
  - Không có bài nộp thì không có gì để chấm.
  - Không có kết quả chấm thì không trao giải hay gửi triển lãm được.
- **Luồng lõi** (cuộc thi → nộp bài → chấm → trao giải) phải chạy được trước Day 18. Đây là phần giám khảo chú ý nhiều nhất.
- Chỉ làm tính năng optional khi các chức năng bắt buộc đã xong và đã qua test.

---

## 7. Lưu ý quan trọng từ đề bài

- **Mọi khối code phải có comment.** Logic chương trình phải được giải thích trong tài liệu.
- Upload, sửa và tải ảnh **bắt buộc dùng File I/O**.
- Gửi **2 email STATUS** (Day 9 và Day 25). Tiêu đề email bắt đầu bằng `STATUS:`, `DOUBT:` hoặc `PROJECT SUBMISSION:`.
- Bộ tài liệu cần đủ **13 mục** (danh sách ở mục 6 của file 04), kèm hướng dẫn sử dụng và tài khoản đăng nhập.
- **Backup mỗi ngày**: push code lên GitHub và tạo file `.bak` cho DB.
- **Chỉ được nộp 1 lần.** Tên file ZIP theo dạng `Batch_Group_ProjectTitle`. Hai người kiểm tra chéo trước khi nộp.

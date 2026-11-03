# PHẦN 04 – KIỂM THỬ, TÀI LIỆU, ĐÓNG GÓI & BẢO VỆ

**Dự án:** Institute of Fine Arts (Học viện Mỹ thuật) – eProject Aptech Semester 3
**Người phụ trách phần này:** QA (kiểm thử & tài liệu tổng hợp), BE1 duyệt
**Đối tượng đọc chính:** QA (kiểm thử thủ công, tài liệu tổng hợp); BE1, BE2, FE1, FE2 (tự test phần mình, viết tài liệu được giao, review)
**Stack:** .NET 10 · ASP.NET Core MVC (C#) · Entity Framework Core · SQL Server · Razor + Bootstrap 5 · File I/O lưu ảnh vào `uploads/`
**Mốc thời gian:** 4 tuần (Day 1 → Day 28) · **Chức năng xong: Day 17** · **Review & chỉnh sửa: Day 18–23** · **Code freeze: Day 24** · **Nộp bài: Day 28** (1 file ZIP, **chỉ được nộp 1 lần**)

> Quy ước dùng chung với các phần kế hoạch khác:
> - Vai trò: `ADMIN`, `MANAGER`, `STAFF`, `STUDENT`, `GUEST`.
> - Module: M01 Xác thực & tài khoản · M02 Trang chủ công khai · M03 Admin – quản lý nhân viên · M04 Admin – quản lý học viên · M05 Cuộc thi & giải thưởng · M06 Bài nộp · M07 Chấm bài · M08 Trao giải · M09 Triển lãm & bán tranh · M10 Báo cáo · M11 Màn hình chỉ xem của Manager.
> - Thực thể: `Account`, `StaffProfile`, `StudentProfile`, `Competition`, `CompetitionAward`, `Submission`, `Evaluation`, `AwardIssued`, `Exhibition`, `ExhibitionEntry`, `Customer`.
> - Xếp loại (Evaluation): `BEST`, `BETTER`, `GOOD`, `MODERATE`, `NORMAL`, `DISQUALIFIED`.
> - Trạng thái cuộc thi (tính từ ngày): `UPCOMING` (hôm nay < startDate), `ONGOING` (startDate ≤ hôm nay ≤ endDate), `CLOSED` (hôm nay > endDate, chưa công bố), `GRADED` (Staff đã công bố kết quả).

---

## Mục lục

1. [Chiến lược kiểm thử](#1-chiến-lược-kiểm-thử)
2. [Hướng dẫn cho tester mới (QA)](#2-hướng-dẫn-cho-tester-mới-qa)
3. [Bộ test case mẫu (M01–M11)](#3-bộ-test-case-mẫu-m01m11)
4. [Kế hoạch dữ liệu mẫu (seed data)](#4-kế-hoạch-dữ-liệu-mẫu-seed-data)
5. [Unit Testing Check List & Final Check List](#5-unit-testing-check-list--final-check-list)
6. [Kế hoạch tài liệu](#6-kế-hoạch-tài-liệu)
7. [Gợi ý tài liệu "Algorithms"](#7-gợi-ý-tài-liệu-algorithms)
8. [Đóng gói & nộp bài](#8-đóng-gói--nộp-bài)
9. [Chuẩn bị demo / bảo vệ](#9-chuẩn-bị-demo--bảo-vệ)

---

## 1. Chiến lược kiểm thử

### 1.1. Các mức kiểm thử

| Mức | Ai làm | Công cụ | Phạm vi | Khi nào |
|---|---|---|---|---|
| **Unit test** | BE (FE hỗ trợ nếu có thời gian) | xUnit + Moq (+ EF Core InMemory cho dữ liệu giả) | Các service có logic quan trọng: `SubmissionService` (kiểm tra hạn nộp, quyền sở hữu), `FileStorageService` (kiểm tra JPEG, kích thước, tên file an toàn), `CompetitionService` (tính trạng thái, chặn xóa khi đã có bài nộp), `EvaluationService` (bài nộp trễ → `DISQUALIFIED`), `ExhibitionService` (bán tranh, trả tiền học viên), `ReportService` (lọc theo khoảng ngày) | Viết song song khi code, mục tiêu ≥ 25 unit test xanh trước Day 24 |
| **Integration test** | BE | `WebApplicationFactory` (gói `Microsoft.AspNetCore.Mvc.Testing`) hoặc Postman collection | Controller ↔ Service ↔ DbContext ↔ DB; phân quyền URL theo vai trò; upload multipart | Từ Day 12, chạy lại trước code freeze |
| **System test (thủ công)** | **QA** (người làm chức năng tự test trước khi tạo PR) | Trình duyệt Chrome/Edge, file test case Excel/Google Sheets, Selenium IDE (tùy chọn) | Toàn bộ chức năng M01–M11 theo test case ở mục 3 | Mỗi module xong (BE/FE báo "Ready for test") → test trong 24 giờ; vòng test đầy đủ Day 16–19 |
| **UAT theo vai trò** | QA đóng vai STUDENT + GUEST, FE1 đóng vai STAFF, FE2 đóng vai ADMIN + MANAGER; BE1/BE2 quan sát | Kịch bản demo (mục 9) | Đi trọn luồng nghiệp vụ như người dùng thật | Day 19 (lần 1), Day 26 (lần 2 trên bản đóng gói) |
| **Regression** | QA | Selenium IDE (ghi/phát lại) + danh sách test case ưu tiên "Cao" | Chạy lại tất cả test case mức ưu tiên Cao + các bug đã sửa | Day 24 (trước code freeze) và Day 26 (trên máy sạch) |

### 1.2. Môi trường kiểm thử

| Thành phần | Cấu hình |
|---|---|
| Hệ điều hành | Windows 10/11 |
| .NET SDK | .NET 10 (kiểm tra bằng `dotnet --version`) |
| CSDL | SQL Server 2019/2022 Express (hoặc Developer), database tên `IFA_DB` |
| Ứng dụng | Chạy bằng `dotnet run --project src/InstituteOfFineArts.Web` hoặc F5 trong Visual Studio, địa chỉ `http://localhost:5000` |
| Trình duyệt | Chrome (bản mới nhất) – chính; Edge – phụ; kiểm tra giao diện điện thoại bằng F12 → Toggle device toolbar |
| Thư mục ảnh | `uploads/` (cạnh file jar / thư mục gốc dự án) |
| Dữ liệu | Chạy script seed (mục 4) để reset dữ liệu trước mỗi vòng test lớn |

> **Quy tắc vàng:** Test trên bản code mới nhất của nhánh `main` (hoặc `develop`) – trước khi test phải `git pull`, chạy lại app. Ghi rõ commit/ngày build vào file test.

### 1.3. Dữ liệu kiểm thử

- Dữ liệu seed chuẩn (mục 4) – dùng chung cho cả nhóm, KHÔNG sửa tay trong DB khi đang test.
- Bộ file test để trong thư mục `test-data/` của repo:

| File | Mục đích |
|---|---|
| `ok_small.jpg` (~300 KB) | JPEG hợp lệ |
| `ok_upper.JPEG` | JPEG phần mở rộng viết hoa |
| `big_8mb.jpg` (> 5 MB) | Kiểm tra giới hạn dung lượng (giới hạn đề xuất: 5 MB/ảnh) |
| `fake_jpg.jpg` (thực chất là file .txt đổi tên) | Kiểm tra nội dung không phải JPEG |
| `image.png`, `image.gif`, `doc.pdf` | Định dạng không được phép |
| `empty.jpg` (0 byte) | File rỗng |
| `ten co dau & ky tu #lạ!.jpg` | Tên file có ký tự đặc biệt / tiếng Việt |
| `..%2F..%2Fevil.jpg` (đặt tên qua Postman: `../../evil.jpg`) | Path traversal |
| `xss.txt` chứa `<script>alert('XSS')</script>` và `' OR '1'='1` | Dán vào ô thơ/trích dẫn/tìm kiếm |

### 1.4. Tiêu chí vào / ra (Entry / Exit criteria)

| | Tiêu chí |
|---|---|
| **Vào (bắt đầu test một module)** | (1) Dev báo module "Ready for test" trên GitHub/nhóm chat; (2) App build & chạy được; (3) Dữ liệu seed đã nạp; (4) Test case của module đã được viết và BE review. |
| **Ra (kết thúc vòng test trước code freeze – Day 24)** | (1) 100% test case đã chạy; (2) ≥ 95% test case **Pass**; (3) **0 bug Critical, 0 bug High** đang mở; (4) Bug Medium/Low còn mở được ghi vào "Known issues" trong README; (5) Unit test chạy `dotnet test` xanh 100%. |
| **Ra (trước khi nộp – Day 27)** | Final Check List (mục 5.2) được tick đủ, cài lại trên máy sạch thành công. |

---

## 2. Hướng dẫn cho tester mới (QA)

### 2.1. Cách đọc yêu cầu (5 bước)

1. Mở đề bài `Hoc vien My thuat - De bai.docx`, tìm đoạn nói về vai trò bạn đang test (VD: "Khi đăng nhập với vai trò Học viên…").
2. Mỗi gạch đầu dòng = **1 chức năng**. Ghi lại vào sổ: *"Ai làm? Làm gì? Điều kiện gì?"*.
   - VD: "Thay đổi bài thiết kế … **với điều kiện chưa quá ngày kết thúc**" → Ai: STUDENT · Làm gì: sửa bài · Điều kiện: hôm nay ≤ endDate.
3. Với mỗi chức năng, tự hỏi 3 câu: **Trường hợp đúng?** (happy path) – **Trường hợp sai/thiếu dữ liệu?** – **Người không có quyền thì sao?**
4. Tìm các "từ khóa giới hạn": *trước hạn, sau hạn, chỉ, chỉ được, JPEG, đã bán, đã trả tiền…* → mỗi từ khóa là ít nhất 1 test case biên.
5. Chỗ nào không rõ → hỏi BE trên nhóm chat, **không tự đoán**. Ghi câu trả lời vào cột "Ghi chú".

### 2.2. Cách viết test case

**Template (file Excel `TestCases_FineArts.xlsx`, mỗi module 1 sheet hoặc 1 sheet chung):**

| ID | Module | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Người test | Ngày |
|---|---|---|---|---|---|---|---|---|---|---|
| TC-M06-03 | M06 | Upload file PNG bị từ chối | Đăng nhập `student01`; cuộc thi "Colors of Autumn" đang ONGOING | 1. Vào Cuộc thi → "Colors of Autumn" → Nộp bài<br>2. Chọn file<br>3. Nhập mô tả<br>4. Bấm "Submit" | `image.png`; mô tả "Test" | Hiện lỗi "Only JPEG images are accepted"; không có bài mới trong "My Submissions"; không có file mới trong `uploads/` | (điền khi test) | Pass / Fail / Blocked / Not run | QA | 20/xx |

**Quy tắc viết:**
- **ID:** `TC-Mxx-yy` (xx = mã module, yy = số thứ tự). Không đổi ID sau khi đã đặt.
- **Tiêu đề:** ngắn, nói rõ điều đang kiểm tra (động từ + đối tượng + điều kiện).
- **Các bước:** đánh số, mỗi bước = 1 hành động bấm/gõ. Người khác đọc phải làm lại được y hệt.
- **Kết quả mong đợi:** phải **kiểm chứng được** (thông báo gì, trang nào, dữ liệu thay đổi ra sao). Tránh viết "hoạt động bình thường".
- **Trạng thái:** `Pass` (đúng mong đợi) · `Fail` (sai → phải tạo bug, ghi số Issue vào cột Kết quả thực tế) · `Blocked` (không test được vì lỗi khác) · `Not run`.
- Fail thì **chụp màn hình** ngay (Win + Shift + S), lưu tên `TC-M06-03_fail.png`.

### 2.3. Cách báo bug (GitHub Issue)

Vào repo → tab **Issues** → **New issue** → dán template dưới đây. Gắn label: `bug`, mức độ (`critical`/`high`/`medium`/`low`), module (`M06`…). Assign cho BE (lỗi xử lý/dữ liệu) hoặc FE (lỗi giao diện).

```markdown
**Tiêu đề:** [M06] Upload file PNG vẫn được chấp nhận
<!-- Quy tắc: [Mã module] + Mô tả ngắn hiện tượng sai -->

**Test case liên quan:** TC-M06-03
**Môi trường:** Chrome 1xx, Windows 11, build ngày 20/xx (commit abc123)
**Tài khoản:** student01

**Bước tái hiện:**
1. Đăng nhập student01 / (mật khẩu test)
2. Vào Cuộc thi → "Colors of Autumn" → Nộp bài
3. Chọn file image.png, nhập mô tả "Test"
4. Bấm "Submit"

**Kết quả mong đợi:** Báo lỗi "Only JPEG images are accepted", không lưu bài.
**Kết quả thực tế:** Bài được lưu, ảnh PNG hiển thị trong "My Submissions".
**Ảnh chụp / log:** (kéo thả ảnh vào đây; nếu có lỗi ở cửa sổ Output/console của Visual Studio thì copy 10–20 dòng)
**Mức độ:** High
**Tần suất:** Luôn luôn (3/3 lần)
```

### 2.4. Mức độ nghiêm trọng (Severity)

| Mức | Định nghĩa | Ví dụ | Thời hạn sửa |
|---|---|---|---|
| **Critical** | App sập, mất dữ liệu, lỗ hổng bảo mật, chặn luồng chính | Không đăng nhập được; STUDENT vào được trang ADMIN; upload được file `.exe`/`.cshtml`/path traversal | Trong ngày |
| **High** | Chức năng chính sai, không có cách vòng | Nộp bài sau hạn vẫn được; bán tranh không lưu khách hàng | ≤ 2 ngày |
| **Medium** | Chức năng phụ sai hoặc có cách vòng | Tìm kiếm không phân biệt dấu; phân trang sai | Trước code freeze nếu kịp |
| **Low** | Giao diện, chính tả, căn lề | Nút lệch, sai chính tả tiêu đề | Khi rảnh / ghi Known issues |

### 2.5. Vòng đời bug

```
 New ──► Assigned ──► In Progress ──► Fixed (Ready for retest) ──► Verified ──► Closed
  │                                        │
  │                                        └──► Reopened (retest vẫn lỗi) ──► Assigned
  └──► Rejected (không phải bug / trùng / do hiểu sai yêu cầu – phải ghi lý do)
```

| Bước | Ai | Việc cần làm trên GitHub |
|---|---|---|
| New | Tester | Tạo Issue theo template |
| Assigned | BE (lead) | Gán người sửa, gắn milestone "Week x" |
| In Progress → Fixed | Dev | Commit có ghi `Fixes #12`; comment "Fixed, ready for retest" |
| Verified / Reopened | Tester | Test lại đúng các bước; nếu OK comment "Verified on build …" rồi Close; nếu còn lỗi → Reopen + ảnh mới |
| Closed | Tester | Cập nhật trạng thái test case thành Pass |

---

## 3. Bộ test case mẫu (M01–M11)

> Ký hiệu cột **Ưu tiên**: C = Cao (bắt buộc chạy trong regression), T = Trung bình, Th = Thấp.
> Mật khẩu test chung: xem mục 4.5. Cột "Kết quả thực tế / Trạng thái / Người test / Ngày" để trống – điền vào file Excel khi chạy.
> Ngày "hôm nay" được ký hiệu **D**. Cuộc thi seed có ngày tương đối với D (mục 4.2).

### M01 – Xác thực & tài khoản (10 TC)

| ID | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|---|---|
| TC-M01-01 | Đăng nhập đúng – STUDENT | Chưa đăng nhập | 1. Mở `/login` 2. Nhập user/pass 3. Bấm Đăng nhập | `student01` / mật khẩu đúng | Chuyển tới trang của học viên; header hiện tên học viên & nút Đăng xuất | C |
| TC-M01-02 | Đăng nhập đúng – mỗi vai trò chuyển đúng trang | Chưa đăng nhập | Lặp TC-M01-01 với từng tài khoản | `admin01`, `manager01`, `staff01` | ADMIN → dashboard admin; MANAGER → dashboard manager; STAFF → dashboard staff | C |
| TC-M01-03 | Sai mật khẩu | Chưa đăng nhập | Nhập user đúng, mật khẩu sai | `student01` / `sai123` | Ở lại `/login`, hiện "Invalid username or password." (không nói rõ sai cái nào) | C |
| TC-M01-04 | Tài khoản không tồn tại | Chưa đăng nhập | Nhập user lạ | `khongton tai` / bất kỳ | Thông báo lỗi giống TC-M01-03 | T |
| TC-M01-05 | Bỏ trống ô đăng nhập | Chưa đăng nhập | Bấm Đăng nhập khi để trống | (trống) | Báo bắt buộc nhập; không gửi form hoặc báo lỗi | T |
| TC-M01-06 | SQL injection ở ô đăng nhập | Chưa đăng nhập | Nhập chuỗi injection vào username | `' OR '1'='1' --` / `x` | Đăng nhập thất bại, không lỗi 500, không lộ thông báo SQL | C |
| TC-M01-07 | Tài khoản bị khóa không đăng nhập được | ADMIN đã khóa `student10` (active = 0) | Đăng nhập `student10` | mật khẩu đúng | Báo "Your account has been locked. Please contact the administrator."/lỗi đăng nhập; không vào được hệ thống | T |
| TC-M01-08 | Đăng xuất | Đang đăng nhập `staff01` | 1. Bấm Đăng xuất 2. Bấm nút Back của trình duyệt 3. Thử mở `/staff/submissions` | | Về trang chủ/login; Back không xem được dữ liệu đã đăng nhập; mở URL staff bị chuyển về `/login` | C |
| TC-M01-09 | Đổi mật khẩu | Đăng nhập `student02` | 1. Hồ sơ → Đổi mật khẩu 2. Nhập mật khẩu cũ sai → Lưu 3. Nhập đúng, mật khẩu mới + xác nhận → Lưu 4. Đăng xuất, đăng nhập bằng mật khẩu mới | | Bước 2 báo lỗi; bước 3 thành công; bước 4 đăng nhập được bằng mật khẩu mới, mật khẩu cũ không dùng được | T |
| TC-M01-10 | Mật khẩu được mã hóa trong DB | Có quyền mở SQL Server Management Studio (SSMS) | `SELECT username, password_hash FROM account` | | Cột password_hash là chuỗi BCrypt (bắt đầu `$2a$`/`$2b$`), không phải mật khẩu thô | C |

### M02 – Trang chủ công khai (6 TC)

| ID | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|---|---|
| TC-M02-01 | GUEST xem trang chủ | Chưa đăng nhập | Mở `http://localhost:5000/` | Seed chuẩn | Hiện 4 khối: cuộc thi sắp diễn ra (UPCOMING), đang diễn ra (ONGOING), học viên đạt giải cuộc thi trước, tổng số bài nộp cuộc thi đang diễn ra | C |
| TC-M02-02 | Đếm số bài nộp đúng | Seed: "Colors of Autumn" ONGOING có 6 bài | So số hiển thị với `SELECT COUNT(*) FROM Submission WHERE competition_id = …` | | Hai con số bằng nhau | C |
| TC-M02-03 | Cập nhật số bài sau khi nộp mới | Ghi lại số bài hiện tại | 1. `student07` nộp 1 bài vào cuộc thi ONGOING 2. Đăng xuất, tải lại trang chủ | `ok_small.jpg` | Số bài tăng thêm 1 | T |
| TC-M02-04 | Danh sách đạt giải cuộc thi trước | Cuộc thi GRADED "Hometown Landscapes" đã trao giải | Xem khối "Awarded" | | Hiện đúng tên học viên, tên giải, tên cuộc thi theo bảng `AwardIssued` | T |
| TC-M02-05 | Tìm kiếm & xem chi tiết cuộc thi khi chưa đăng nhập | Chưa đăng nhập | 1. Mở `/competitions`, tìm "autumn", lọc trạng thái ONGOING 2. Bấm vào 1 cuộc thi UPCOMING | | Danh sách chỉ hiện cuộc thi đã công bố (tóm tắt), kết quả lọc đúng; trang chi tiết hiện mô tả, thể lệ, ngày bắt đầu/kết thúc, giải thưởng; nút "Submit" yêu cầu đăng nhập | T |
| TC-M02-06 | Giao diện điện thoại | Chưa đăng nhập | F12 → Toggle device → iPhone 12 | | Menu thu gọn (hamburger), không tràn ngang, ảnh co giãn | Th |

### M03 – Admin quản lý nhân viên (6 TC)

| ID | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|---|---|
| TC-M03-01 | Thêm nhân viên hợp lệ | Đăng nhập `admin01` | 1. Nhân viên → Thêm mới 2. Nhập thông tin đăng nhập, cá nhân, công việc (ngày vào làm, lớp, môn), ghi chú 3. Lưu | username `staff09`, vai trò STAFF, ngày vào làm 01/09/2025, lớp "Painting K12", môn "Watercolor" | Báo thành công; nhân viên mới có trong danh sách; đăng nhập được bằng `staff09` | C |
| TC-M03-02 | Trùng username | `staff01` đã tồn tại | Thêm mới với username `staff01` | | Báo "Username already exists"; không tạo bản ghi | C |
| TC-M03-03 | Bỏ trống trường bắt buộc / email sai định dạng | Đăng nhập `admin01` | Để trống họ tên; email `abc@` → Lưu | | Báo lỗi tại từng ô; dữ liệu đã nhập không bị mất | T |
| TC-M03-04 | Sửa thông tin nhân viên | Có `staff02` | Sửa lớp phụ trách → Lưu | "Animation K5" | Danh sách & chi tiết hiện giá trị mới | T |
| TC-M03-05 | Tìm kiếm nhân viên | Seed 3 STAFF | Tìm theo tên một phần / theo môn | "Ngoc", "Watercolor" | Chỉ hiện các nhân viên khớp; không khớp → "No results found" | T |
| TC-M03-06 | Xóa nhân viên đã chấm bài | `staff01` đã có Evaluation | Bấm Xóa `staff01` → xác nhận | | Hệ thống chặn xóa (báo lý do) **hoặc** chỉ vô hiệu hóa tài khoản; Evaluation cũ vẫn còn tên người chấm | C |

### M04 – Admin quản lý học viên (5 TC)

| ID | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|---|---|
| TC-M04-01 | Thêm học viên hợp lệ | Đăng nhập `admin01` | Học viên → Thêm → nhập thông tin đăng nhập, cá nhân, nhập học (ngày nhập học, lớp, khóa) → Lưu | `student11` | Thành công; `student11` đăng nhập được với vai trò STUDENT | C |
| TC-M04-02 | Ngày sinh không hợp lệ | Đăng nhập `admin01` | Nhập ngày sinh ở tương lai → Lưu | 01/01/2099 | Báo lỗi ngày sinh | Th |
| TC-M04-03 | Sửa & tìm học viên | Có `student03` | Sửa số điện thoại; sau đó tìm theo mã/tên | | Lưu đúng; tìm ra đúng học viên | T |
| TC-M04-04 | Xóa học viên đã có bài nộp | `student01` có Submission | Xóa → xác nhận | | Chặn xóa hoặc vô hiệu hóa (soft delete); không để lại bài nộp "mồ côi" gây lỗi 500 ở trang staff | C |
| TC-M04-05 | STAFF chỉ được XEM học viên | Đăng nhập `staff01` | Mở danh sách học viên; thử mở URL `/admin/students/new` | | Xem được thông tin cá nhân & nhập học; không có nút Thêm/Sửa/Xóa; URL admin → 403 | C |

### M05 – Cuộc thi & giải thưởng (8 TC)

| ID | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|---|---|
| TC-M05-01 | Tạo cuộc thi hợp lệ kèm giải thưởng | Đăng nhập `staff01` | 1. Cuộc thi → Thêm 2. Nhập tên, mô tả, thể lệ, ngày bắt đầu D+3, kết thúc D+20 3. Thêm 3 giải (Nhất/Nhì/Ba) 4. Lưu | "Lunar New Year 2027" | Lưu thành công; trạng thái hiển thị UPCOMING; có 3 `CompetitionAward` | C |
| TC-M05-02 | Ngày kết thúc trước ngày bắt đầu | Đăng nhập `staff01` | Nhập start D+10, end D+5 → Lưu | | Báo "End date must be on or after the start date"; không lưu | C |
| TC-M05-03 | Trạng thái tính đúng theo ngày | Seed đủ 3 loại | Xem danh sách cuộc thi | | UPCOMING / ONGOING / CLOSED / GRADED khớp bảng mục 4.2; cuộc thi có endDate = D vẫn là ONGOING | C |
| TC-M05-04 | Sửa cuộc thi | Cuộc thi UPCOMING | Sửa thể lệ & thêm 1 giải → Lưu | | Chi tiết hiển thị nội dung mới | T |
| TC-M05-05 | Xóa cuộc thi CHƯA có bài nộp | Cuộc thi UPCOMING "Family Portrait" (0 bài) | Xóa → xác nhận | | Xóa thành công; các `CompetitionAward` của nó cũng bị xóa | T |
| TC-M05-06 | Xóa cuộc thi ĐÃ có bài nộp | "Colors of Autumn" có 6 bài | Xóa → xác nhận | | **Bị chặn**: "A competition with submissions cannot be deleted"; dữ liệu và file ảnh còn nguyên | C |
| TC-M05-07 | Tìm kiếm cuộc thi theo tên/trạng thái | Seed chuẩn | Lọc trạng thái CLOSED; tìm "autumn" | | Kết quả đúng | T |
| TC-M05-08 | XSS trong mô tả cuộc thi | Đăng nhập `staff01` | Mô tả = `<script>alert('XSS')</script>` → Lưu → mở trang chi tiết (cả khi GUEST) | | Không có hộp thoại alert; chuỗi hiển thị dạng chữ thường (Razor tự mã hóa HTML) | C |

### M06 – Bài nộp (14 TC)

| ID | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|---|---|
| TC-M06-01 | Nộp bài hợp lệ | `student01`; "Colors of Autumn" ONGOING; chưa nộp | 1. Cuộc thi → Nộp bài 2. Chọn ảnh 3. Nhập thơ/trích dẫn/câu chuyện + mô tả 4. Nộp | `ok_small.jpg`, thơ 4 câu | Thành công; bài có trong "My Submissions" với ngày nộp = hôm nay; file mới trong `uploads/` với **tên do hệ thống sinh** (Guid) | C |
| TC-M06-02 | JPEG phần mở rộng viết hoa | Như trên (cuộc thi ONGOING khác) | Nộp | `ok_upper.JPEG` | Được chấp nhận | T |
| TC-M06-03 | File PNG bị từ chối | Như TC-M06-01 | Nộp | `image.png` | "Only JPEG images are accepted"; không lưu bài, không có file mới | C |
| TC-M06-04 | File giả JPEG (đổi đuôi) | Như trên | Nộp | `fake_jpg.jpg` | Bị từ chối (kiểm tra content-type/magic bytes `FF D8 FF`) | C |
| TC-M06-05 | File quá lớn | Như trên | Nộp | `big_8mb.jpg` | Báo "Maximum file size is 5 MB" trên trang (không phải trang lỗi trắng/500) | C |
| TC-M06-06 | File rỗng / không chọn file | Như trên | Nộp không chọn file; nộp `empty.jpg` | | Báo "Please choose an image" / "Invalid file" | T |
| TC-M06-07 | Tên file có ký tự lạ/tiếng Việt | Như trên | Nộp | `ten co dau & ky tu #lạ!.jpg` | Lưu thành công, ảnh hiển thị đúng (vì tên file lưu là Guid) | T |
| TC-M06-08 | Path traversal qua tên file | Postman, có cookie phiên `student01` | Gửi multipart với `filename="../../evil.jpg"` (nội dung JPEG thật) | | Không có file nào được ghi ra ngoài `uploads/`; tên lưu là Guid hoặc request bị từ chối | C |
| TC-M06-09 | Nộp bài ngay trước hạn (đúng ngày end date) | Cuộc thi "Deadline Today" có endDate = D | Nộp lúc trong ngày D | `ok_small.jpg` | **Được chấp nhận** (hạn tính hết 23:59:59 ngày end date) | C |
| TC-M06-10 | Nộp bài sau hạn | Cuộc thi CLOSED (endDate = D-1) | 1. Mở trang cuộc thi 2. Thử mở trực tiếp URL `/student/submissions/new?competitionId=…` và POST bằng Postman | | Nút Nộp bị ẩn/khóa; POST trực tiếp bị từ chối "The competition has ended"; không có bài mới | C |
| TC-M06-11 | Nộp vào cuộc thi UPCOMING | Cuộc thi UPCOMING | Thử nộp (qua URL trực tiếp) | | Bị từ chối "The competition has not started yet" | T |
| TC-M06-12 | Sửa bài trước hạn (đổi ảnh + thơ) | `student01` có bài ở cuộc thi ONGOING | Bài của tôi → Sửa → chọn ảnh mới, sửa thơ → Lưu | | Ảnh mới hiển thị; file ảnh cũ bị xóa khỏi `uploads/` (hoặc được thay thế); thơ mới được lưu | C |
| TC-M06-13 | Sửa/xóa bài sau hạn | `student02` có bài ở cuộc thi CLOSED | Mở bài → tìm nút Sửa/Xóa; thử POST trực tiếp `/student/submissions/{id}/edit` và `/delete` | | Không có nút; request trực tiếp bị từ chối; dữ liệu không đổi | C |
| TC-M06-14 | Sửa/xóa bài của người khác (IDOR) | `student01` đăng nhập; biết id bài của `student03` | Đổi id trên URL `/student/submissions/{id}/edit` | id bài của student03 | 403 hoặc "Not found"; không xem/sửa được bài người khác | C |

> Bổ sung (không đánh số riêng, gộp vào TC-M06-01 khi chạy): XSS/SQL trong ô thơ/trích dẫn – xem TC-M07-06. Một học viên chỉ được 1 bài/1 cuộc thi (nếu nhóm chọn quy tắc này) – nộp lần 2 phải báo "You have already submitted to this competition. Use Edit instead.".

### M07 – Chấm bài (7 TC)

| ID | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|---|---|
| TC-M07-01 | Xem danh sách bài theo cuộc thi | Đăng nhập `staff01` | Chấm bài → chọn "Hometown Landscapes" | | Thấy ảnh thu nhỏ, tên học viên, ngày nộp, thơ/trích dẫn, mô tả; tải ảnh gốc về được (download) | C |
| TC-M07-02 | Chấm xếp loại BEST kèm nhận xét | Bài chưa chấm | Chọn BEST, nhập điểm mạnh, điểm yếu, cần cải thiện → Lưu | | Lưu `Evaluation`; bài hiển thị nhãn BEST; ghi nhận người chấm & ngày chấm | C |
| TC-M07-03 | Sửa xếp loại đã chấm | Bài đã chấm GOOD | Đổi thành BETTER → Lưu | | Cập nhật, không tạo bản ghi trùng | T |
| TC-M07-04 | Bài nộp trễ → DISQUALIFIED | Seed có 1 bài `submittedAt` > endDate (nhập qua seed) | Mở bài đó để chấm | | Hệ thống tự gán/ép DISQUALIFIED, không cho chọn loại khác (hoặc cảnh báo rõ) | C |
| TC-M07-05 | Lọc bài theo xếp loại | Seed có đủ 6 loại | Lọc BEST; lọc DISQUALIFIED | | Chỉ hiện bài đúng loại | C |
| TC-M07-06 | XSS / SQL trong ô thơ & nhận xét | `student04` nộp bài với thơ = `<script>alert(1)</script>` và trích dẫn = `'; DROP TABLE Submission; --`; staff nhập nhận xét = `<b>đậm</b>` | Staff mở bài; student xem nhận xét | | Không có alert; chữ hiển thị nguyên văn; bảng `Submission` vẫn còn; không lỗi 500 | C |
| TC-M07-07 | STUDENT không chấm được | Đăng nhập `student01` | Mở URL `/staff/evaluations/{id}` hoặc POST chấm bằng Postman | | 403 Forbidden (trang lỗi thân thiện) | C |

### M08 – Trao giải (5 TC)

| ID | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|---|---|
| TC-M08-01 | Trao giải cho bài BEST | Cuộc thi "Hometown Landscapes" (đã chấm), có bài BEST chưa đạt giải | Trao giải → chọn cuộc thi → chọn giải "First Prize" → chọn bài → nhập nhận xét cuộc thi → Lưu | | Tạo `AwardIssued`; hiện ở trang chủ & trang "Awards" của học viên | C |
| TC-M08-02 | Không trao giải khi cuộc thi chưa kết thúc | Cuộc thi ONGOING | Thử trao giải | | Bị chặn "Awards can only be issued after the competition has closed" | T |
| TC-M08-03 | Không trao giải cho bài DISQUALIFIED | Có bài DISQUALIFIED | Thử chọn bài đó | | Bài không có trong danh sách chọn / bị từ chối | C |
| TC-M08-04 | Không trao 1 giải cho 2 người (trùng) | "First Prize" đã trao | Trao "First Prize" lần 2 cho bài khác | | Báo giải đã được trao (trừ khi giải cho phép số lượng > 1) | T |
| TC-M08-05 | STUDENT xem giải theo cuộc thi | Đăng nhập `student05` | Giải thưởng → chọn cuộc thi | | Hiện danh sách người đạt giải, tên giải, nhận xét | T |

### M09 – Triển lãm & bán tranh (9 TC)

| ID | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|---|---|
| TC-M09-01 | Tạo triển lãm | Đăng nhập `staff01` (hoặc `manager01`) | Triển lãm → Thêm: tên, địa điểm, ngày bắt đầu/kết thúc, mô tả → Lưu | "Hanoi Spring Exhibition" | Lưu thành công; STUDENT thấy trong "Upcoming Exhibitions" | C |
| TC-M09-02 | Gửi tranh BEST đi triển lãm với giá học viên đưa | Bài BEST của `student01` | Chọn bài → Gửi triển lãm → chọn triển lãm → nhập giá học viên đề xuất → Lưu | Giá $200.00 | Tạo `ExhibitionEntry` (sold = false, paidToStudent = false) | C |
| TC-M09-03 | Giá không hợp lệ | Như trên | Nhập giá âm / chữ / để trống | `-5`, `abc` | Báo lỗi; không lưu | T |
| TC-M09-04 | Không gửi trùng 1 bài vào cùng 1 triển lãm | Bài đã có trong triển lãm | Gửi lại | | Báo trùng | T |
| TC-M09-05 | Đánh dấu ĐÃ BÁN kèm khách hàng | Entry chưa bán | Sửa → tick Đã bán → nhập giá bán, ngày bán, thông tin khách (tên, SĐT, email, địa chỉ) → Lưu | Giá bán $250.00 | sold = true, soldPrice lưu đúng, tạo/gắn `Customer`; trạng thái "Sold – Not paid to student" | C |
| TC-M09-06 | Bán nhưng thiếu giá bán/khách hàng | Entry chưa bán | Tick Đã bán, để trống giá bán → Lưu | | Báo lỗi bắt buộc | C |
| TC-M09-07 | Cập nhật ĐÃ TRẢ TIỀN cho học viên | Entry đã bán, chưa trả | Tick "Paid to student" (+ ngày trả) → Lưu; sau đó đăng nhập `student01` xem "My Exhibition Entries" | | Học viên thấy: giá, đã bán, giá khách trả, **đã được trả tiền** | C |
| TC-M09-08 | Không cho "Paid to student" khi chưa bán | Entry chưa bán | Tick "Paid to student" → Lưu | | Báo lỗi "Not sold yet" | C |
| TC-M09-09 | Bỏ đánh dấu bán / xóa entry đã bán | Entry đã bán & đã trả | Thử xóa entry | | Bị chặn hoặc yêu cầu xác nhận rõ ràng; không mất thông tin khách hàng âm thầm | T |

### M10 – Báo cáo (5 TC)

| ID | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|---|---|
| TC-M10-01 | Báo cáo bài BEST theo cuộc thi trong khoảng ngày | Đăng nhập `staff01`; seed có bài BEST rải rác ngày | Báo cáo → chọn cuộc thi "Hometown Landscapes", xếp loại BEST, từ ngày A đến ngày B → Xem | A, B bao quanh 2 trong 3 bài BEST | Hiện đúng 2 bài; tổng số đúng; khớp câu SQL kiểm tra | C |
| TC-M10-02 | Biên khoảng ngày (bao gồm 2 đầu) | Bài nộp đúng ngày A và đúng ngày B | Lọc A→B | | Cả 2 bài nằm trong kết quả (lọc từ 00:00 ngày A đến 23:59:59 ngày B) | C |
| TC-M10-03 | Từ ngày > Đến ngày | | Nhập A sau B | | Báo lỗi khoảng ngày | T |
| TC-M10-04 | Không có dữ liệu | | Chọn khoảng ngày năm 2000 | | Hiện "No data", không lỗi | T |
| TC-M10-05 | Báo cáo bán tranh | Seed 3 tranh đã bán | Báo cáo triển lãm → xem tổng doanh thu, đã trả/chưa trả học viên | | Tổng tiền = tổng soldPrice; số chưa trả khớp seed (1) | T |

### M11 – Manager (chỉ xem) & phân quyền chung (8 TC)

| ID | Tiêu đề | Tiền điều kiện | Các bước | Dữ liệu | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|---|---|
| TC-M11-01 | Manager xem học viên & nhân viên | Đăng nhập `manager01` | Mở 2 danh sách | | Xem được; **không** có nút Thêm/Sửa/Xóa | C |
| TC-M11-02 | Manager xem cuộc thi, giải đã trao, tất cả bài nộp theo cuộc thi | `manager01` | Mở từng trang | | Hiển thị đầy đủ, chỉ đọc | C |
| TC-M11-03 | Manager xem xếp loại/nhận xét của 1 bức tranh | `manager01` | Mở chi tiết 1 bài đã chấm | | Thấy xếp loại + điểm mạnh/yếu/cần cải thiện, không sửa được | T |
| TC-M11-04 | Manager quản lý triển lãm (được phép sửa) | `manager01` | Thêm/sửa 1 triển lãm sắp tới | | Thành công (đề bài cho phép Manager quản lý triển lãm) | T |
| TC-M11-05 | Manager không sửa được qua URL/POST | `manager01` | POST trực tiếp `/admin/students/{id}/delete` | | 403 | C |
| TC-M11-06 | STUDENT truy cập URL của STAFF/ADMIN | `student01` | Mở `/staff/submissions`, `/admin/staff`, `/manager/reports` | | 403 (trang "You do not have permission to access this page") cho cả 3 URL | C |
| TC-M11-07 | GUEST truy cập trang cần đăng nhập | Chưa đăng nhập | Mở `/student/submissions` | | Chuyển về `/login`; sau đăng nhập quay lại đúng trang | C |
| TC-M11-08 | STUDENT xem tranh triển lãm & nhận xét của mình | `student01` | Mở "My Results" và "Exhibition Entries" | | Thấy xếp loại + nhận xét tất cả bài đã nộp; thấy giá, đã bán chưa, đã trả tiền chưa, khách trả bao nhiêu; **chỉ** dữ liệu của chính mình (trang "All Best Works" thì thấy của mọi người) | C |

### Tổng hợp số lượng

| Module | M01 | M02 | M03 | M04 | M05 | M06 | M07 | M08 | M09 | M10 | M11 | **Tổng** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Số TC | 10 | 6 | 6 | 5 | 8 | 14 | 7 | 5 | 9 | 5 | 8 | **83** |
| Người chạy chính | QA | QA | QA | QA | QA | QA | QA | QA | QA | QA | QA | QA: 83 (theo 3 vòng – xem Phan_cong_task.xlsx) |

> Sau khi QA chạy xong một vòng, **người làm chức năng** (BE/FE phụ trách module) chạy lại các TC ưu tiên Cao của module mình để kiểm tra chéo.

### Gợi ý Selenium IDE cho người mới (regression)

1. Cài extension **Selenium IDE** trên Chrome → mở → "Record a new test in a new project" → nhập `http://localhost:5000`.
2. Thực hiện TC (VD: TC-M01-01) bằng tay; Selenium ghi lại các bước. Bấm Stop.
3. Thêm lệnh kiểm tra: chuột phải phần tử → *Selenium IDE → Assert → Text*.
4. Lưu project `FineArts_Regression.side` vào repo thư mục `test-data/selenium/`.
5. Đặt tên test trùng ID test case. Trước code freeze, bấm **Run all tests** – ghi kết quả vào Excel.
6. Ưu tiên ghi: TC-M01-01/03/08, TC-M06-01/03/10/13, TC-M07-02, TC-M09-05/07, TC-M11-06.

---

## 4. Kế hoạch dữ liệu mẫu (seed data)

### 4.1. Tài khoản

| Vai trò | Số lượng | Username | Ghi chú |
|---|---|---|---|
| ADMIN | 1 | `admin01` | |
| MANAGER | 1 | `manager01` | |
| STAFF | 3 | `staff01`, `staff02`, `staff03` | Mỗi người phụ trách lớp/môn khác nhau; `staff01` đã chấm bài |
| STUDENT | 10 | `student01` … `student10` | `student10` bị khóa (active = 0) để test TC-M01-07; `student07` chưa nộp bài nào |
| **Tổng** | **15** | | |

### 4.2. Cuộc thi (ngày tính tương đối so với ngày chạy seed **D**)

> Gợi ý cho BE: viết seed trong `Data/Seed/DbSeeder.cs`, ngày tháng dùng `DateTimeHelper.Today.AddDays(± n)` để trạng thái luôn đúng mỗi khi reset DB.

| # | Tên cuộc thi | startDate | endDate | Trạng thái | Giải thưởng | Bài nộp | Ghi chú |
|---|---|---|---|---|---|---|---|
| 1 | Hometown Landscapes | D-60 | D-30 | GRADED | First, Second, Third | 8 | Đã chấm đủ 6 loại (có 1 bài nộp trễ → DISQUALIFIED); đã trao 3 giải |
| 2 | Wildlife | D-45 | D-15 | GRADED | First, Second, Third, Honorable Mention | 6 | Đã chấm, đã trao 2 giải |
| 3 | City at Night | D-25 | D-1 | CLOSED | First, Second | 5 | **Chưa chấm** – để demo chấm bài |
| 4 | Colors of Autumn | D-10 | D+10 | ONGOING | First, Second, Third | 6 | Dùng test sửa/xóa bài, xóa cuộc thi bị chặn |
| 5 | Deadline Today | D-7 | **D** | ONGOING | First | 2 | Test biên nộp đúng ngày end date |
| 6 | Lunar New Year 2027 | D+5 | D+30 | UPCOMING | First, Second, Third | 0 | |
| 7 | Family Portrait | D+15 | D+45 | UPCOMING | First | 0 | Test xóa cuộc thi chưa có bài |

**Tổng bài nộp: 27** (thêm 1 bài trễ hạn đã nằm trong cuộc thi #1). Xếp loại phân bố cho cuộc thi #1–#2: BEST 4, BETTER 3, GOOD 3, MODERATE 2, NORMAL 1, DISQUALIFIED 1.

### 4.3. Triển lãm & bán tranh

| Triển lãm | Thời gian | Entry | Đã bán | Đã trả học viên |
|---|---|---|---|---|
| Young Artists Exhibition (Hanoi) | D-20 → D-5 (đã diễn ra) | 5 | 3 | 2 (1 chưa trả – test TC-M09-07) |
| Saigon Spring Exhibition | D+20 → D+27 (sắp tới) | 3 | 0 | 0 |
| **Customer** | 3 khách hàng (tên/SĐT/email giả, VD `khachhang1@example.com`) | | | |

### 4.4. Ảnh mẫu & bản quyền

- Cần **~30 ảnh JPEG** (27 bài nộp + dự phòng), kích thước 800–1600 px, ≤ 1 MB/ảnh. Đặt trong `Database/seed-images/` và copy sang `uploads/` khi seed.
- Nguồn ưu tiên (theo thứ tự):
  1. **Tự vẽ/tự tạo** (vẽ tay chụp lại, vẽ bằng Paint/Krita/Canva bản free) – an toàn nhất.
  2. **Unsplash** (unsplash.com/license), **Pexels** (pexels.com/license), **Pixabay** – giấy phép free, cho phép dùng không cần xin phép.
- **Bắt buộc ghi nguồn**: file `Database/seed-images/CREDITS.txt` theo mẫu: `fileName | nguồn | tác giả | link | giấy phép`. Đưa đoạn "Nguồn ảnh" vào User Guide.
- KHÔNG dùng ảnh lấy từ Google Images, Pinterest, tranh của họa sĩ nổi tiếng còn bản quyền, ảnh có logo/thương hiệu, ảnh có khuôn mặt người thật dễ nhận diện.
- Thơ/trích dẫn mẫu: **tiếng Anh**, tự viết (2–4 câu) hoặc trích tác phẩm đã thuộc phạm vi công cộng (ví dụ William Blake, Emily Dickinson) – ghi tên tác giả. Thêm 2–3 bài có thơ tiếng Việt có dấu để kiểm tra lưu & hiển thị Unicode.
- Tên người mẫu: tiếng Việt **không dấu** (ví dụ Nguyen Minh Khoa); tên cuộc thi, giải, lớp, khóa học, triển lãm: tiếng Anh.

### 4.5. Danh sách tài khoản test (đưa vào README.txt & Installation Guide)

| Vai trò | Username | Mật khẩu | Dùng để demo |
|---|---|---|---|
| ADMIN | `admin01` | `Admin@123` | Quản lý nhân viên, học viên |
| MANAGER | `manager01` | `Manager@123` | Xem toàn bộ, quản lý triển lãm |
| STAFF | `staff01` | `Staff@123` | Cuộc thi, chấm bài, trao giải, triển lãm, báo cáo |
| STAFF | `staff02`, `staff03` | `Staff@123` | |
| STUDENT | `student01` | `Student@123` | Nộp/sửa bài, xem kết quả, tranh triển lãm |
| STUDENT | `student02` … `student09` | `Student@123` | |
| STUDENT (bị khóa) | `student10` | `Student@123` | Test tài khoản bị khóa |
| SQL Server | Windows Authentication (mặc định trong `appsettings.json`); nếu dùng tài khoản SQL thì đặt bằng User Secrets – ghi lại trong README | – | Kết nối DB |

> Đây là mật khẩu **test** chỉ dùng cho bản nộp bài; mật khẩu lưu trong DB dạng BCrypt (seed mã hóa bằng `BCrypt.Net.BCrypt.HashPassword` trong `Data/Seed/DbSeeder.Accounts.cs`).

---

## 5. Unit Testing Check List & Final Check List

### 5.1. Unit Testing Check List (tài liệu nộp – BE1/BE2 điền, QA trình bày)

Mẫu bảng cho tài liệu Word:

| STT | Lớp / Phương thức | Mô tả kiểm tra | Dữ liệu vào | Kết quả mong đợi | Kết quả | Người test | Ngày |
|---|---|---|---|---|---|---|---|

Checklist nội dung tối thiểu:

**A. Unit test (xUnit + Moq)**
- [ ] `SubmissionService.CreateAsync` – từ chối khi hôm nay > endDate
- [ ] `SubmissionService.CreateAsync` – chấp nhận khi hôm nay = endDate
- [ ] `SubmissionService.CreateAsync` – từ chối khi cuộc thi UPCOMING
- [ ] `SubmissionService.UpdateAsync/DeleteAsync` – từ chối khi quá hạn
- [ ] `SubmissionService.UpdateAsync/DeleteAsync` – từ chối khi không phải chủ bài (IDOR)
- [ ] `FileStorageService.SaveSubmissionImageAsync` – chỉ chấp nhận JPEG (đuôi + content-type + magic bytes)
- [ ] `FileStorageService.SaveSubmissionImageAsync` – từ chối file rỗng / > 5 MB
- [ ] `FileStorageService.SaveSubmissionImageAsync` – tên lưu là Guid, đường dẫn chuẩn hóa nằm trong `uploads/`
- [ ] `FileStorageService.Delete` – xóa file cũ khi thay ảnh
- [ ] `CompetitionStatusResolver.Resolve` – trả đúng UPCOMING/ONGOING/CLOSED (3 test biên)
- [ ] `CompetitionService.DeleteAsync` – ném lỗi khi đã có Submission
- [ ] `CompetitionService.SaveAsync` – endDate < startDate bị từ chối
- [ ] `EvaluationService.EvaluateAsync` – bài nộp trễ → DISQUALIFIED
- [ ] `AwardService.IssueAsync` – chặn khi cuộc thi chưa CLOSED / bài DISQUALIFIED / giải đã trao
- [ ] `ExhibitionEntryService.MarkSoldAsync` – bắt buộc soldPrice > 0 và Customer
- [ ] `ExhibitionEntryService.MarkPaidAsync` – chặn khi chưa bán
- [ ] `ReportService.GetSubmissionReportAsync` – bao gồm 2 đầu khoảng ngày
- [ ] `StaffService.CreateAsync` / `StudentService.CreateAsync` – trùng username bị từ chối; mật khẩu được mã hóa BCrypt

**B. Kiểm tra mức màn hình/đơn vị giao diện (QA)**
- [ ] Mỗi form: trường bắt buộc có dấu `*` và thông báo lỗi bằng **tiếng Anh** thống nhất
- [ ] Mỗi form: nhập sai không làm mất dữ liệu đã gõ
- [ ] Mỗi danh sách: phân trang, tìm kiếm, "No results found"
- [ ] Mỗi nút Xóa: có hộp thoại xác nhận
- [ ] Mỗi trang: tiêu đề đúng, menu đúng vai trò
- [ ] Ảnh hiển thị đúng, có ảnh thay thế khi file bị thiếu
- [ ] Kết quả `dotnet test`: tất cả xanh – chụp màn hình đính kèm

### 5.2. Final Check List (trước khi nộp – Day 27, 2 người ký)

**Mã nguồn**
- [ ] Code freeze Day 24 đã tuân thủ; chỉ sửa bug Critical/High sau đó
- [ ] Mọi class/method/khối code có comment (yêu cầu spec: *every code block must have comments*)
- [ ] Không còn code chết, `System.out.println` debug, TODO chưa xử lý
- [ ] `dotnet build -c Release` thành công; `dotnet test` xanh
- [ ] Không có mật khẩu cá nhân thật trong code; chỉ tài khoản test

**Chức năng**
- [ ] 83/83 test case đã chạy; ≥ 95% Pass; 0 bug Critical/High mở
- [ ] Đã chạy UAT theo 5 vai trò (ADMIN, MANAGER, STAFF, STUDENT, GUEST)
- [ ] Regression Selenium chạy lại sau lần sửa cuối

**Cơ sở dữ liệu**
- [ ] File `.bak` restore được trên máy khác
- [ ] Script `.sql` (schema + seed) chạy được trên DB trống
- [ ] Ảnh seed có trong ZIP và khớp đường dẫn trong DB

**Tài liệu**
- [ ] Đủ 13 tài liệu bắt buộc + User Guide + Installation Guide (mục 6)
- [ ] Báo cáo dự án có synopsis, code, documentation; mục lục cập nhật số trang
- [ ] Ảnh chụp màn hình rõ nét, có chú thích
- [ ] Đã xuất PDF + giữ bản .docx

**Đóng gói**
- [ ] Cấu trúc ZIP đúng mục 8.1; tên ZIP đúng quy tắc
- [ ] README.txt có tài khoản đăng nhập & các bước chạy
- [ ] Phiếu phản hồi (Feedback form) đã điền
- [ ] Đã giải nén & cài lại thành công trên **máy sạch**
- [ ] Kiểm tra chéo bởi 2 người (mục 8.3) – ký tên & ngày

---

## 6. Kế hoạch tài liệu

### 6.1. Nguyên tắc chung

- Công cụ: **Microsoft Word / WPS Writer** (file .docx, xuất thêm PDF), **draw.io (diagrams.net)** cho ERD/flowchart/sơ đồ, **Excel** cho Task Sheet & test case, Snipping Tool cho ảnh màn hình.
- Định dạng thống nhất: font Times New Roman 13 (hoặc Calibri 12), heading dùng Style Heading 1/2/3 (để tạo mục lục tự động), lề 2.5 cm, đánh số trang, header ghi "Institute of Fine Arts – eProject".
- Ngôn ngữ: bản nộp bằng **tiếng Anh** (team nước ngoài chấm); nháp viết tiếng Việt, dịch sang tiếng Anh khi chốt (Day 25–27). Ảnh chụp màn hình lấy từ giao diện tiếng Anh. Thuật ngữ vai trò/thực thể giữ nguyên tiếng Anh như mục quy ước.
- Tất cả tài liệu lưu trong repo thư mục `docs/` (nháp) → bản cuối ghép thành **Project Report** (có Synopsis + toàn bộ tài liệu + phụ lục code chính).
- Quy trình: **người được giao viết nháp → người review góp ý (comment trong Word) → người viết sửa → QA ghép vào Project Report**.

### 6.2. Bảng phân công & hạn

| # | Tài liệu | Nội dung cần có (dàn ý) | Viết nháp | Review | Hạn nháp | Hạn chốt | Công cụ |
|---|---|---|---|---|---|---|---|
| 1 | **Certificate of Completion** | Tên dự án, tên nhóm/khóa/nhóm, thành viên + mã HV, giảng viên hướng dẫn, ngày, chữ ký (theo mẫu trung tâm) | QA | BE1 | Day 25 | Day 26 | Word |
| 2 | **Table of Contents** | Mục lục tự động (References → Table of Contents), cập nhật cuối cùng | QA | BE1 | Day 26 | Day 27 | Word |
| 3 | **Problem Definition** | 1. Giới thiệu khách hàng 2. Hiện trạng (quản lý giấy tờ, khó tra cứu) 3. Vấn đề cần giải quyết 4. Giải pháp đề xuất (website) 5. Phạm vi (trong/ngoài phạm vi) 6. Đối tượng người dùng (5 vai trò) | BE2 | BE1 | Day 2 | Day 3 | Word |
| 4 | **Customer Requirement Specification (CRS)** | 1. Mục đích 2. Mô tả vai trò 3. Yêu cầu chức năng theo module M01–M11 (bảng: mã YC, mô tả, vai trò, ưu tiên) 4. Yêu cầu phi chức năng (bảo mật, hiệu năng, upload JPEG ≤ 5 MB, trình duyệt) 5. Yêu cầu tài chính (giá giải, giá học viên, giá bán) 6. Sơ đồ Use Case 7. Ràng buộc & giả định | BE2 | BE1 | Day 3 | Day 4 | Word + draw.io |
| 5 | **Project Plan** | 1. Mục tiêu 2. Thành viên & vai trò 3. Mốc (Sprint 1–3 xong chức năng Day 17, review & chỉnh sửa Day 18–23, code freeze Day 24, nộp Day 28) 4. Gantt chart 5. Rủi ro & phương án 6. Công cụ & môi trường 7. Lịch status report (Day 7 và Day 25) | QA | BE1 | Day 2 | Day 3 | Word + Excel (Gantt) |
| 6 | **E-R Diagrams** | 1. ERD mức khái niệm (11 thực thể chuẩn) 2. ERD vật lý (bảng, PK/FK, kiểu dữ liệu) 3. Từ điển dữ liệu (bảng: cột, kiểu, null, mô tả) 4. Giải thích quan hệ (1-n, 1-1) | BE2 (vẽ theo schema đã có) | BE1 | Day 4 | Day 6 (cập nhật lại Day 24 nếu đổi) | draw.io + Word |
| 7 | **Algorithms** | Xem mục 7: pseudo-code + flowchart 5 thuật toán chính | BE1 (pseudo-code) + QA (vẽ flowchart) | BE1 | Day 13 | Day 16 | draw.io + Word |
| 8 | **GUI Standards Document** | 1. Bảng màu, font, logo 2. Bố cục chung (header/menu/footer) 3. Quy chuẩn nút (màu theo hành động: Lưu = primary, Xóa = danger) 4. Quy chuẩn form (nhãn, `*`, thông báo lỗi) 5. Bảng/phân trang 6. Thông báo (alert success/error) 7. Responsive Bootstrap 5 | FE1 | FE2 | Day 4 | Day 6 | Word |
| 9 | **Interface Design Document** | Mỗi màn hình: tên, vai trò truy cập, URL, ảnh chụp/wireframe, mô tả các trường & nút, điều hướng; sơ đồ sitemap theo vai trò | FE2 (QA chụp màn hình) | FE1 | Day 9 (wireframe) | Day 25 (ảnh thật) | Word + draw.io |
| 10 | **Task Sheet** | Bảng: STT, công việc, module, người làm, ngày bắt đầu, ngày kết thúc dự kiến/thực tế, trạng thái, ghi chú – lấy từ `docs/Phan_cong_task.xlsx`, cập nhật cuối mỗi sprint | QA (xuất từ Phan_cong_task.xlsx) | BE1 | Day 2 (tạo) | Cập nhật Day 5, 11, 17, chốt Day 27 | Excel |
| 11 | **Project Review and Monitoring Report** | Mỗi kỳ: tiến độ % theo module, việc đã xong, việc đang làm, khó khăn & cách xử lý, kế hoạch kỳ tới, minh chứng (ảnh, script) – dùng làm nội dung 2 email STATUS | QA (số liệu từ Kanban & Phan_cong_task.xlsx) | BE1 | Day 7 (kỳ 1), Day 24 (kỳ 2) | Day 7, Day 25 | Word |
| 12 | **Unit Testing Check List** | Mục 5.1 + kết quả `dotnet test` + bảng tổng hợp test case Pass/Fail | QA (trình bày) + BE1, BE2 (unit test) | BE1 | Day 19 | Day 24 | Word + Excel |
| 13 | **Final Check List** | Mục 5.2, tick & ký | QA | BE1 | Day 26 | Day 27 | Word |
| 14 | **User Guide (Hướng dẫn sử dụng)** | 1. Giới thiệu 2. Đăng nhập/đăng xuất 3. Hướng dẫn theo vai trò (GUEST, STUDENT, STAFF, MANAGER, ADMIN) – mỗi chức năng: mục đích, các bước, ảnh chụp 4. Câu hỏi thường gặp & lỗi thường gặp 5. Nguồn ảnh | FE1 (GUEST, STUDENT, STAFF) + FE2 (ADMIN, MANAGER) | QA (làm thử theo hướng dẫn) | Day 18 | Day 25 | Word |
| 15 | **Installation Guide (Hướng dẫn cài đặt)** | 1. Yêu cầu hệ thống (.NET 10 SDK, SQL Server, SSMS) 2. Restore DB từ `.bak` hoặc chạy `.sql` 3. Cấu hình `appsettings.json` (chuỗi kết nối) 4. Copy thư mục ảnh `uploads/` 5. Chạy ứng dụng (`mvn spring-boot:run` / `java -jar`) 6. Truy cập `http://localhost:5000` 7. **Bảng tài khoản đăng nhập** (mục 4.5) 8. Lỗi thường gặp (cổng 5000 bận, TCP/IP SQL Server chưa bật, sai mật khẩu `sa`) | BE2 | QA (cài thử trên máy sạch) | Day 24 | Day 26 (sau test máy sạch) | Word |
| — | **Project Report (bản ghép)** | Bìa → Certificate → Mục lục → **Synopsis** (1–2 trang) → tài liệu 3–13 → User Guide → Installation Guide → Phụ lục: code chính có comment | QA | BE1 | Day 26 | **Day 27** | Word → PDF |

> Ghi nhớ yêu cầu spec: *mọi khối code phải có comment; phải giải thích logic chương trình; báo cáo hoàn chỉnh gồm synopsis, mã nguồn và tài liệu.* BE1, BE2, FE1, FE2 tự rà comment code Day 18–24; QA kiểm tra ngẫu nhiên 10 file (có XML comment `///` cho class/method public không) và ghi vào Final Check List.

### 6.3. Hướng dẫn chụp ảnh màn hình (cho QA)

1. Dùng dữ liệu seed đẹp (không có chữ "test", "asdf").
2. Trình duyệt 100% zoom, cửa sổ 1366×768 hoặc 1920×1080, ẩn bookmark bar.
3. Đặt tên: `Mxx_<vai trò>_<màn hình>.png` (VD `M06_STUDENT_nop_bai.png`), lưu vào `Screenshots/`.
4. Chèn vào Word kèm chú thích "Hình x: …" (References → Insert Caption).

---

## 7. Gợi ý tài liệu "Algorithms"

Cấu trúc mỗi thuật toán: **(1) Mục đích · (2) Đầu vào / Đầu ra · (3) Pseudo-code · (4) Flowchart (draw.io) · (5) Vị trí trong code (class/method)**.

### 7.1. Tính trạng thái cuộc thi

```
FUNCTION getStatus(competition, today)
    // Đầu vào: startDate, endDate của cuộc thi; ngày hiện tại
    IF today < competition.startDate THEN RETURN "UPCOMING"
    ELSE IF today <= competition.endDate THEN RETURN "ONGOING"   // ngày end date vẫn còn hạn
    ELSE RETURN "CLOSED"   // GRADED chỉ đặt khi Staff công bố
END FUNCTION
```
Vị trí: `CompetitionStatusResolver.Resolve()`.

### 7.2. Kiểm tra hạn nộp (nộp / sửa / xóa bài)

```
FUNCTION canModifySubmission(student, competition, submission?, now)
    status ← getStatus(competition, now.date)
    IF status = "UPCOMING" THEN THROW "The competition has not started yet"
    IF status <> "ONGOING" THEN THROW "The competition has ended. Submissions can no longer be added, edited or deleted."
    IF submission ≠ null AND submission.student ≠ student THEN THROW AccessDenied   // chống IDOR
    RETURN true
END FUNCTION
```
Vị trí: `SubmissionService.CreateAsync/UpdateAsync/DeleteAsync()` – gọi **ở server**, không chỉ ẩn nút ở giao diện.

### 7.3. Upload file an toàn (File I/O)

```
FUNCTION storeImage(multipartFile)
    IF file is empty THEN THROW "Please choose an image"
    IF file.size > 5 MB THEN THROW "Maximum file size is 5 MB"
    ext ← lowercase(extension(file.originalName))
    IF ext NOT IN {"jpg","jpeg"} THEN THROW "Only JPEG images are accepted"
    IF file.contentType ≠ "image/jpeg" THEN THROW "Only JPEG images are accepted"
    IF first 3 bytes ≠ FF D8 FF THEN THROW "The file is not a valid JPEG image"
    newName ← Guid.NewGuid() + ".jpg"                    // bỏ hoàn toàn tên gốc → chống path traversal & ký tự lạ
    target ← normalize(uploadDir / newName)
    IF target NOT startsWith uploadDir THEN THROW "Invalid file path"
    copy(file → target)                          // FileStream + IFormFile.CopyToAsync
    RETURN newName                               // lưu tên này vào Submission.imagePath
END FUNCTION

FUNCTION replaceImage(oldName, newFile)
    newName ← storeImage(newFile)
    delete(uploadDir / oldName) IF exists
    RETURN newName
END FUNCTION
```
Vị trí: `FileStorageService`.

### 7.4. Xếp loại & chọn bài gửi triển lãm

```
FUNCTION evaluate(submission, staff, grade, positive, negative, improvement)
    IF submission.submittedAt.date > submission.competition.endDate THEN
        grade ← "DISQUALIFIED"                   // nộp trễ luôn bị loại
    SAVE Evaluation(submission, staff, grade, positive, negative, improvement, today)
END FUNCTION

FUNCTION listCandidatesForExhibition(exhibition)
    RETURN submissions WHERE evaluation.grade IN {"BEST"}       // có thể mở rộng thêm "BETTER"
                       AND submission NOT IN exhibition.entries
                       ORDER BY evaluation.grade, submittedAt
END FUNCTION

FUNCTION addToExhibition(exhibition, submission, studentPrice)
    IF studentPrice <= 0 THEN THROW "Invalid price"
    IF exists(exhibition, submission) THEN THROW "This painting is already in an exhibition"
    SAVE ExhibitionEntry(exhibition, submission, studentPrice, sold=false, paidToStudent=false)
END FUNCTION
```

### 7.5. Cập nhật bán tranh & trả tiền học viên

```
FUNCTION markSold(entry, soldPrice, soldDate, customerInfo)
    IF entry.sold THEN THROW "Sold"
    IF soldPrice <= 0 OR customerInfo missing THEN THROW "Sold price and customer are required"
    customer ← findOrCreate Customer(customerInfo)
    entry.sold ← true; entry.soldPrice ← soldPrice; entry.soldDate ← soldDate; entry.customer ← customer
    SAVE entry
END FUNCTION

FUNCTION markPaidToStudent(entry, paidDate)
    IF NOT entry.sold THEN THROW "The painting has not been sold, so payment cannot be recorded"
    entry.paidToStudent ← true; entry.paidDate ← paidDate
    SAVE entry
END FUNCTION
```

### 7.6. (Phụ) Báo cáo theo khoảng ngày

```
FUNCTION reportBest(competitionId, fromDate, toDate, grade = "BEST")
    IF fromDate > toDate THEN THROW "Invalid date range"
    RETURN submissions WHERE competition = competitionId
                       AND submittedAt >= fromDate 00:00:00
                       AND submittedAt <  (toDate + 1 day) 00:00:00     // bao gồm trọn ngày cuối
                       AND evaluation.grade = grade
END FUNCTION
```

---

## 8. Đóng gói & nộp bài

### 8.1. Cấu trúc thư mục ZIP chuẩn

```
<Batch>_<Group>_InstituteOfFineArts/
├── README.txt                      ← đọc đầu tiên: giới thiệu, yêu cầu, các bước chạy, TÀI KHOẢN ĐĂNG NHẬP, tài khoản DB
├── SourceCode/
│   └── institute-of-fine-arts/     ← solution .NET (.sln, src/, tests/) – KHÔNG kèm bin/, obj/, .vs/
│       └── uploads/                ← ảnh seed (để app hiển thị ngay sau khi chạy)
├── Database/
│   ├── IFA_DB.bak              ← bản backup SQL Server
│   ├── 01_schema.sql               ← tạo bảng
│   ├── 02_seed_data.sql            ← dữ liệu mẫu
│   └── seed-images/ + CREDITS.txt  ← ảnh mẫu & nguồn ảnh
├── Documentation/
│   ├── ProjectReport_InstituteOfFineArts.pdf   ← bản ghép đầy đủ (có Synopsis)
│   ├── ProjectReport_InstituteOfFineArts.docx
│   ├── 01_CertificateOfCompletion.docx … 13_FinalCheckList.docx
│   ├── UserGuide.pdf
│   ├── InstallationGuide.pdf
│   ├── ERD.drawio (+ .png)
│   └── TestCases_FineArts.xlsx, Phan_cong_task.xlsx
├── Screenshots/                    ← ảnh màn hình theo vai trò (Mxx_ROLE_screen.png)
└── Feedback/
    └── FeedbackForm.docx           ← phiếu phản hồi đã điền
```

### 8.2. Quy tắc đặt tên ZIP

- Mẫu: **`<Batch>_<Group>_<ProjectTitle>.zip`** – không dấu, không khoảng trắng.
- Ví dụ: `T1.2409.E1_Group3_InstituteOfFineArts.zip` (thay bằng mã khóa và số nhóm thật của lớp).
- Dung lượng: cố gắng < 100 MB (nén ảnh seed; không đưa `bin/`, `obj/`, `.vs/`).

### 8.3. Checklist kiểm tra chéo 2 người (Day 27)

Người A (QA) đóng gói, người B (FE2) kiểm tra **trên máy khác**; sau đó đổi vai với Documentation.

| # | Mục kiểm tra | Người A | Người B |
|---|---|---|---|
| 1 | Tên ZIP đúng quy tắc | [ ] | [ ] |
| 2 | Giải nén không lỗi, đủ 5 thư mục + README.txt | [ ] | [ ] |
| 3 | README có tài khoản 5 vai trò + tài khoản DB + bước chạy | [ ] | [ ] |
| 4 | Restore `.bak` thành công **hoặc** chạy 2 file `.sql` thành công | [ ] | [ ] |
| 5 | App chạy, đăng nhập được 4 vai trò (+ xem được như GUEST) | [ ] | [ ] |
| 6 | Ảnh seed hiển thị (không ảnh vỡ) | [ ] | [ ] |
| 7 | Nộp thử 1 bài JPEG → thành công | [ ] | [ ] |
| 8 | Đủ 13 tài liệu + User Guide + Installation Guide, mở được | [ ] | [ ] |
| 9 | PDF báo cáo có mục lục đúng số trang, có Synopsis | [ ] | [ ] |
| 10 | Feedback form đã điền | [ ] | [ ] |
| 11 | Không có file rác (`.vs`, `bin`, `obj`, `~$*.docx`, `Thumbs.db`) | [ ] | [ ] |
| 12 | Tiêu đề email nộp bắt đầu bằng `PROJECT SUBMISSION:` | [ ] | [ ] |
| Ký tên / ngày | | | |

### 8.4. Test cài đặt lại trên máy sạch (Day 26)

1. Dùng máy của FE2 hoặc QA **chưa từng chạy dự án** (hoặc tạo user Windows mới / máy ảo).
2. Chỉ cài theo đúng **Installation Guide** – không hỏi BE. Ghi lại mọi bước bị vướng.
3. Bấm giờ: mục tiêu ≤ 30 phút từ giải nén đến đăng nhập được.
4. Chạy 10 test case ưu tiên Cao (TC-M01-01, TC-M02-01, TC-M05-06, TC-M06-01, TC-M06-03, TC-M06-10, TC-M07-02, TC-M09-05, TC-M09-07, TC-M11-06).
5. Bước nào vướng → sửa Installation Guide/README ngay, đóng gói lại, lặp lại bước 1–4.

### 8.5. Lịch nộp liên quan

| Ngày | Việc |
|---|---|
| Day 7 | Email `STATUS:` lần 1 + Project Review & Monitoring Report kỳ 1 + ZIP minh chứng |
| Day 24 | Code freeze; regression; Unit Testing Check List chốt |
| Day 25 | Email `STATUS:` lần 2 (3 ngày trước khi kết thúc) + Report kỳ 2; User Guide chốt |
| Day 26 | Test máy sạch; Installation Guide & Certificate chốt; ghép Project Report |
| Day 27 | Final Check List; đóng gói ZIP; kiểm tra chéo 2 người |
| **Day 28** | **Nộp ZIP duy nhất** (`PROJECT SUBMISSION:`) – nộp buổi sáng, không để sát giờ |

---

## 9. Chuẩn bị demo / bảo vệ

### 9.1. Kịch bản demo (≈ 15 phút)

| Thời gian | Phần | Người trình bày | Nội dung / thao tác | Tài khoản |
|---|---|---|---|---|
| 0:00–1:30 | Mở đầu | BE1 | Giới thiệu nhóm, bài toán (quản lý giấy → website), kiến trúc (ASP.NET Core MVC + Razor + EF Core + SQL Server), ERD 1 slide | — |
| 1:30–3:00 | GUEST – Trang chủ | BE2 | Cuộc thi sắp/đang diễn ra, số bài nộp, danh sách đạt giải; xem chi tiết cuộc thi | (không đăng nhập) |
| 3:00–5:30 | STUDENT | FE1 | Đăng nhập → xem cuộc thi "Colors of Autumn" → nộp JPEG + thơ → sửa bài → **thử nộp file PNG (bị chặn)** → **cuộc thi đã kết thúc: không có nút sửa** → xem kết quả/nhận xét → xem tranh triển lãm (đã bán, đã được trả tiền) | `student01` |
| 5:30–9:00 | STAFF | BE1 | Tạo cuộc thi + giải → chấm bài "City at Night" (BEST + nhận xét) → **thử xóa cuộc thi có bài (bị chặn)** → trao giải → gửi tranh BEST đi triển lãm với giá học viên → đánh dấu đã bán + khách hàng → đã trả tiền → báo cáo BEST theo khoảng ngày | `staff01` |
| 9:00–10:30 | ADMIN | FE2 | Thêm nhân viên/học viên, tìm kiếm, khóa tài khoản | `admin01` |
| 10:30–12:00 | MANAGER | QA | Xem toàn bộ (chỉ đọc), xem xếp loại 1 bức tranh, quản lý triển lãm sắp tới | `manager01` |
| 12:00–13:30 | Bảo mật & chất lượng | QA + BE1 | STUDENT mở URL `/staff/...` → 403; mật khẩu BCrypt trong DB; upload đổi tên Guid; `dotnet test` xanh; số liệu test (83 TC) | — |
| 13:30–15:00 | Kết luận | BE1 | Hạn chế & hướng phát triển (thanh toán online, email thông báo, phân trang nâng cao); cảm ơn | — |

**Chuẩn bị trước buổi demo:** reset DB bằng seed; mở sẵn 3 trình duyệt/tab ẩn danh cho 3 vai trò; tắt thông báo Windows; zoom trình duyệt 110–125%; có **video quay sẵn demo** (OBS/Xbox Game Bar) phòng khi máy lỗi; kiểm tra máy chiếu.

### 9.2. Câu hỏi hay bị hỏi & gợi ý trả lời

| # | Câu hỏi | Ai trả lời | Gợi ý trả lời |
|---|---|---|---|
| 1 | Làm sao chặn nộp bài sau hạn? Nếu học viên sửa HTML/gửi request trực tiếp thì sao? | BE1 | Kiểm tra ở **service phía server** (`SubmissionService` dùng `CompetitionStatusResolver.IsOpenForSubmission`) so ngày hiện tại với endDate, không chỉ ẩn nút; đã test bằng Postman (TC-M06-10/13). |
| 2 | Ảnh lưu ở đâu? Vì sao không lưu vào DB? | BE1 | Lưu file bằng File I/O vào `uploads/`, DB chỉ lưu tên file → DB nhẹ, đúng yêu cầu đề "phải dùng File I/O". |
| 3 | Làm sao biết file đúng là JPEG? | BE1 / QA | Kiểm tra 3 lớp: phần mở rộng, content-type, magic bytes `FF D8 FF`; giới hạn 5 MB. |
| 4 | Chống path traversal / trùng tên file thế nào? | BE1 | Bỏ tên gốc, đặt tên Guid; chuẩn hóa đường dẫn và kiểm tra nằm trong `uploads/`. |
| 5 | Phân quyền làm thế nào? | BE1 | Cookie Authentication + `[Authorize(Roles = ...)]` theo Area: phân theo URL `/admin/**`, `/staff/**`, `/manager/**`, `/student/**` + kiểm tra chủ sở hữu bài trong service. |
| 6 | Mật khẩu lưu thế nào? | BE2 | Băm BCrypt (thư viện BCrypt.Net-Next) trước khi lưu; khi đăng nhập dùng `BCrypt.Verify`; không lưu mật khẩu thô. |
| 7 | Chống SQL injection, XSS? | BE1 / FE1 | EF Core/LINQ tự tham số hóa câu lệnh; Razor tự mã hóa HTML khi hiển thị; đã test TC-M01-06, TC-M07-06. |
| 8 | Vì sao không cho xóa cuộc thi đã có bài nộp? | BE1 | Tránh mất bài và kết quả chấm/giải của học viên; tính toàn vẹn dữ liệu (khóa ngoại). |
| 9 | Trạng thái cuộc thi lưu trong DB hay tính? | BE2 | Cả hai: khi đọc luôn tính lại từ startDate/endDate (`CompetitionStatusResolver`) nên không sai lệch; job chạy nền (`StatusSyncBackgroundService`) đồng bộ cột status mỗi giờ để lọc/báo cáo nhanh; riêng GRADED do giáo viên công bố. |
| 10 | Bài nộp trễ xử lý thế nào? | BE1 | Hệ thống **chặn** nộp/sửa/xóa sau 23:59:59 ngày kết thúc (kiểm tra ở server – BR-13, BR-14), nên không có bài trễ. Xếp loại DISQUALIFIED dùng khi bài vi phạm thể lệ hoặc nhập bù ngoại lệ. |
| 11 | Quy trình bán tranh & trả tiền học viên? | BE1 / FE1 | ExhibitionEntry: giá học viên → đánh dấu đã bán + giá bán + Customer → đánh dấu đã trả học viên (chỉ khi đã bán). |
| 12 | Nhóm kiểm thử thế nào? | QA | 83 test case thủ công M01–M11, báo bug bằng GitHub Issues, regression bằng Selenium IDE, unit test xUnit/Moq cho service chính, test cài đặt trên máy sạch. |
| 13 | Phân công công việc trong nhóm? | QA | Trình bày Task Sheet: BE1 + FE1 phần nghiệp vụ khó (nộp bài, chấm, trao giải, bán tranh), BE2 + FE2 phần quản lý & trang xem (Admin, trang chủ, báo cáo, Manager), QA kiểm thử & tài liệu; BE1 review mọi PR. |
| 14 | Nếu có thêm thời gian sẽ làm gì? | FE2 | Thanh toán online, gửi email thông báo kết quả, giao diện quản lý ảnh nâng cao, triển khai lên cloud. |
| 15 | Chỉ ra đoạn code có comment / giải thích thuật toán X | Người được hỏi | Mở `FileStorageService`/`SubmissionService`, đọc comment; đối chiếu tài liệu Algorithms. |

> **Luyện tập:** Diễn tập đầy đủ 2 lần (Day 26 và Day 27), bấm giờ. Mỗi người phải tự trả lời được ít nhất 5 câu trong bảng trên — BE2 luyện câu 6, 9; QA luyện câu 3, 12, 13.

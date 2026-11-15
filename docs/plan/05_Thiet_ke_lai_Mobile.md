# 05 – Thiết kế lại app mobile (Flutter)

> Phạm vi: thư mục `mobile/` + API `/api/...` trong `src/InstituteOfFineArts.Web` (nhánh `feature/mobile-api`).
> Ngày lập: 15/11/2026. Người quản lý: BE1. Thực hiện: các agent theo mục 6.

## 1. Vấn đề của bản hiện tại

| Vấn đề | Chi tiết |
|---|---|
| Không có bản sắc | Theme Material 3 mặc định, tông hồng; không dùng màu và font của web (navy `#0B2A33`, vàng đồng `#A8814A`, Bodoni Moda + General Sans). |
| Không có hình ảnh | Một app về **tranh** nhưng không hiện ảnh bìa cuộc thi, tranh đoạt giải hay kho tranh. |
| Trang chủ nghèo nội dung | Chỉ là danh sách chữ "Đang diễn ra / Sắp diễn ra / Đạt giải"; không giới thiệu học viện, không dẫn tới tranh. |
| Khách không có gì để xem ngoài trang chủ | Không có danh sách cuộc thi đầy đủ, không có gallery. |
| Sau đăng nhập không có "trang chủ" | Học viên vào thẳng tab Cuộc thi; Staff vào thẳng tab Chấm bài; không có tổng quan việc cần làm. |
| Ngôn ngữ | Giao diện app tiếng Việt trong khi web đã chốt **chỉ tiếng Anh** (PR #57). |

## 2. Phạm vi chức năng (chốt từ thảo luận trước)

App phục vụ **Khách, Học viên, Staff**. Manager và Admin dùng web (app hiện màn hình "Please use the website").
Không thêm nghiệp vụ mới: mọi thao tác ghi đi qua API hiện có (PR #53), luật nghiệp vụ giữ nguyên như web.

| Vai trò | Chức năng trên app |
|---|---|
| Khách | Xem trang chủ, danh sách và chi tiết cuộc thi, kết quả và người đạt giải, kho tranh công khai, xem tranh phóng to; đăng nhập. |
| Học viên | Tổng quan cá nhân; xem cuộc thi; nộp / sửa / xoá bài (JPEG ≤ 5 MB + thơ / trích dẫn / câu chuyện); bài của tôi + kết quả chấm + giải; triển lãm: đề xuất tranh, sửa giá, rút đề xuất, theo dõi bán và trả tiền; tài khoản. |
| Staff | Tổng quan việc cần làm; chấm bài (xem tranh phóng to, lưu / xoá đánh giá, sang bài kế tiếp); trao giải, công bố kết quả, thu hồi giải; triển lãm: tạo / sửa, duyệt đề xuất, ghi nhận bán, trả tiền học viên; tài khoản. |

## 3. Hệ thiết kế (đồng bộ với web)

- **Màu:** nền tường `#FFFFFF`, dải đá `#E6E3D9` (khung quanh tranh), sương `#EAF0F3`, mực navy `#0B2A33` (màu chính, thanh điều hướng), chữ graphite `#2B2924`, chữ phụ `#625D53`, đường kẻ `#D6D2C8`, vàng đồng `#A8814A` (viền tranh đoạt giải, điểm nhấn), vàng đồng chữ nhỏ `#7A5B2E`, nguy hiểm `#A23B2A`. Trạng thái: sắp diễn ra `#2C5D8A`, đang diễn ra `#2F6B4F`, đã đóng `#625D53`, đã công bố = navy.
- **Chữ:** tiêu đề **Bodoni Moda** (serif, nghiêng cho tên tranh như "wall label"), nội dung **General Sans**. Font đóng gói trong `mobile/assets/fonts/` (TTF chuyển từ woff2 của web), không tải từ mạng.
- **Tranh là trung tâm:** ảnh tranh có viền "passe-partout" màu đá; tranh đoạt giải viền vàng đồng; nhãn kiểu bảo tàng (tên tranh nghiêng, tác giả, cuộc thi).
- **Hình khối:** góc bo nhỏ (4–8), nút chính navy chữ trắng, nút phụ viền navy; khoảng trắng rộng; không dùng màu hồng / tím mặc định.
- **Ngôn ngữ:** toàn bộ chữ trên app **tiếng Anh** (giống web). Thông báo lỗi lấy `title` từ API (đã tiếng Anh).
- **Trợ năng:** chữ ≥ 14, vùng bấm ≥ 44×44, ảnh có `semanticLabel`, tương phản ≥ 4.5:1, tôn trọng "reduce motion".

## 4. Danh sách màn hình

### 4.1 Khách (thanh dưới: Home · Competitions · Gallery · Sign in)

| Mã | Màn hình | Nội dung |
|---|---|---|
| M-G1 | **Home** | Phần đầu: tên học viện + một câu giới thiệu + ảnh tranh đoạt giải mới nhất làm nền; nút "Browse competitions". **Open now**: thẻ ngang có ảnh bìa, đếm ngược hạn nộp, số bài. **Coming soon**. **Latest winners**: lưới tranh đoạt giải (ảnh + tên tranh + học viên + giải) của cuộc thi công bố gần nhất. **From the gallery**: dải ảnh ngang tranh công khai → "See the gallery". |
| M-G2 | **Competitions** | Ba mục chọn: Open (đang diễn ra) · Upcoming · Results (đã công bố). Thẻ có ảnh bìa, trạng thái, ngày. |
| M-G3 | **Competition detail** | Ảnh bìa lớn, chip trạng thái, mã, ngày + đếm ngược; Overview; Rules; Prizes (thẻ giải: hạng, giá trị, số suất); Winners (lưới tranh) khi đã công bố; nút cuối trang theo trạng thái: "Submit your entry" (học viên, đang mở), "Sign in to enter" (khách), "Opens on …", "Submissions closed", "View all artworks". |
| M-G4 | **Gallery** | Lưới tranh công khai (2 cột), tải thêm khi cuộn; lọc theo chủ đề (Animals, Landscape, People, Flora, Abstract…) như mega-menu web. |
| M-G5 | **Artwork** | Tranh toàn màn hình, phóng to / kéo bằng hai ngón (InteractiveViewer); nhãn: tên tranh, học viên, cuộc thi, giải. |
| M-G6 | **Sign in** | Thương hiệu học viện, ô tên đăng nhập / mật khẩu, thông báo lỗi rõ (sai mật khẩu, tài khoản bị khoá). |

### 4.2 Học viên (thanh dưới: Home · Competitions · My entries · Exhibitions · Account)

| Mã | Màn hình | Nội dung |
|---|---|---|
| M-S1 | **Home** | "Good morning, {name}"; thẻ việc cần làm: cuộc thi đang mở **chưa nộp bài** (đếm ngược, nút Submit); bài đã nộp có thể sửa đến hạn; kết quả mới (xếp loại / giải); đề xuất triển lãm đang chờ duyệt / tranh đã bán chưa nhận tiền. |
| M-S2 | Competitions | Như M-G2/M-G3; nút "Submit your entry" hoặc "View my entry" nếu đã nộp. |
| M-S3 | **Submit / Edit entry** | Chọn ảnh (xem trước ngay), tiêu đề, mô tả, loại phần chữ (Poem / Quotation / Story / Other) dạng chip, nội dung chữ; báo trước giới hạn JPEG ≤ 5 MB; lỗi theo từng trường. |
| M-S4 | **My entries** | Danh sách có ảnh thu nhỏ, cuộc thi, trạng thái (Submitted / Waiting for results / Rated BEST… / Award), lọc theo cuộc thi và xếp loại. Gộp tab "Results" cũ vào đây. |
| M-S5 | **Entry detail** | Tranh phóng to được; phần chữ; đánh giá (khi đã công bố) dạng thẻ: xếp loại, điểm mạnh, điểm yếu, cần cải thiện; giải; nút Edit / Delete khi còn hạn. |
| M-S6 | **Exhibitions** | Triển lãm sắp tới có thể gửi tranh; đề xuất của tôi (trạng thái, giá, đã bán, đã nhận tiền); đề xuất / sửa giá / rút như hiện tại. |
| M-S7 | **Account** | Tên, mã học viên, email, lớp; đăng xuất. |

### 4.3 Staff (thanh dưới: Home · Grading · Awards · Exhibitions · Account)

| Mã | Màn hình | Nội dung |
|---|---|---|
| M-T1 | **Home** | Việc cần làm: cuộc thi đã đóng còn bài chưa chấm (thanh tiến độ), cuộc thi chấm xong chưa công bố, đề xuất triển lãm đang chờ duyệt, tranh đã bán chưa trả tiền học viên. Mỗi thẻ dẫn thẳng tới màn hình xử lý. |
| M-T2 | Grading | Danh sách cuộc thi + tiến độ; danh sách bài (lọc đã / chưa chấm, xếp loại, tìm kiếm) **có ảnh thu nhỏ**. |
| M-T3 | **Grade entry** | Tranh lớn phóng to được ở đầu; thông tin bài + phần chữ; form đánh giá (chip xếp loại, ba ô nhận xét); Save / Save & next; xoá đánh giá. |
| M-T4 | Awards | Như hiện tại (trao, sửa, xoá, công bố, thu hồi) nhưng **thẻ giải có ảnh tranh** của người nhận và ứng viên. |
| M-T5 | Exhibitions | Như hiện tại, thêm ảnh tranh ở danh sách đề xuất và chi tiết. |
| M-T6 | Account | Như M-S7 (mã nhân viên, chức vụ). |

## 5. API bổ sung (không đổi CSDL)

Ảnh công khai (bìa cuộc thi, tranh BEST / đoạt giải của cuộc thi đã công bố) tải thẳng bằng URL công khai của web, không cần token:
`GET /files/competitions/{id}/cover`, `GET /files/submissions/{id}/thumb`, `GET /files/submissions/{id}`.
Ảnh riêng tư (bài của chính học viên, mọi bài với Staff) tải qua API kèm JWT.

| Thay đổi | Mô tả |
|---|---|
| Thêm trường ảnh | `CompetitionSummary`, `CompetitionDetail`, các thẻ cuộc thi của Staff: `hasCover` (bool). `Winner` / `AwardResult` / tranh trong `awards`: `submissionId`, `hasThumbnail`. Danh sách bài (My entries, Grading list, Exhibition entries, Award candidates / issued): `submissionId` (nếu chưa có) + `hasThumbnail`. |
| `GET /api/submissions/{id}/image?thumb=true` | Trả ảnh thu nhỏ 400 px (mặc định vẫn ảnh gốc). Cùng quy tắc quyền như hiện tại. |
| `GET /api/gallery?category=&page=&pageSize=` | Kho tranh công khai, dùng `IArtworkCatalogService` (đã có trên develop): `{ items:[{submissionId,title,studentName,competitionId,competitionTitle,awardName,hasThumbnail}], page, pageSize, totalCount, totalPages }`. `category` theo danh mục của web (vd. `subject/Animals`); không có = tất cả. |
| `GET /api/gallery/categories` | `[{group, key, label}]` để app dựng bộ lọc. |
| `GET /api/gallery/artworks/{id}` | Một tranh công khai (404 nếu không công khai). |
| `GET /api/student/dashboard` | `{ openToEnter:[CompetitionSummary], editable:[SubmissionListItem], newResults:[SubmissionListItem], pendingProposals:int, unpaidSales:int }`. |
| `GET /api/staff/dashboard` | `{ toGrade:[{competitionId,title,endDate,total,evaluated}], toPublish:[{competitionId,title,emptySlots}], pendingProposals:int, unpaidSales:int }`. |
| `GET /api/auth/me` | Thêm `email`, `code` (mã HV / NV), `className` hoặc `position`. |

Mọi endpoint mới có unit test như các API hiện có (`tests/.../Api/*`).

## 6. Kế hoạch thực hiện

| Giai đoạn | Agent | Việc | File được sửa |
|---|---|---|---|
| 1 (song song) | **A – API** | Toàn bộ mục 5 + test. | `src/.../Controllers/Api/**`, `tests/.../Api/**` |
| 1 (song song) | **B – Nền giao diện** | Theme + font (mục 3); bộ widget dùng chung: ảnh công khai / ảnh có token (có cache, ô chờ, lỗi), khung tranh + nhãn bảo tàng, thẻ cuộc thi có ảnh bìa, chip trạng thái, tiêu đề mục, ô trống, thanh tiến độ, trình xem tranh phóng to; khung điều hướng 3 vai trò (thanh dưới mục 4) và router; đổi chữ sang tiếng Anh ở phần khung; model / repository cho các API mới theo hợp đồng mục 5. | `mobile/lib/app/**`, `mobile/lib/core/**`, `mobile/lib/shared/**` (mới), `mobile/pubspec.yaml`, `mobile/assets/**`, các file model / repository mới |
| 2 (song song, sau giai đoạn 1) | **C – Khách** | M-G1 → M-G6. | `mobile/lib/features/home/**`, `features/competitions/**`, `features/gallery/**` (mới), `features/auth/login_screen.dart` |
| 2 | **D – Học viên** | M-S1 → M-S7. | `features/student/**`, `features/submissions/**`, `features/exhibitions/student_*`, `features/account/**` (mới) |
| 2 | **E – Staff** | M-T1 → M-T6. | `features/staff/**`, `features/grading/**`, `features/awarding/**`, `features/exhibitions/staff_*` |

Quy tắc chung cho agent: không sửa file của nhóm khác; không đổi CSDL; chữ tiếng Anh; giữ nguyên luật nghiệp vụ và lời gọi API hiện có; `flutter analyze` không lỗi, `flutter test` và `dotnet test` đều pass; commit trên nhánh riêng, không push.

## 7. Tiêu chí nghiệm thu

1. Mọi màn hình mục 4 dùng theme mới, không còn màu / chữ mặc định, toàn bộ tiếng Anh.
2. Ảnh hiện ở: trang chủ, danh sách và chi tiết cuộc thi, gallery, bài của tôi, chấm bài, trao giải, triển lãm.
3. Học viên làm trọn: xem cuộc thi → nộp bài có ảnh → sửa → xem kết quả. Staff làm trọn: chấm → trao giải → công bố. Chạy thật với API ở `http://localhost:5000` và tài khoản seed.
4. `flutter analyze` 0 lỗi; `flutter test` pass; `dotnet test` pass.
5. Hiển thị tốt ở 360×640 và 412×915 (không tràn chữ, không cuộn ngang).

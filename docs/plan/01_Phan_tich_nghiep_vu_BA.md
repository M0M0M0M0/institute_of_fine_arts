# PHẦN 1 – PHÂN TÍCH NGHIỆP VỤ (Business Analysis)

**Dự án:** Institute of Fine Arts – Website quản lý cuộc thi vẽ tranh, bài dự thi, chấm bài, trao giải, triển lãm & bán tranh
**Loại dự án:** eProject – Aptech Semester 3
**Nhóm:** 4 thành viên (1 Lead Backend, 1 Frontend, 2 Tester/Tài liệu/Nhập liệu)
**Thời gian:** 4 tuần (28 ngày)
**Công nghệ (đã chốt theo yêu cầu giảng viên):** .NET 10 – ASP.NET Core MVC (C#), Entity Framework Core, Razor + Bootstrap 5, SQL Server. Upload/Download ảnh bằng **File I/O (`System.IO`)** vào thư mục `uploads/`.
**Tài liệu nguồn:** `Institute of Fine Arts.doc` (bản gốc tiếng Anh), `Hoc vien My thuat - De bai.docx` (bản dịch), `Dac ta du an - Hoc vien My thuat.docx` (đặc tả eProject).

> Tài liệu này là đầu vào cho: Phần 2 (Thiết kế CSDL/kiến trúc), Phần 3 (Kế hoạch & phân công), Phần 4 (Kiểm thử). Mọi tên Role, Entity, mã Module (M01–M11), mã chức năng (F-Mxx-yy), quy tắc (BR-xx), màn hình (SCR-xx), báo cáo (RPT-xx) trong tài liệu này là **chuẩn chung** cho cả dự án.

---

## Mục lục

1. [Tóm tắt bài toán, mục tiêu & phạm vi](#1-tóm-tắt-bài-toán-mục-tiêu--phạm-vi)
2. [Stakeholders, Actors & Ma trận phân quyền](#2-stakeholders-actors--ma-trận-phân-quyền)
3. [Danh sách chức năng (M01–M11, M12+)](#3-danh-sách-chức-năng)
4. [Quy tắc nghiệp vụ (BR-xx) & vòng đời trạng thái](#4-quy-tắc-nghiệp-vụ-business-rules)
5. [Use case chính](#5-use-case-chính)
6. [User stories & Acceptance criteria](#6-user-stories--acceptance-criteria)
7. [Danh sách báo cáo](#7-danh-sách-báo-cáo)
8. [Yêu cầu phi chức năng](#8-yêu-cầu-phi-chức-năng)
9. [Danh sách màn hình](#9-danh-sách-màn-hình-screen-list)
10. [Điểm mơ hồ, giả định & câu hỏi DOUBT](#10-điểm-mơ-hồ-giả-định--câu-hỏi-doubt)
11. [Glossary Anh – Việt](#11-glossary-anh--việt)
12. [Phụ lục: Truy vết yêu cầu đề bài → chức năng](#12-phụ-lục-ma-trận-truy-vết-yêu-cầu-traceability)

---

## 1. Tóm tắt bài toán, mục tiêu & phạm vi

### 1.1 Tóm tắt bài toán

"Institute of Fine Arts" là học viện đào tạo hội họa, hoạt hình. Trong khóa học, học viện tổ chức **các cuộc thi vẽ tranh**; **mọi học viên** phải tham gia, sáng tác tranh trên máy tính và nộp dưới dạng **file JPEG**, kèm phần chữ (thơ, trích dẫn, câu chuyện, mô tả). Quá hạn nộp → bài không được chấp nhận, học viên **bị loại**. Sau hạn nộp, giáo viên (staff) **đánh giá/xếp loại** bài theo sự sáng tạo + phần chữ, chọn bài tốt nhất để **trao giải**. Thỉnh thoảng học viện đưa các bức tranh đẹp nhất đi **triển lãm**, tranh được **bán theo giá do học viên đặt**, tiền bán được **trả lại cho học viên**.

**Hiện trạng:** quản lý bằng giấy bút → tra cứu chậm; mỗi khi học viên muốn đổi tranh, nhân viên phải tìm hồ sơ rồi sửa thủ công.

**Giải pháp:** Website của học viện cho phép công bố cuộc thi, học viên tự upload/download/sửa/xóa tranh, nhân viên chấm bài online, quản lý giải thưởng, triển lãm, bán tranh và báo cáo.

### 1.2 Mục tiêu nghiệp vụ

| Mã | Mục tiêu | Chỉ số đo (KPI gợi ý) |
|---|---|---|
| G1 | Số hóa toàn bộ quy trình cuộc thi (công bố → nộp bài → chấm → trao giải) | 100% bài nộp qua website, 0 hồ sơ giấy |
| G2 | Học viên tự phục vụ: nộp, sửa, xóa, tải bài trước hạn | Nhân viên không phải sửa hồ sơ thay học viên |
| G3 | Tự động chặn bài nộp trễ, đảm bảo công bằng | 0 bài được nhận sau hạn |
| G4 | Giảm thời gian chấm & tra cứu | Tìm 1 bài / 1 học viên < 5 giây (tìm kiếm, lọc) |
| G5 | Minh bạch kết quả: xếp loại, nhận xét, giải thưởng hiển thị cho học viên | Học viên xem được kết quả ngay khi công bố |
| G6 | Quản lý tài chính: giá trị giải, giá đề xuất, giá bán, trạng thái trả tiền cho học viên | Không có tranh đã bán mà chưa ghi nhận trả tiền quá hạn |
| G7 | Báo cáo phục vụ quản lý (bài nộp theo kỳ/xếp loại, giải thưởng, doanh thu triển lãm) | Có ≥ 5 báo cáo, lọc theo tham số |

### 1.3 Phạm vi

**In-scope (trong phạm vi):**

| # | Hạng mục |
|---|---|
| 1 | Đăng nhập/đăng xuất, phân quyền 4 role ADMIN, MANAGER, STAFF, STUDENT + khách GUEST |
| 2 | Trang chủ công khai: cuộc thi sắp/đang diễn ra, tổng bài nộp cuộc thi đang diễn ra, học viên đạt giải cuộc thi trước |
| 3 | Admin quản lý nhân viên (STAFF, MANAGER, ADMIN) và học viên: CRUD + tìm kiếm |
| 4 | Staff quản lý cuộc thi + giải thưởng định nghĩa (CompetitionAward) |
| 5 | Student nộp/sửa/xóa/tìm/tải bài dự thi (Submission) bằng File I/O, chỉ JPEG |
| 6 | Staff chấm bài (Evaluation): xếp loại BEST/BETTER/GOOD/MODERATE/NORMAL/DISQUALIFIED + nhận xét |
| 7 | Staff trao giải (AwardIssued) |
| 8 | Staff/Manager quản lý triển lãm (Exhibition); quản lý tranh gửi triển lãm (ExhibitionEntry), bán tranh, khách mua (Customer), trả tiền cho học viên |
| 9 | Manager xem toàn bộ dữ liệu (dashboard, read-only) + quản lý triển lãm |
| 10 | Báo cáo & thống kê (lọc theo cuộc thi, khoảng thời gian, xếp loại...) |
| 11 | Giao diện responsive (Bootstrap 5), **tiếng Anh** (ngôn ngữ chính – sản phẩm được team nước ngoài chấm); thêm giao diện tiếng Việt là tính năng phụ (Could) |

**Out-of-scope (ngoài phạm vi):**

| # | Hạng mục | Lý do |
|---|---|---|
| 1 | Thanh toán online (cổng VNPay/PayPal…) khi bán tranh | Đề bài chỉ yêu cầu *ghi nhận* giá bán, khách mua, đã trả tiền hay chưa |
| 2 | Quản lý lớp học, thời khóa biểu, điểm danh, học phí | Đề bài chỉ nhắc "lớp/môn phụ trách" như thông tin mô tả |
| 3 | Học viên tự đăng ký tài khoản công khai | Tài khoản do Admin cấp (giả định A-01); có thể là M12 Could |
| 4 | Ứng dụng di động native, API công khai cho bên thứ ba | Chỉ web responsive |
| 5 | Lưu ảnh trên cloud (S3…) | Đề bài bắt buộc File I/O → lưu ổ đĩa cục bộ `uploads/` |
| 6 | Chỉnh sửa ảnh online (crop, filter) | Học viên vẽ trên máy riêng rồi nộp JPEG |
| 7 | Bán tranh online cho khách (giỏ hàng) | Việc bán diễn ra tại triển lãm, hệ thống chỉ ghi nhận |
| 8 | Triển lãm ngoài nước | Đề bài: "triển lãm trong nước" |

---

## 2. Stakeholders, Actors & Ma trận phân quyền

### 2.1 Stakeholders

| Stakeholder | Vai trò | Quan tâm chính |
|---|---|---|
| Ban lãnh đạo học viện (2 nhà sáng lập) | Khách hàng / Sponsor | Tự động hóa, báo cáo, uy tín học viện |
| Quản lý (Manager) | Người dùng | Giám sát toàn bộ, quản lý triển lãm |
| Quản trị viên (Admin) | Người dùng | Quản lý tài khoản nhân viên, học viên |
| Giáo viên / Nhân viên (Staff) | Người dùng | Tạo cuộc thi, chấm bài, trao giải, đưa tranh đi triển lãm, ghi nhận bán |
| Học viên (Student) | Người dùng | Nộp bài dễ dàng, xem kết quả, nhận tiền bán tranh |
| Khách (Guest) | Người dùng ẩn danh | Xem thông tin cuộc thi, học viên đạt giải |
| Khách mua tranh (Customer) | Đối tượng dữ liệu (không đăng nhập) | Thông tin được lưu khi mua tranh |
| Nhóm eProjects (Aptech) | Người chấm/hướng dẫn | Tài liệu đầy đủ, đúng hạn, trả lời DOUBT |

### 2.2 Actors

| Mã Role | Tên | Mô tả | Cách có tài khoản |
|---|---|---|---|
| GUEST | Khách | Chưa đăng nhập, chỉ xem trang chủ & trang công khai | – |
| STUDENT | Học viên | Xem cuộc thi, nộp/sửa/xóa bài, xem kết quả, giải, triển lãm của mình | Admin tạo (M04) |
| STAFF | Nhân viên / Giáo viên | Xem học viên, quản lý cuộc thi, chấm bài, trao giải, triển lãm, bán tranh | Admin tạo (M03) |
| MANAGER | Quản lý | Xem tất cả (read-only), quản lý triển lãm, xem báo cáo | Admin tạo (M03) |
| ADMIN | Quản trị viên | Quản lý tài khoản & hồ sơ nhân viên, học viên | Tạo sẵn bằng script seed; Admin có thể tạo thêm Admin |
| SYSTEM | Hệ thống (actor phụ) | Tự tính trạng thái cuộc thi theo ngày, chặn nộp trễ, ghi log | – |

> Mỗi Account có **đúng 1 role** (giả định A-02). Account của ADMIN/MANAGER/STAFF có StaffProfile; Account của STUDENT có StudentProfile.

### 2.3 Ma trận phân quyền chi tiết (Role × Chức năng)

Ký hiệu: **C** = Create, **R** = Read/xem, **U** = Update, **D** = Delete, **S** = Search/lọc, **–** = không có quyền, **(own)** = chỉ dữ liệu của chính mình, **(pub)** = chỉ dữ liệu đã công bố.

| Đối tượng / Chức năng | GUEST | STUDENT | STAFF | MANAGER | ADMIN |
|---|---|---|---|---|---|
| Trang chủ công khai (M02) | R | R | R | R | R |
| Đăng nhập, đổi mật khẩu, hồ sơ cá nhân (M01) | Đăng nhập | R/U (own) | R/U (own) | R/U (own) | R/U (own) |
| Account + StaffProfile (nhân viên) (M03) | – | – | – | R, S | C, R, U, D, S |
| Account + StudentProfile (học viên) (M04) | – | R/U hạn chế (own) | R, S | R, S | C, R, U, D, S |
| Reset mật khẩu / khóa tài khoản | – | – | – | – | U |
| Competition (M05) | R, S (pub, tóm tắt) | R, S | C, R, U, D, S | R, S | R, S |
| CompetitionAward – giải định nghĩa (M05) | R (pub) | R | C, R, U, D | R | R |
| Submission – bài dự thi (M06) | R (ảnh + tên tranh của bài đạt giải/BEST đã công bố), không có phần chữ và nhận xét. | C, R, U, D, S (own); R (bài BEST đã công bố của mọi học viên, kèm nhận xét) | R, S, tải ảnh | R, S, tải ảnh | – |
| Evaluation – chấm bài (M07) | – | R (own, sau khi công bố) | C, R, U, D, S | R, S | – |
| AwardIssued – giải đã trao (M08) | R (pub: tên HV, giải, cuộc thi) | R, S (pub) | C, R, U, D, S | R, S | R |
| Exhibition – triển lãm (M09) | R (sắp tới – Should) | R, S | C, R, U, D, S | C, R, U, D, S | R |
| ExhibitionEntry – tranh gửi triển lãm (M09) | – | C (đề xuất), R, S, D (khi PENDING) (own) | C, R, U, D, S | R, S | – |
| Customer – khách mua (M09) | – | – | C, R, U, D, S | R, S | – |
| Báo cáo & thống kê (M10) | – | – | R (RPT-01..06) | R (tất cả) | R (RPT-07) |
| Manager dashboard (M11) | – | – | – | R | – |

> **Nguyên tắc:** Quyền được kiểm tra ở **cả hai lớp**: URL (`[Authorize(Roles = ...)]` theo từng Area `/admin/**`, `/staff/**`, `/manager/**`, `/student/**`) và tầng service (kiểm tra quyền sở hữu – ownership – với dữ liệu "own").

---

## 3. Danh sách chức năng

Độ ưu tiên MoSCoW: **Must** = đề bài yêu cầu trực tiếp; **Should** = bổ sung hợp lý, nên có; **Could** = có thì tốt, làm khi còn thời gian; **Won't** = không làm lần này.

### M01 – Xác thực & tài khoản

| ID | Chức năng | Mô tả | Actor | MoSCoW |
|---|---|---|---|---|
| F-M01-01 | Đăng nhập | Đăng nhập bằng username + mật khẩu; chuyển hướng về dashboard theo role | Tất cả | Must |
| F-M01-02 | Đăng xuất | Hủy session, quay về trang chủ | Tất cả (đã đăng nhập) | Must |
| F-M01-03 | Phân quyền theo role | Chặn truy cập URL không đúng role (403), menu hiển thị theo role | SYSTEM | Must |
| F-M01-04 | Đổi mật khẩu | Nhập mật khẩu cũ, mới, xác nhận | Tất cả (đã đăng nhập) | Should |
| F-M01-05 | Xem/cập nhật hồ sơ cá nhân | Xem thông tin của mình; sửa SĐT, địa chỉ, ảnh đại diện (không sửa role, mã HV) | Tất cả (đã đăng nhập) | Should |
| F-M01-06 | Chặn tài khoản bị khóa | Account `active = false` không đăng nhập được, thông báo rõ | SYSTEM | Must |
| F-M01-07 | Trang lỗi thân thiện | 403/404/500, quá dung lượng upload | SYSTEM | Should |

### M02 – Trang chủ công khai

| ID | Chức năng | Mô tả | Actor | MoSCoW |
|---|---|---|---|---|
| F-M02-01 | Cuộc thi sắp diễn ra | Danh sách Competition trạng thái UPCOMING (tên, chủ đề, ngày bắt đầu/kết thúc) | GUEST + tất cả | Must |
| F-M02-02 | Cuộc thi đang diễn ra | Danh sách Competition ONGOING + số ngày còn lại | GUEST + tất cả | Must |
| F-M02-03 | Tổng số bài nộp của cuộc thi đang diễn ra | Đếm Submission theo từng cuộc thi ONGOING | GUEST + tất cả | Must |
| F-M02-04 | Học viên đạt giải cuộc thi trước | Danh sách AwardIssued của cuộc thi GRADED gần nhất (tên HV, giải, tên tranh) | GUEST + tất cả | Must |
| F-M02-05 | Xem chi tiết cuộc thi (công khai) | Mô tả, thể lệ, giải thưởng; nút "Log in to submit" | GUEST + tất cả | Should |
| F-M02-06 | Triển lãm sắp tới | Danh sách Exhibition UPCOMING | GUEST + tất cả | Should |
| F-M02-07 | Gallery tranh đạt giải | Ảnh thumbnail các tranh đạt giải (đã công bố) | GUEST + tất cả | Could |
| F-M02-08 | Giới thiệu học viện / liên hệ | Trang tĩnh | GUEST | Could |

### M03 – Admin: quản lý nhân viên

| ID | Chức năng | Mô tả | Actor | MoSCoW |
|---|---|---|---|---|
| F-M03-01 | Thêm nhân viên | Tạo Account (username, mật khẩu, email, role STAFF/MANAGER/ADMIN) + StaffProfile: thông tin cá nhân (họ tên, giới tính, ngày sinh, SĐT, địa chỉ), công việc (mã NV, ngày vào làm, lớp phụ trách, môn phụ trách, chức vụ), ghi chú | ADMIN | Must |
| F-M03-02 | Sửa nhân viên | Sửa mọi thông tin (trừ username) | ADMIN | Must |
| F-M03-03 | Xóa nhân viên | Xóa cứng nếu chưa có dữ liệu liên quan; nếu đã có (cuộc thi, chấm bài…) → vô hiệu hóa (BR-04) | ADMIN | Must |
| F-M03-04 | Tìm kiếm & danh sách nhân viên | Lọc theo tên, mã NV, role, môn, lớp, trạng thái; phân trang | ADMIN | Must |
| F-M03-05 | Xem chi tiết nhân viên | Toàn bộ thông tin đăng nhập (không hiện mật khẩu), cá nhân, công việc, ghi chú | ADMIN | Must |
| F-M03-06 | Reset mật khẩu nhân viên | Đặt mật khẩu tạm | ADMIN | Should |
| F-M03-07 | Khóa / mở khóa tài khoản | Đổi `active` | ADMIN | Should |

### M04 – Admin: quản lý học viên

| ID | Chức năng | Mô tả | Actor | MoSCoW |
|---|---|---|---|---|
| F-M04-01 | Thêm học viên | Account (role STUDENT) + StudentProfile: cá nhân (họ tên, giới tính, ngày sinh, SĐT, email, địa chỉ), nhập học (mã HV, ngày nhập học, khóa học, lớp, trạng thái học) | ADMIN | Must |
| F-M04-02 | Sửa học viên | | ADMIN | Must |
| F-M04-03 | Xóa học viên | Xóa cứng nếu chưa có bài nộp; ngược lại vô hiệu hóa (BR-04) | ADMIN | Must |
| F-M04-04 | Tìm kiếm & danh sách học viên | Lọc theo tên, mã HV, lớp, khóa, ngày nhập học, trạng thái | ADMIN | Must |
| F-M04-05 | Xem chi tiết học viên | | ADMIN | Must |
| F-M04-06 | Staff xem thông tin học viên | Danh sách + chi tiết (cá nhân + nhập học), read-only | STAFF | Must |
| F-M04-07 | Reset mật khẩu / khóa học viên | | ADMIN | Should |
| F-M04-08 | Import học viên từ file CSV | Nhập hàng loạt | ADMIN | Could |

### M05 – Cuộc thi & giải thưởng

| ID | Chức năng | Mô tả | Actor | MoSCoW |
|---|---|---|---|---|
| F-M05-01 | Tạo cuộc thi | Tên, chủ đề, mô tả, ngày bắt đầu, ngày kết thúc (hạn nộp), thể lệ/điều kiện, ảnh banner (tùy chọn), người tạo | STAFF | Must |
| F-M05-02 | Sửa cuộc thi | Theo BR-10 (giới hạn sửa khi đã có bài nộp) | STAFF | Must |
| F-M05-03 | Xóa cuộc thi | Chỉ khi chưa có Submission (BR-11) | STAFF | Must |
| F-M05-04 | Tìm kiếm & danh sách cuộc thi | Lọc theo tên, trạng thái, khoảng ngày | STAFF, MANAGER, ADMIN, STUDENT | Must |
| F-M05-05 | Xem chi tiết cuộc thi | Thông tin, thể lệ, giải, số bài nộp, trạng thái | Tất cả đã đăng nhập | Must |
| F-M05-06 | Quản lý giải thưởng định nghĩa (CompetitionAward) | Thêm/sửa/xóa giải cho cuộc thi: tên giải (Nhất, Nhì, Ba, Khuyến khích…), thứ hạng, giá trị (tiền), số lượng, mô tả phần thưởng | STAFF | Must |
| F-M05-07 | Tự động xác định trạng thái | UPCOMING/ONGOING/CLOSED theo ngày hệ thống; GRADED khi công bố kết quả (BR-07) | SYSTEM | Must |
| F-M05-08 | Công bố kết quả cuộc thi | Chuyển CLOSED → GRADED khi mọi bài đã chấm (BR-08); kết quả hiển thị cho học viên & trang chủ | STAFF | Must |
| F-M05-09 | Student xem cuộc thi sắp tới / đang diễn ra | Kèm giải thưởng, chi tiết, thể lệ | STUDENT | Must |

### M06 – Bài dự thi (Student)

| ID | Chức năng | Mô tả | Actor | MoSCoW |
|---|---|---|---|---|
| F-M06-01 | Nộp bài (upload) | Chọn cuộc thi ONGOING, upload file JPEG (File I/O), nhập tiêu đề, mô tả (vì sao vẽ, vì sao xứng đáng nhất), thơ/trích dẫn/câu chuyện | STUDENT | Must |
| F-M06-02 | Sửa nội dung bài | Sửa tiêu đề, mô tả, thơ/trích dẫn/câu chuyện khi chưa quá hạn | STUDENT | Must |
| F-M06-03 | Thay ảnh bài | Upload ảnh JPEG mới, xóa file cũ khi chưa quá hạn | STUDENT | Must |
| F-M06-04 | Xóa bài | Xóa Submission + file ảnh khi chưa quá hạn | STUDENT | Must |
| F-M06-05 | Danh sách & tìm kiếm bài của tôi | Lọc theo cuộc thi, tiêu đề, khoảng ngày nộp, xếp loại | STUDENT | Must |
| F-M06-06 | Xem chi tiết & tải ảnh (download) | Xem ảnh full, tải file gốc qua File I/O | STUDENT (own), STAFF, MANAGER | Must |
| F-M06-07 | Xem xếp loại & nhận xét các bài của tôi | Toàn bộ bài đã tham gia: xếp loại, điểm mạnh, điểm yếu, cần cải thiện | STUDENT | Must |
| F-M06-08 | Xem tất cả bài BEST kèm nhận xét | Gallery các bài BEST (của mọi HV, cuộc thi đã GRADED) + nhận xét của staff | STUDENT | Must |
| F-M06-09 | Xem giải thưởng đã trao theo cuộc thi | Danh sách AwardIssued theo cuộc thi | STUDENT | Must |
| F-M06-10 | Cảnh báo hạn nộp | Hiển thị đếm ngược, thông báo "Deadline passed" | STUDENT | Should |

### M07 – Chấm bài / Đánh giá (Staff)

| ID | Chức năng | Mô tả | Actor | MoSCoW |
|---|---|---|---|---|
| F-M07-01 | Danh sách bài nộp theo cuộc thi | Lọc theo cuộc thi, trạng thái chấm (chưa/đã), xếp loại, tên/mã HV, ngày nộp | STAFF | Must |
| F-M07-02 | Xem chi tiết bài | Ảnh, tiêu đề, mô tả, thơ/trích dẫn, ngày nộp, ngày sửa cuối, cuộc thi, học viên | STAFF | Must |
| F-M07-03 | Chấm bài (tạo Evaluation) | Chọn xếp loại BEST/BETTER/GOOD/MODERATE/NORMAL/DISQUALIFIED; nhập điểm mạnh (positive points), điểm yếu (negative points), điểm cần cải thiện (improvement areas) | STAFF | Must |
| F-M07-04 | Sửa đánh giá | Khi cuộc thi chưa GRADED (BR-21) | STAFF | Must |
| F-M07-05 | Xóa đánh giá | Khi chưa GRADED và bài chưa được trao giải/gửi triển lãm | STAFF | Must |
| F-M07-06 | Tìm kiếm bài theo xếp loại | Tìm bài BEST/BETTER… để chọn gửi triển lãm | STAFF | Must |
| F-M07-07 | Hồ sơ học tập học viên | Lịch sử toàn bộ bài + đánh giá + giải của 1 học viên | STAFF | Must |
| F-M07-08 | Gợi ý DISQUALIFIED tự động | Bài có `submittedAt` > hạn (nhập bù/ngoại lệ) được hệ thống đề xuất DISQUALIFIED | SYSTEM | Should |
| F-M07-09 | Tiến độ chấm | "18/25 graded" cho mỗi cuộc thi | STAFF | Should |

### M08 – Trao giải

| ID | Chức năng | Mô tả | Actor | MoSCoW |
|---|---|---|---|---|
| F-M08-01 | Trao giải (tạo AwardIssued) | Chọn cuộc thi CLOSED/GRADED → chọn CompetitionAward → chọn bài đủ điều kiện (BR-30) → ngày trao, ghi chú/nhận xét cuộc thi | STAFF | Must |
| F-M08-02 | Sửa giải đã trao | Đổi bài/giải/ghi chú | STAFF | Must |
| F-M08-03 | Thu hồi (xóa) giải đã trao | | STAFF | Must |
| F-M08-04 | Tìm kiếm danh sách giải đã trao | Lọc theo cuộc thi, học viên, giải, khoảng ngày | STAFF, MANAGER, STUDENT (pub) | Must |
| F-M08-05 | Đánh dấu đã phát thưởng | Ghi nhận phần thưởng (tiền/hiện vật) đã giao cho HV | STAFF | Should |

### M09 – Triển lãm & bán tranh

| ID | Chức năng | Mô tả | Actor | MoSCoW |
|---|---|---|---|---|
| F-M09-01 | Quản lý triển lãm (CRUD) | Tên, địa điểm, thành phố (trong nước), ngày bắt đầu/kết thúc, ban tổ chức, mô tả | STAFF, MANAGER | Must |
| F-M09-02 | Tìm kiếm triển lãm | Theo tên, thành phố, khoảng ngày, trạng thái | STAFF, MANAGER, STUDENT | Must |
| F-M09-03 | Student xem triển lãm sắp tới | Chi tiết triển lãm | STUDENT | Must |
| F-M09-04 | Student đề xuất gửi tranh đến triển lãm | Chọn bài đủ điều kiện (BR-40) + nhập giá đề xuất → ExhibitionEntry PENDING | STUDENT | Must |
| F-M09-05 | Staff gửi/duyệt tranh đến triển lãm | Duyệt đề xuất (ACCEPTED/REJECTED) hoặc tìm bài BEST/BETTER rồi tạo entry trực tiếp với giá HV đề xuất | STAFF | Must |
| F-M09-06 | Cập nhật trạng thái bán | Đánh dấu SOLD: giá bán, ngày bán, khách mua; hoặc UNSOLD khi triển lãm kết thúc | STAFF | Must |
| F-M09-07 | Quản lý khách mua (Customer) | Họ tên, SĐT, email, địa chỉ | STAFF | Must |
| F-M09-08 | Ghi nhận trả tiền cho học viên | `paidToStudent`, số tiền trả, ngày trả | STAFF | Must |
| F-M09-09 | Danh sách & tìm kiếm tranh triển lãm (CRUD entry) | Lọc theo triển lãm, trạng thái, đã trả tiền, học viên | STAFF | Must |
| F-M09-10 | Student xem tranh đã gửi triển lãm của mình | Giá đề xuất, trạng thái, đã bán?, giá khách trả, đã nhận tiền? | STUDENT | Must |
| F-M09-11 | Manager xem danh sách tranh được staff đưa đi triển lãm | Kèm chi tiết | MANAGER | Must |
| F-M09-12 | Student rút đề xuất | Xóa entry khi còn PENDING | STUDENT | Should |

### M10 – Báo cáo & thống kê

| ID | Chức năng | Mô tả | Actor | MoSCoW |
|---|---|---|---|---|
| F-M10-01 | RPT-01 Bài nộp theo khoảng thời gian, cuộc thi, xếp loại | Đúng ví dụ đề bài | STAFF, MANAGER | Must |
| F-M10-02 | RPT-02 Thống kê tham gia & xếp loại theo cuộc thi | | STAFF, MANAGER | Must |
| F-M10-03 | RPT-03 Giải thưởng đã trao | Kèm tổng giá trị giải (Financial) | STAFF, MANAGER | Must |
| F-M10-04 | RPT-04 Tranh triển lãm & doanh thu bán tranh | Giá đề xuất, giá bán (Financial) | STAFF, MANAGER | Must |
| F-M10-05 | RPT-05 Công nợ trả tiền cho học viên | Tranh đã bán chưa trả tiền HV | STAFF, MANAGER | Should |
| F-M10-06 | RPT-06 Hồ sơ thành tích học viên | | STAFF, MANAGER | Should |
| F-M10-07 | RPT-07 Thống kê tài khoản | Số NV/HV theo role, trạng thái, lớp | ADMIN, MANAGER | Could |
| F-M10-08 | In báo cáo | CSS print-friendly (nút In trình duyệt) | STAFF, MANAGER | Should |

### M11 – Manager dashboard (xem)

| ID | Chức năng | Mô tả | Actor | MoSCoW |
|---|---|---|---|---|
| F-M11-01 | Dashboard tổng quan | Số HV, NV, cuộc thi theo trạng thái, bài nộp, giải đã trao, tranh đã bán | MANAGER | Must |
| F-M11-02 | Xem học viên & nhân viên | Danh sách + chi tiết, tìm kiếm (read-only) | MANAGER | Must |
| F-M11-03 | Xem các cuộc thi đã tổ chức | Read-only | MANAGER | Must |
| F-M11-04 | Xem giải đã trao | Read-only | MANAGER | Must |
| F-M11-05 | Xem tất cả bài nộp theo cuộc thi | Read-only, xem ảnh, tải ảnh | MANAGER | Must |
| F-M11-06 | Xem xếp loại/nhận xét của 1 bức tranh | Read-only | MANAGER | Must |
| F-M11-07 | Xem tranh đã đưa đi triển lãm | (= F-M09-11) | MANAGER | Must |
| F-M11-08 | Quản lý triển lãm sắp tới | (= F-M09-01) | MANAGER | Must |

### M12+ – Chức năng bổ sung (value-add, tùy chọn)

| ID | Module | Chức năng | Mô tả | MoSCoW |
|---|---|---|---|---|
| F-M12-01 | M12 Thông báo | Email khi có cuộc thi mới / sắp hết hạn / có kết quả / tranh đã bán | MailKit (SMTP Gmail test) | Could |
| F-M12-02 | M12 Thông báo | Thông báo trong hệ thống (chuông) | Bảng Notification | Could |
| F-M13-01 | M13 Ảnh | Tạo thumbnail khi upload (SkiaSharp, File I/O) | Tăng tốc gallery | Could |
| F-M13-02 | M13 Ảnh | Watermark tên học viện khi hiển thị công khai | Chống sao chép | Could |
| F-M14-01 | M14 Xuất báo cáo | Xuất PDF (QuestPDF) / Excel (ClosedXML) | | Could |
| F-M15-01 | M15 Biểu đồ | Dashboard Chart.js: bài nộp theo cuộc thi, tỉ lệ xếp loại, doanh thu | | Could |
| F-M16-01 | M16 Quên mật khẩu | Gửi link/token reset qua email | | Could |
| F-M17-01 | M17 Audit log | Nhật ký: ai, làm gì, lúc nào (đăng nhập, chấm bài, xóa, bán tranh) | | Could |
| F-M18-01 | M18 Phân trang & lọc nâng cao | Phân trang, sắp xếp mọi danh sách (`Skip/Take` trong EF Core) | Nên làm sớm vì rẻ | Could (khuyến nghị làm) |
| F-M19-01 | M19 Đa ngôn ngữ | Thêm giao diện tiếng Việt bên cạnh tiếng Anh mặc định, nút chuyển ngôn ngữ (ASP.NET Core Localization – file `.resx`) | | Could |
| F-M20-01 | M20 Tự đăng ký học viên | Đăng ký + Admin duyệt | | Won't (lần này) |

---

## 4. Quy tắc nghiệp vụ (Business Rules)

### 4.1 Tài khoản & bảo mật

| Mã | Quy tắc |
|---|---|
| BR-01 | `username` và `email` là duy nhất trong toàn hệ thống. `studentCode`, `staffCode` duy nhất. |
| BR-02 | Mật khẩu ≥ 8 ký tự, có chữ và số; lưu dạng băm **BCrypt**, không bao giờ hiển thị/ghi log. |
| BR-03 | Mỗi Account có đúng **1 role**. Không ai tự đổi role của mình. Không xóa/khóa được tài khoản ADMIN cuối cùng và không tự xóa chính mình. |
| BR-04 | Xóa Account/Profile: nếu đã phát sinh dữ liệu nghiệp vụ (Submission, Evaluation, Competition, AwardIssued, ExhibitionEntry) → **không xóa cứng**, chỉ vô hiệu hóa (`active = false`). Nếu chưa phát sinh → được xóa cứng (sau xác nhận). |
| BR-05 | Account bị khóa không đăng nhập được; dữ liệu lịch sử vẫn hiển thị trong báo cáo. |

### 4.2 Cuộc thi (Competition)

| Mã | Quy tắc |
|---|---|
| BR-06 | `startDate` ≤ `endDate`; khi tạo mới `endDate` ≥ ngày hiện tại. Hạn nộp = **23:59:59 ngày `endDate`** theo giờ máy chủ (Asia/Ho_Chi_Minh). |
| BR-07 | Trạng thái cuộc thi được **tính tự động** (không nhập tay), trừ GRADED: **UPCOMING** khi now < startDate (00:00); **ONGOING** khi startDate ≤ now ≤ hạn nộp; **CLOSED** khi now > hạn nộp và chưa công bố; **GRADED** khi staff bấm "Publish results" (`resultPublished = true`). |
| BR-08 | Chỉ công bố kết quả (→ GRADED) khi cuộc thi đã CLOSED và **100% bài nộp đã có Evaluation**. Sau GRADED, kết quả (xếp loại, nhận xét, giải) hiển thị cho học viên và trang chủ. |
| BR-09 | Mỗi cuộc thi phải có ≥ 1 CompetitionAward trước khi chuyển sang ONGOING (hệ thống cảnh báo khi lưu nếu chưa có). Mỗi CompetitionAward có `rank` (1 = cao nhất), `prizeValue` ≥ 0 (USD), `quantity` ≥ 1. |
| BR-10 | Khi cuộc thi đã có Submission: không được sửa `startDate`; được **gia hạn** `endDate` (chỉ kéo dài, không rút ngắn); được sửa mô tả/thể lệ. Khi đã GRADED: chỉ xem. |
| BR-11 | Chỉ xóa cuộc thi khi **chưa có Submission nào** (xóa kèm CompetitionAward). |
| BR-12 | "Mọi học viên phải tham gia": hệ thống không bắt buộc kỹ thuật, nhưng báo cáo RPT-02 liệt kê học viên **chưa nộp bài** để nhắc nhở. Học viên không nộp = không có kết quả (coi như bị loại, không tạo Evaluation). |

### 4.3 Bài dự thi (Submission) & File I/O

| Mã | Quy tắc |
|---|---|
| BR-13 | Chỉ được **nộp/sửa/thay ảnh/xóa** bài khi cuộc thi đang **ONGOING** (now ≤ hạn nộp). Kiểm tra ở **server** tại thời điểm request (không tin client). |
| BR-14 | Sau hạn nộp hệ thống **từ chối** mọi upload/sửa/xóa (thông báo "The submission deadline has passed"). Bài nộp trễ không được tạo → học viên đó bị loại. Xếp loại **DISQUALIFIED** được dùng khi: (a) bài có `submittedAt`/`updatedAt` > hạn nộp (trường hợp nhập bù ngoại lệ); (b) bài vi phạm thể lệ (đạo văn, sai chủ đề, nội dung không phù hợp). |
| BR-15 | **Mỗi học viên chỉ có tối đa 1 bài dự thi / 1 cuộc thi** (unique `studentId + competitionId`). Muốn đổi tranh → dùng chức năng sửa/thay ảnh. |
| BR-16 | Chỉ chấp nhận ảnh **JPEG**: đuôi `.jpg`/`.jpeg`, content-type `image/jpeg` **và** kiểm tra magic bytes `FF D8 FF`. |
| BR-17 | Dung lượng ảnh tối đa **5 MB**/file (cấu hình `Upload:MaxFileSizeBytes` + `[RequestSizeLimit]`). Kích thước ảnh tối thiểu khuyến nghị 600 px cạnh ngắn (cảnh báo, không chặn). |
| BR-18 | File lưu bằng File I/O (`System.IO`) tại `uploads/submissions/{competitionId}/{guid}.jpg`; tên file gốc chỉ lưu trong DB (`originalFileName`) – **không dùng tên file người dùng làm đường dẫn** (chống path traversal). Thay ảnh → ghi file mới rồi xóa file cũ; xóa bài → xóa file. |
| BR-19 | Bắt buộc: tiêu đề (≤ 150 ký tự), ảnh, mô tả (≤ 2000 ký tự). Phần chữ đi kèm (thơ/trích dẫn/câu chuyện) bắt buộc ít nhất 1 trường (≤ 4000 ký tự), chọn loại `textType` = POEM / QUOTATION / STORY / OTHER. |
| BR-20 | Học viên chỉ xem/sửa/xóa/tải **bài của mình**. STAFF/MANAGER xem & tải mọi bài. GUEST/STUDENT khác chỉ thấy bài BEST hoặc bài đạt giải của cuộc thi đã GRADED. Download ảnh luôn đi qua controller (kiểm tra quyền), không phơi thư mục `uploads/` trực tiếp. |

### 4.4 Chấm bài (Evaluation)

| Mã | Quy tắc |
|---|---|
| BR-21 | Chỉ chấm được khi cuộc thi **CLOSED** (sau hạn nộp – đúng đề bài "sau hạn nộp mới đánh giá"). Sửa/xóa Evaluation được khi cuộc thi chưa GRADED. |
| BR-22 | Mỗi Submission có **tối đa 1 Evaluation** (1 staff chấm, lưu `evaluatedBy`, `evaluatedAt`; staff khác sửa thì ghi đè người chấm cuối). Bất kỳ STAFF nào cũng được chấm (giả định A-07). |
| BR-23 | Xếp loại (`rating`) ∈ {BEST, BETTER, GOOD, MODERATE, NORMAL, DISQUALIFIED} – bắt buộc. Với DISQUALIFIED bắt buộc nhập lý do (vào `negativePoints`). Với rating khác: `positivePoints` bắt buộc, `negativePoints`, `improvementAreas` khuyến nghị nhập. |
| BR-24 | Học viên chỉ thấy Evaluation của mình **sau khi cuộc thi GRADED**. |

### 4.5 Trao giải (AwardIssued)

| Mã | Quy tắc |
|---|---|
| BR-30 | Chỉ trao giải cho Submission **đã được chấm với rating = BEST** của đúng cuộc thi đó. Bài DISQUALIFIED tuyệt đối không được trao giải. |
| BR-31 | Số AwardIssued của mỗi CompetitionAward ≤ `quantity`. Mỗi học viên nhận **tối đa 1 giải / 1 cuộc thi** (hệ quả của BR-15: unique `submissionId` trong AwardIssued). |
| BR-32 | Trao giải khi cuộc thi CLOSED (sau khi chấm) hoặc GRADED. Giải chỉ hiển thị công khai khi cuộc thi GRADED. |
| BR-33 | Thu hồi giải (xóa AwardIssued) được phép, bắt buộc ghi lý do trong ghi chú (log). Giá trị giải lấy từ `CompetitionAward.prizeValue` (Financial). |

### 4.6 Triển lãm & bán tranh (Exhibition, ExhibitionEntry, Customer)

| Mã | Quy tắc |
|---|---|
| BR-40 | Chỉ Submission **đã chấm với rating BEST hoặc BETTER** (cuộc thi đã GRADED) mới được gửi triển lãm. |
| BR-41 | Giá bán đề xuất (`askingPrice`) **do học viên quyết định** (> 0 USD). Staff có thể tạo entry trực tiếp nhưng giá phải là giá học viên đã báo (nhập kèm ghi chú nguồn). |
| BR-42 | Chỉ gửi tranh tới triển lãm **UPCOMING** (chưa bắt đầu). Exhibition: `startDate` ≤ `endDate`; trạng thái tính tự động UPCOMING / ONGOING / FINISHED. Chỉ xóa triển lãm khi chưa có ExhibitionEntry. |
| BR-43 | Một Submission chỉ có **1 entry đang hiệu lực** (PENDING/ACCEPTED) tại một thời điểm; tranh đã **SOLD** không được gửi triển lãm nào nữa. Tranh UNSOLD/REJECTED được gửi lại triển lãm khác. |
| BR-44 | Đánh dấu SOLD bắt buộc: `soldPrice` > 0, `soldDate` (trong khoảng ngày triển lãm), Customer. `soldPrice` mặc định = `askingPrice`, có thể khác (ghi lại giá thực tế khách trả). |
| BR-45 | **Toàn bộ tiền bán tranh trả cho học viên** (không trích hoa hồng – giả định A-10). Khi trả: `paidToStudent = true`, `paidAmount` (mặc định = `soldPrice`), `paidDate` ≥ `soldDate`. Chỉ ghi nhận trả tiền khi entry SOLD. |
| BR-46 | Học viên sửa giá/rút đề xuất chỉ khi entry còn PENDING. Sau ACCEPTED, mọi thay đổi do STAFF. |
| BR-47 | Tiền tệ **USD**, kiểu `DECIMAL(18,2)` (2 chữ số thập phân), không âm; hiển thị dạng `$1,250.00`. |

### 4.7 Vòng đời trạng thái

**a) Competition**

```
            now >= startDate           now > endDate 23:59:59        Staff "Publish results"
UPCOMING ─────────────────► ONGOING ─────────────────────► CLOSED ────────────────────────► GRADED
 (sửa/xóa tự do)           (HV nộp/sửa/xóa bài)          (Staff chấm, trao giải)       (công khai kết quả, chỉ xem)
```

**b) Submission** (trạng thái hiển thị, suy ra từ dữ liệu)

| Trạng thái | Điều kiện | Học viên được làm |
|---|---|---|
| SUBMITTED | Đã nộp, cuộc thi ONGOING | Sửa, thay ảnh, xóa, tải |
| LOCKED (chờ chấm) | Cuộc thi CLOSED, chưa có Evaluation | Chỉ xem, tải |
| EVALUATED | Có Evaluation (học viên chỉ thấy kết quả khi GRADED) | Xem kết quả |
| AWARDED | Có AwardIssued | Xem giải |
| (DISQUALIFIED) | Evaluation.rating = DISQUALIFIED | Xem lý do |

**c) ExhibitionEntry**

```
 Student đề xuất                Staff duyệt
 ──────────► PENDING ──────────────────────► ACCEPTED ──(bán tại triển lãm)──► SOLD ──(trả tiền)──► paidToStudent = true
               │   └──(HV rút)──► [xóa]        │                                   
               └──(Staff từ chối)──► REJECTED  └──(triển lãm kết thúc, không bán)──► UNSOLD
 Staff tạo trực tiếp ───────────────────────► ACCEPTED
```

Trạng thái thanh toán (`paymentStatus`, suy ra): `N/A` (chưa bán) → `UNPAID` (SOLD, chưa trả) → `PAID`.

**d) Account:** `ACTIVE` ⇄ `INACTIVE` (khóa/vô hiệu hóa).

---

## 5. Use case chính

### UC-01: Đăng nhập (F-M01-01)

| Mục | Nội dung |
|---|---|
| Actor | ADMIN, MANAGER, STAFF, STUDENT |
| Tiền điều kiện | Có Account, `active = true` |
| Luồng chính | 1. Người dùng mở `/login`. 2. Nhập username, mật khẩu. 3. Hệ thống so khớp BCrypt. 4. Tạo session, chuyển về dashboard theo role (`/admin`, `/manager`, `/staff`, `/student`). |
| Luồng phụ / ngoại lệ | 3a. Sai thông tin → "Invalid username or password." (không nói rõ sai trường nào). 3b. Tài khoản bị khóa → "Your account has been locked. Please contact the administrator.". 1a. Đã đăng nhập mà vào `/login` → chuyển về dashboard. |
| Hậu điều kiện | Session có role; menu hiển thị theo role. |

### UC-02: Staff tạo cuộc thi & định nghĩa giải thưởng (F-M05-01, F-M05-06)

| Mục | Nội dung |
|---|---|
| Actor | STAFF |
| Tiền điều kiện | Đã đăng nhập role STAFF |
| Luồng chính | 1. Staff vào "Competitions" → "Create". 2. Nhập tên, chủ đề, mô tả, ngày bắt đầu, ngày kết thúc, thể lệ, (banner). 3. Lưu → hệ thống kiểm tra BR-06, lưu Competition (UPCOMING hoặc ONGOING tùy ngày). 4. Staff vào tab "Awards", thêm từng CompetitionAward (tên, hạng, giá trị, số lượng, mô tả). 5. Cuộc thi xuất hiện trên trang chủ. |
| Luồng phụ / ngoại lệ | 3a. Ngày không hợp lệ → báo lỗi tại trường. 3b. Thiếu trường bắt buộc → báo lỗi. 4a. Chưa có giải nào → cảnh báo BR-09. Sửa/xóa: theo BR-10, BR-11 (có bài nộp → không cho xóa). |
| Hậu điều kiện | Competition + CompetitionAward lưu trong DB; học viên thấy cuộc thi. |

### UC-03: Học viên nộp bài dự thi (F-M06-01) – **use case cốt lõi**

| Mục | Nội dung |
|---|---|
| Actor | STUDENT |
| Tiền điều kiện | Đăng nhập STUDENT; cuộc thi ONGOING; học viên chưa có bài trong cuộc thi này (BR-15) |
| Luồng chính | 1. HV mở chi tiết cuộc thi ONGOING → "Submit". 2. Nhập tiêu đề, mô tả, chọn loại phần chữ và nhập thơ/trích dẫn/câu chuyện. 3. Chọn file JPEG. 4. Gửi. 5. Server kiểm tra: thời hạn (BR-13), định dạng (BR-16), dung lượng (BR-17), dữ liệu bắt buộc (BR-19). 6. Ghi file bằng `Files.copy` vào `uploads/submissions/{competitionId}/{guid}.jpg` (BR-18). 7. Lưu Submission (`submittedAt = now`). 8. Thông báo thành công, chuyển tới "My Submissions". |
| Luồng phụ / ngoại lệ | 5a. Quá hạn → từ chối "The submission deadline has passed" (BR-14). 5b. Không phải JPEG / > 5 MB → báo lỗi, không lưu. 5c. Đã có bài trong cuộc thi → chuyển sang màn hình sửa bài. 6a. Lỗi ghi file (IOException) → rollback, không tạo Submission, thông báo lỗi chung. 7a. Lỗi DB sau khi ghi file → xóa file vừa ghi. |
| Hậu điều kiện | Submission + file ảnh tồn tại; tổng bài nộp trên trang chủ tăng 1. |

### UC-04: Học viên sửa / thay ảnh / xóa bài (F-M06-02..04)

| Mục | Nội dung |
|---|---|
| Actor | STUDENT |
| Tiền điều kiện | Bài thuộc học viên; cuộc thi ONGOING |
| Luồng chính | 1. HV vào "My Submissions" → chọn bài → "Edit". 2. Sửa nội dung và/hoặc chọn ảnh mới. 3. Lưu → kiểm tra BR-13, BR-16..19. 4. Nếu có ảnh mới: ghi file mới, cập nhật đường dẫn, xóa file cũ. 5. Cập nhật `updatedAt`. |
| Luồng phụ / ngoại lệ | Xóa: 1a. HV bấm "Delete" → xác nhận → xóa Submission + file. 3a. Quá hạn → nút Sửa/Xóa bị ẩn; nếu gọi trực tiếp URL → 403/thông báo quá hạn. 3b. Bài không thuộc HV → 403. |
| Hậu điều kiện | Bài cập nhật/bị xóa; file cũ không còn trên đĩa. |

### UC-05: Staff chấm bài (F-M07-01..04)

| Mục | Nội dung |
|---|---|
| Actor | STAFF |
| Tiền điều kiện | Cuộc thi CLOSED (BR-21) |
| Luồng chính | 1. Staff vào "Grade" → chọn cuộc thi → danh sách bài (lọc "Not graded"). 2. Mở bài: xem ảnh full, mô tả, thơ/trích dẫn, ngày nộp. 3. Chọn xếp loại, nhập điểm mạnh, điểm yếu, điểm cần cải thiện. 4. Lưu Evaluation (`evaluatedBy`, `evaluatedAt`). 5. Hệ thống chuyển tới bài chưa chấm kế tiếp, cập nhật tiến độ "x/y". |
| Luồng phụ / ngoại lệ | 1a. Cuộc thi còn ONGOING → nút chấm bị vô hiệu, thông báo "The submission period has not ended yet". 3a. Chọn DISQUALIFIED mà không nhập lý do → lỗi (BR-23). 3b. Bài nộp sau hạn (dữ liệu ngoại lệ) → hệ thống gợi ý DISQUALIFIED. 4a. Cuộc thi đã GRADED → không cho sửa. |
| Hậu điều kiện | Submission có Evaluation; khi tất cả bài đã chấm, nút "Publish results" được bật. |

### UC-06: Staff trao giải & công bố kết quả (F-M08-01, F-M05-08)

| Mục | Nội dung |
|---|---|
| Actor | STAFF |
| Tiền điều kiện | Cuộc thi CLOSED, đã chấm ≥ 1 bài BEST; có CompetitionAward |
| Luồng chính | 1. Staff mở cuộc thi → tab "Issue award". 2. Với mỗi CompetitionAward, chọn bài từ danh sách bài BEST chưa đạt giải. 3. Nhập ngày trao, nhận xét. 4. Lưu AwardIssued (kiểm tra BR-30, BR-31). 5. Khi đã chấm 100% bài, bấm "Publish results" → GRADED (BR-08). |
| Luồng phụ / ngoại lệ | 2a. Không còn suất (đủ `quantity`) → không cho chọn. 2b. Bài không phải BEST → không hiển thị. 5a. Còn bài chưa chấm → từ chối công bố, hiển thị số bài còn lại. |
| Hậu điều kiện | Trang chủ hiển thị học viên đạt giải; học viên thấy xếp loại, nhận xét, giải. |

### UC-07: Gửi tranh đến triển lãm, bán và trả tiền cho học viên (F-M09-04..08)

| Mục | Nội dung |
|---|---|
| Actor | STUDENT, STAFF (chính), MANAGER (tạo triển lãm) |
| Tiền điều kiện | Có Exhibition UPCOMING; bài của HV đạt BEST/BETTER, cuộc thi GRADED (BR-40) |
| Luồng chính | 1. HV xem triển lãm sắp tới → "Submit to exhibition". 2. Chọn bài đủ điều kiện, nhập giá đề xuất → entry PENDING. 3. Staff vào "Exhibition Entries" → duyệt → ACCEPTED. 4. Tại triển lãm, tranh bán được: Staff chọn entry → "Sold": nhập/chọn Customer, giá bán, ngày bán → SOLD. 5. Khi trả tiền cho HV: Staff "Paid to student": số tiền, ngày trả → `paidToStudent = true`. 6. HV xem trạng thái ở "My Exhibition Entries". |
| Luồng phụ / ngoại lệ | 2a. Bài đã có entry hiệu lực/đã bán → từ chối (BR-43). 3a. Staff từ chối → REJECTED + lý do. 3b. Staff tự tìm bài BEST (F-M07-06) và tạo entry ACCEPTED với giá HV đã báo. 4a. Triển lãm kết thúc không bán → UNSOLD. 5a. Chưa SOLD → không cho ghi nhận trả tiền. |
| Hậu điều kiện | Entry có đầy đủ giá đề xuất, giá bán, khách mua, trạng thái trả tiền → dùng cho RPT-04, RPT-05. |

### UC-08: Admin quản lý học viên / nhân viên (F-M03, F-M04)

| Mục | Nội dung |
|---|---|
| Actor | ADMIN |
| Tiền điều kiện | Đăng nhập ADMIN |
| Luồng chính | 1. Admin vào "Students" (hoặc "Staff"). 2. Tìm kiếm theo tên/mã/lớp… 3. "Add new": nhập thông tin đăng nhập + cá nhân + nhập học (hoặc công việc + ghi chú). 4. Lưu: hệ thống kiểm tra BR-01, BR-02, băm mật khẩu BCrypt, tạo Account + Profile trong **1 transaction**. 5. Sửa/xem chi tiết tương tự. |
| Luồng phụ / ngoại lệ | 4a. Trùng username/email/mã → báo lỗi tại trường. Xóa: đã có dữ liệu → hệ thống đề xuất vô hiệu hóa thay vì xóa (BR-04). Xóa chính mình / ADMIN cuối → từ chối (BR-03). |
| Hậu điều kiện | Account + Profile được tạo/cập nhật; người dùng đăng nhập được. |

---

## 6. User stories & Acceptance criteria

Định dạng: *Là <role>, tôi muốn <mục tiêu>, để <lợi ích>.* Tiêu chí chấp nhận viết theo Given/When/Then (dùng trực tiếp cho test case ở Phần 4).

### US-01 (F-M01-01, F-M01-03) – Đăng nhập & phân quyền
*Là người dùng, tôi muốn đăng nhập để dùng đúng chức năng của vai trò mình.*
- **AC1:** Given tài khoản hợp lệ, active, When đăng nhập đúng mật khẩu, Then vào dashboard đúng role.
- **AC2:** Given sai mật khẩu, When đăng nhập, Then hiện thông báo lỗi chung và vẫn ở trang login.
- **AC3:** Given tài khoản bị khóa, When đăng nhập, Then bị từ chối với thông báo "account locked".
- **AC4:** Given đăng nhập role STUDENT, When truy cập `/admin/**`, Then nhận trang 403.
- **AC5:** Given chưa đăng nhập, When truy cập `/student/**`, Then bị chuyển tới `/login`.

### US-02 (F-M02-01..04) – Trang chủ
*Là khách, tôi muốn xem các cuộc thi và học viên đạt giải để biết hoạt động của học viện.*
- **AC1:** Given có cuộc thi startDate > hôm nay, When mở trang chủ, Then nó nằm trong mục "Upcoming".
- **AC2:** Given cuộc thi ONGOING có 12 bài nộp, When mở trang chủ, Then hiển thị cuộc thi đó với "12 bài dự thi".
- **AC3:** Given cuộc thi GRADED gần nhất có 3 giải đã trao, When mở trang chủ, Then hiển thị 3 học viên + tên giải.
- **AC4:** Given cuộc thi CLOSED chưa công bố, Then **không** hiển thị kết quả của cuộc thi đó.

### US-03 (F-M03-01..05) – Admin quản lý nhân viên
*Là Admin, tôi muốn thêm/sửa/xóa/tìm nhân viên kèm thông tin đăng nhập, cá nhân, công việc, ghi chú để quản lý nhân sự.*
- **AC1:** Given form đầy đủ hợp lệ, When lưu, Then tạo Account (mật khẩu BCrypt) + StaffProfile, nhân viên đăng nhập được.
- **AC2:** Given username đã tồn tại, When lưu, Then báo "Username already exists", không tạo bản ghi.
- **AC3:** Given nhập "toán" vào ô môn phụ trách, When tìm kiếm, Then chỉ hiển thị nhân viên có môn chứa "toán".
- **AC4:** Given nhân viên đã từng chấm bài, When xóa, Then hệ thống vô hiệu hóa thay vì xóa và thông báo rõ.

### US-04 (F-M04-01..06) – Admin quản lý học viên; Staff xem học viên
*Là Admin, tôi muốn quản lý hồ sơ học viên (đăng nhập, cá nhân, nhập học); là Staff, tôi muốn xem hồ sơ học viên.*
- **AC1:** Given dữ liệu hợp lệ, When Admin lưu học viên mới, Then học viên đăng nhập được với role STUDENT.
- **AC2:** Given học viên chưa có bài nộp, When Admin xóa và xác nhận, Then bản ghi bị xóa.
- **AC3:** Given đăng nhập STAFF, When mở chi tiết học viên, Then thấy thông tin cá nhân + nhập học, **không** có nút Sửa/Xóa.

### US-05 (F-M05-01..06, 09) – Quản lý cuộc thi
*Là Staff, tôi muốn tạo cuộc thi với ngày, thể lệ và giải thưởng để học viên tham gia.*
- **AC1:** Given startDate > endDate, When lưu, Then báo lỗi "End date must be on or after the start date".
- **AC2:** Given cuộc thi đã có bài nộp, When xóa, Then bị từ chối với thông báo "This competition already has submissions".
- **AC3:** Given cuộc thi có 3 CompetitionAward, When học viên xem chi tiết, Then thấy 3 giải với giá trị và số lượng.
- **AC4:** Given hôm nay nằm giữa start và end, When xem danh sách, Then trạng thái hiển thị ONGOING.

### US-06 (F-M06-01) – Nộp bài
*Là học viên, tôi muốn upload tranh JPEG kèm thơ/trích dẫn trước hạn để tham gia cuộc thi.*
- **AC1:** Given cuộc thi ONGOING và file .jpg 2 MB, When nộp, Then bài được lưu, file tồn tại trong `uploads/submissions/{id}/`, thấy bài trong "My Submissions".
- **AC2:** Given file .png (hoặc file .jpg giả mạo không có magic bytes JPEG), When nộp, Then báo "Only JPEG images are accepted", không lưu.
- **AC3:** Given file 6 MB, When nộp, Then báo "The image exceeds 5 MB".
- **AC4:** Given cuộc thi đã qua hạn, When gửi request nộp (kể cả gọi thẳng URL), Then bị từ chối "The submission deadline has passed".
- **AC5:** Given học viên đã có bài trong cuộc thi, When chọn nộp lần nữa, Then được chuyển sang màn hình sửa bài.

### US-07 (F-M06-02..04) – Sửa/xóa bài
*Là học viên, tôi muốn sửa tranh hoặc phần chữ, hoặc xóa bài khi chưa hết hạn.*
- **AC1:** Given bài của tôi, cuộc thi ONGOING, When thay ảnh mới, Then ảnh mới hiển thị và file cũ bị xóa khỏi đĩa.
- **AC2:** Given cuộc thi CLOSED, When mở bài, Then không thấy nút Sửa/Xóa.
- **AC3:** Given bài của học viên khác, When gọi URL sửa/xóa, Then nhận 403.
- **AC4:** Given ONGOING, When xóa và xác nhận, Then bài + file bị xóa, tổng bài nộp trên trang chủ giảm 1.

### US-08 (F-M06-05, 06) – Tìm kiếm & tải bài
*Là học viên, tôi muốn tìm và tải lại các bài mình đã nộp.*
- **AC1:** Given tôi có 5 bài, When lọc theo cuộc thi X, Then chỉ hiện bài của cuộc thi X.
- **AC2:** When bấm "Download", Then trình duyệt tải file JPEG với tên file gốc (header `Content-Disposition: attachment`).

### US-09 (F-M07-01..06) – Chấm bài
*Là Staff, tôi muốn xem bài và xếp loại kèm nhận xét để đánh giá học viên.*
- **AC1:** Given cuộc thi ONGOING, When mở trang chấm, Then nút chấm bị khóa với thông báo "The submission period has not ended yet".
- **AC2:** Given CLOSED, When chọn BETTER, nhập điểm mạnh, lưu, Then bài chuyển sang "Graded", tiến độ tăng 1.
- **AC3:** Given chọn DISQUALIFIED và để trống lý do, When lưu, Then báo lỗi bắt buộc nhập lý do.
- **AC4:** When lọc xếp loại = BEST, Then chỉ hiện bài BEST.
- **AC5:** Given cuộc thi GRADED, When sửa đánh giá, Then bị từ chối.

### US-10 (F-M05-08, F-M06-07, 08) – Công bố & xem kết quả
*Là học viên, tôi muốn xem xếp loại và nhận xét cho các bài của mình, và xem các bài BEST để học hỏi.*
- **AC1:** Given cuộc thi còn 2 bài chưa chấm, When Staff bấm công bố, Then bị từ chối "2 submissions have not been graded yet".
- **AC2:** Given cuộc thi CLOSED (chưa công bố) đã chấm bài tôi, When tôi xem bài, Then hiển thị "Awaiting results".
- **AC3:** Given GRADED, When xem "My Results", Then thấy xếp loại, điểm mạnh, điểm yếu, cần cải thiện cho mọi bài đã tham gia.
- **AC4:** Given GRADED, When mở "Best Works", Then thấy mọi bài BEST kèm nhận xét staff.

### US-11 (F-M08-01..04) – Trao giải
*Là Staff, tôi muốn ghi nhận giải đã trao cho học viên để công bố và lưu trữ.*
- **AC1:** Given giải "Nhất" quantity = 1 đã trao, When trao thêm, Then bị từ chối "No award slots left".
- **AC2:** Given bài xếp loại GOOD, When trao giải, Then bài không xuất hiện trong danh sách chọn.
- **AC3:** Given học viên đã nhận giải Nhì cuộc thi này, When trao thêm giải Ba cho cùng bài, Then bị từ chối.
- **AC4:** When học viên xem "Awards", Then thấy giải đã trao của các cuộc thi GRADED.

### US-12 (F-M09-01..03) – Quản lý triển lãm
*Là Staff/Manager, tôi muốn quản lý các triển lãm sắp tới trong nước để học viên biết và gửi tranh.*
- **AC1:** Given dữ liệu hợp lệ, When Manager tạo triển lãm, Then học viên thấy triển lãm trong mục "Upcoming Exhibitions".
- **AC2:** Given triển lãm đã có tranh gửi, When xóa, Then bị từ chối.

### US-13 (F-M09-04..12) – Tranh triển lãm, bán & trả tiền
*Là học viên, tôi muốn gửi tranh đạt BEST/BETTER đi triển lãm với giá tôi đặt và theo dõi tình trạng bán, nhận tiền.*
- **AC1:** Given bài BEST, triển lãm UPCOMING, When gửi với giá $200.00, Then tạo entry PENDING với askingPrice = 200.00.
- **AC2:** Given bài GOOD, When gửi, Then bài không có trong danh sách chọn.
- **AC3:** Given entry ACCEPTED, When Staff đánh dấu SOLD mà không chọn khách mua, Then báo lỗi.
- **AC4:** Given entry PENDING, When Staff nhập "Paid to student", Then bị từ chối (chưa SOLD).
- **AC5:** Given SOLD $250.00 và đã trả, When học viên xem, Then thấy: giá đề xuất, "Sold", giá khách trả $250.00, "Paid" + ngày trả.
- **AC6:** Given tranh đã SOLD, When gửi sang triển lãm khác, Then bị từ chối.

### US-14 (F-M10-01) – Báo cáo bài nộp
*Là Staff/Manager, tôi muốn xem các bài nộp trong khoảng thời gian cho 1 cuộc thi theo xếp loại.*
- **AC1:** Given cuộc thi X, từ 01/11 đến 10/11, xếp loại BEST, When chạy báo cáo, Then chỉ liệt kê bài của X, `submittedAt` trong khoảng, rating BEST, kèm tổng số dòng.
- **AC2:** Given không có dữ liệu, Then hiển thị "No data".

### US-15 (F-M11-01..08) – Manager xem
*Là Manager, tôi muốn xem toàn bộ học viên, nhân viên, cuộc thi, bài nộp, đánh giá, giải, tranh triển lãm.*
- **AC1:** Given đăng nhập MANAGER, When mở mọi màn hình xem, Then không có nút Thêm/Sửa/Xóa (trừ Triển lãm).
- **AC2:** Given MANAGER gọi URL POST sửa cuộc thi, Then nhận 403.
- **AC3:** When mở dashboard, Then thấy các số liệu tổng (HV, NV, cuộc thi theo trạng thái, bài nộp, giải, tranh đã bán, tổng tiền bán).

---

## 7. Danh sách báo cáo

| Mã | Tên báo cáo | Tham số lọc | Cột hiển thị | Tổng/Thống kê | Actor |
|---|---|---|---|---|---|
| RPT-01 | **Bài nộp theo khoảng thời gian cho 1 cuộc thi theo xếp loại** (yêu cầu đề bài) | Cuộc thi (bắt buộc), Từ ngày – Đến ngày (theo `submittedAt`), Xếp loại (nhiều lựa chọn, có "Not graded"), Lớp (tùy chọn) | STT, Mã HV, Họ tên, Lớp, Tiêu đề tranh, Ngày nộp, Ngày sửa cuối, Xếp loại, Người chấm, Ngày chấm, Đạt giải (có/không) | Tổng số bài; số bài theo từng xếp loại | STAFF, MANAGER |
| RPT-02 | Thống kê tham gia & xếp loại theo cuộc thi | Khoảng thời gian cuộc thi, Trạng thái cuộc thi | Cuộc thi, Ngày bắt đầu–kết thúc, Trạng thái, Số HV (active), Số bài nộp, % tham gia, Số BEST/BETTER/GOOD/MODERATE/NORMAL/DISQUALIFIED, Số chưa chấm | Dòng tổng; chi tiết: danh sách HV **chưa nộp** (BR-12) | STAFF, MANAGER |
| RPT-03 | Giải thưởng đã trao | Cuộc thi, Khoảng ngày trao, Học viên | Cuộc thi, Giải, Hạng, Giá trị giải (USD), Học viên, Tranh, Ngày trao, Đã phát thưởng, Ghi chú | Tổng số giải, **tổng giá trị giải** (Financial) | STAFF, MANAGER |
| RPT-04 | Tranh triển lãm & doanh thu bán tranh | Triển lãm, Khoảng ngày, Trạng thái entry, Học viên | Triển lãm, Tranh, Học viên, Xếp loại, Giá đề xuất, Trạng thái, Giá bán, Ngày bán, Khách mua, Đã trả HV, Số tiền trả, Ngày trả | Số tranh gửi/đã bán/chưa bán, **tổng giá đề xuất, tổng giá bán**, chênh lệch | STAFF, MANAGER |
| RPT-05 | Công nợ trả tiền cho học viên | Triển lãm, Học viên, Khoảng ngày bán | Học viên, Mã HV, Tranh, Triển lãm, Ngày bán, Giá bán, Số ngày chưa trả | Tổng tiền chưa trả | STAFF, MANAGER |
| RPT-06 | Hồ sơ thành tích học viên | Học viên (bắt buộc), Khoảng thời gian | Cuộc thi, Tranh, Ngày nộp, Xếp loại, Nhận xét tóm tắt, Giải, Triển lãm/Đã bán | Số cuộc thi tham gia, số BEST, số giải, tổng tiền bán tranh | STAFF, MANAGER |
| RPT-07 | Thống kê tài khoản | Role, Trạng thái, Lớp/Khóa | Role, Số tài khoản active/inactive; HV theo lớp/khóa | Tổng | ADMIN, MANAGER |

> Báo cáo hiển thị dạng bảng HTML có nút "In" (CSS `@media print`). Xuất PDF/Excel thuộc M14 (Could).

---

## 8. Yêu cầu phi chức năng

| Mã | Nhóm | Yêu cầu | Cách đáp ứng / kiểm chứng |
|---|---|---|---|
| NFR-01 | Bảo mật – mật khẩu | Băm BCrypt (strength 10), không lưu/ghi log mật khẩu rõ | `BCryptPasswordEncoder`; kiểm tra DB không có plaintext |
| NFR-02 | Bảo mật – phân quyền | Kiểm tra quyền ở URL + service (ownership); mặc định từ chối | `[Authorize(Roles = ...)]` theo Area + kiểm tra chủ sở hữu trong Service; test 403 |
| NFR-03 | Bảo mật – web | CSRF token cho mọi form POST; chống XSS (Razor tự mã hóa HTML); chống SQL injection (EF Core/LINQ tham số hóa) | Anti-forgery token của ASP.NET Core, Razor encode; không nối chuỗi SQL |
| NFR-04 | Bảo mật – file | Chỉ JPEG (magic bytes), ≤ 5 MB, tên file Guid, chặn path traversal, phục vụ ảnh qua controller có kiểm tra quyền | BR-16..18, BR-20 |
| NFR-05 | Session | Hết hạn sau 30 phút không hoạt động; đăng xuất hủy session | Cookie đăng nhập `ExpireTimeSpan = 30 phút`, `SlidingExpiration` |
| NFR-06 | Validate | Validate cả client (HTML5/Bootstrap) và server (DataAnnotations `[Required]`, `[StringLength]`, `[EmailAddress]`, `[Range]`…); thông báo lỗi **tiếng Anh** tại từng trường | Test nhập sai |
| NFR-07 | Toàn vẹn dữ liệu | Ràng buộc FK, UNIQUE (BR-01, BR-15, BR-31), CHECK (giá ≥ 0, ngày); thao tác ghi nhiều bảng trong `@Transactional`; file và DB nhất quán (xóa file khi rollback) | Script DDL Phần 2 |
| NFR-08 | Hiệu năng | Trang danh sách < 2 giây với 1.000 bài nộp; phân trang 10–20 dòng; index các cột lọc (competitionId, studentId, submittedAt, rating) | Test với dữ liệu mẫu |
| NFR-09 | Khả dụng (usability) | Giao diện nhất quán Bootstrap 5, menu theo role, breadcrumb, thông báo flash thành công/lỗi, xác nhận trước khi xóa | GUI Standards Document |
| NFR-10 | Responsive | Dùng tốt trên desktop, tablet, mobile (≥ 360 px) | Kiểm tra Chrome DevTools |
| NFR-11 | Ngôn ngữ | **Giao diện và dữ liệu mẫu bằng tiếng Anh** (ngôn ngữ chính). UTF-8 toàn hệ thống (DB `NVARCHAR`, collation `Vietnamese_100_CI_AS_SC_UTF8`) để vẫn lưu và hiển thị đúng nội dung tiếng Việt người dùng nhập (thơ, mô tả). Giao diện tiếng Việt là tính năng phụ (M19 – Could) | Kiểm tra mọi trang hiển thị tiếng Anh; nhập thử tiếng Việt có dấu |
| NFR-12 | Tương thích | Chrome, Edge, Firefox bản mới | |
| NFR-13 | Sao lưu | Theo đặc tả eProject: **sao lưu mã nguồn và dữ liệu mỗi ngày** – Git push hằng ngày; script `BACKUP DATABASE` SQL Server + nén thư mục `uploads/` | Checklist hằng ngày |
| NFR-14 | Khả năng bảo trì | Kiến trúc phân lớp Controller – Service – DbContext (EF Core) – Entity; **mọi khối mã có chú thích** (yêu cầu đặc tả); đặt tên theo chuẩn C# | Code review |
| NFR-15 | Cấu hình | Đường dẫn `uploads/`, giới hạn dung lượng, kết nối DB nằm trong `appsettings.json` | |
| NFR-16 | Ghi log | Log lỗi (SLF4J/Logback) ra file; không log dữ liệu nhạy cảm | |
| NFR-17 | Múi giờ & định dạng | Văn hóa mặc định `en-US` (cố định trong `Program.cs`, không phụ thuộc ngôn ngữ Windows); múi giờ học viện Asia/Ho_Chi_Minh; ngày hiển thị `dd MMM yyyy` (ví dụ 05 Nov 2026); tiền `$#,##0.00` | Nhập giá có phần thập phân trên máy Windows tiếng Việt vẫn đúng |
| NFR-18 | Triển khai | Chạy được bằng `dotnet run` (hoặc F5 trong Visual Studio) trên Windows, SQL Server 2019+; có script tạo DB + dữ liệu mẫu | Hướng dẫn cài đặt |

---

## 9. Danh sách màn hình (Screen list)

### 9.1 Chung / GUEST

| Mã | Màn hình | URL gợi ý | Chức năng liên quan |
|---|---|---|---|
| SCR-01 | Trang chủ | `/` | F-M02-01..04, 06, 07 |
| SCR-02 | Chi tiết cuộc thi (công khai) | `/competitions/{id}` | F-M02-05 |
| SCR-03 | Đăng nhập | `/login` | F-M01-01 |
| SCR-04 | Hồ sơ cá nhân & đổi mật khẩu | `/profile` | F-M01-04, 05 |
| SCR-05 | Trang lỗi 403/404/500 | `/error` | F-M01-07 |
| SCR-06 | Giới thiệu / Gallery tranh đạt giải | `/about`, `/gallery` | F-M02-07, 08 |

### 9.2 ADMIN

| Mã | Màn hình | URL gợi ý | Chức năng liên quan |
|---|---|---|---|
| SCR-10 | Dashboard Admin | `/admin` | F-M10-07 |
| SCR-11 | Danh sách & tìm kiếm nhân viên | `/admin/staff` | F-M03-04, 07 |
| SCR-12 | Form thêm/sửa nhân viên | `/admin/staff/new`, `/admin/staff/{id}/edit` | F-M03-01, 02, 06 |
| SCR-13 | Chi tiết nhân viên | `/admin/staff/{id}` | F-M03-05, 03 |
| SCR-14 | Danh sách & tìm kiếm học viên | `/admin/students` | F-M04-04, 07 |
| SCR-15 | Form thêm/sửa học viên | `/admin/students/new`, `.../{id}/edit` | F-M04-01, 02 |
| SCR-16 | Chi tiết học viên | `/admin/students/{id}` | F-M04-05, 03 |

### 9.3 STAFF

| Mã | Màn hình | URL gợi ý | Chức năng liên quan |
|---|---|---|---|
| SCR-20 | Dashboard Staff (cuộc thi cần chấm, đề xuất triển lãm chờ duyệt) | `/staff` | F-M07-09 |
| SCR-21 | Danh sách học viên (xem) | `/staff/students` | F-M04-06 |
| SCR-22 | Chi tiết học viên + hồ sơ học tập | `/staff/students/{id}` | F-M04-06, F-M07-07 |
| SCR-23 | Danh sách & tìm kiếm cuộc thi | `/staff/competitions` | F-M05-04 |
| SCR-24 | Form thêm/sửa cuộc thi | `/staff/competitions/new`, `.../{id}/edit` | F-M05-01, 02 |
| SCR-25 | Chi tiết cuộc thi (tabs: Thông tin, Giải thưởng, Bài nộp, Trao giải) + nút Công bố | `/staff/competitions/{id}` | F-M05-03, 05, 06, 08 |
| SCR-26 | Danh sách bài nộp để chấm (lọc xếp loại) | `/staff/competitions/{id}/submissions` | F-M07-01, 06, 09 |
| SCR-27 | Chấm bài (ảnh + form Evaluation) | `/staff/submissions/{id}/evaluate` | F-M07-02..05, 08, F-M06-06 |
| SCR-28 | Trao giải (form) & danh sách giải đã trao | `/staff/awards` | F-M08-01..05 |
| SCR-29 | Danh sách & form triển lãm | `/staff/exhibitions` | F-M09-01, 02 |
| SCR-30 | Tranh triển lãm (danh sách, duyệt, bán, trả tiền) | `/staff/exhibition-entries` | F-M09-05, 06, 08, 09 |
| SCR-31 | Form cập nhật bán tranh / trả tiền | `/staff/exhibition-entries/{id}` | F-M09-06, 08 |
| SCR-32 | Quản lý khách mua | `/staff/customers` | F-M09-07 |
| SCR-33 | Báo cáo (chọn báo cáo + tham số + kết quả) | `/reports/{code}` | F-M10-01..06, 08 |

### 9.4 STUDENT

| Mã | Màn hình | URL gợi ý | Chức năng liên quan |
|---|---|---|---|
| SCR-40 | Dashboard học viên (cuộc thi đang mở, hạn nộp, kết quả mới) | `/student` | F-M06-10 |
| SCR-41 | Danh sách cuộc thi (sắp tới / đang diễn ra / đã kết thúc) | `/student/competitions` | F-M05-09 |
| SCR-42 | Chi tiết cuộc thi + giải + thể lệ + nút Nộp bài | `/student/competitions/{id}` | F-M05-09 |
| SCR-43 | Form nộp/sửa bài (upload JPEG) | `/student/submissions/new?competitionId=`, `.../{id}/edit` | F-M06-01..03 |
| SCR-44 | Bài của tôi (danh sách, tìm kiếm, xóa) | `/student/submissions` | F-M06-04, 05 |
| SCR-45 | Chi tiết bài của tôi + kết quả chấm + tải ảnh | `/student/submissions/{id}` | F-M06-06, 07 |
| SCR-46 | Bài xuất sắc (BEST) kèm nhận xét | `/student/best` | F-M06-08 |
| SCR-47 | Giải thưởng đã trao theo cuộc thi | `/student/awards` | F-M06-09, F-M08-04 |
| SCR-48 | Triển lãm sắp tới & gửi tranh | `/student/exhibitions` | F-M09-02, 03, 04 |
| SCR-49 | Tranh triển lãm của tôi (trạng thái, bán, nhận tiền) | `/student/exhibition-entries` | F-M09-10, 12 |

### 9.5 MANAGER

| Mã | Màn hình | URL gợi ý | Chức năng liên quan |
|---|---|---|---|
| SCR-50 | Dashboard Manager (số liệu tổng) | `/manager` | F-M11-01 |
| SCR-51 | Xem học viên / nhân viên | `/manager/students`, `/manager/staff` | F-M11-02 |
| SCR-52 | Xem cuộc thi (danh sách + chi tiết) | `/manager/competitions` | F-M11-03 |
| SCR-53 | Xem bài nộp theo cuộc thi + đánh giá của 1 bức tranh | `/manager/submissions`, `/manager/submissions/{id}` | F-M11-05, 06 |
| SCR-54 | Xem giải đã trao | `/manager/awards` | F-M11-04 |
| SCR-55 | Xem tranh triển lãm | `/manager/exhibition-entries` | F-M11-07 |
| SCR-56 | Quản lý triển lãm (dùng lại SCR-29 với quyền MANAGER) | `/manager/exhibitions` | F-M11-08 |
| SCR-33 | Báo cáo (dùng chung) | `/reports/{code}` | F-M10-* |

> Tổng ≈ 45 màn hình; nhiều màn hình xem của MANAGER dùng chung View/partial với STAFF (ẩn nút ghi theo role) để giảm khối lượng cho Frontend.

---

## 10. Điểm mơ hồ, giả định & câu hỏi DOUBT

### 10.1 Điểm mơ hồ & giả định đề xuất

| Mã | Điểm mơ hồ trong đề bài | Giả định của nhóm |
|---|---|---|
| A-01 | Không nói ai tạo tài khoản học viên/nhân viên | Admin tạo mọi tài khoản; không có tự đăng ký. Tài khoản ADMIN đầu tiên tạo bằng script seed |
| A-02 | Một người có thể vừa là STAFF vừa là MANAGER? | Mỗi Account đúng 1 role. Manager/Admin cũng có StaffProfile (đề bài: "staffs (manager, administrator, lecturers)") |
| A-03 | Bản tiếng Anh viết "after the due date the college will **not** evaluate…" mâu thuẫn ngữ cảnh | Hiểu là lỗi đánh máy: sau hạn nộp trường **mới** đánh giá (khớp bản dịch). Chấm chỉ khi CLOSED (BR-21) |
| A-04 | "Disqualified (nếu ngày nộp sau ngày kết thúc)" trong khi website phải chặn nộp trễ | Hệ thống chặn nộp trễ (BR-14); DISQUALIFIED vẫn có cho trường hợp ngoại lệ/nhập bù hoặc vi phạm thể lệ; HV không nộp thì hiện ở RPT-02 |
| A-05 | Mỗi HV được nộp mấy bài/cuộc thi? | **1 bài/HV/cuộc thi** (BR-15); muốn đổi thì sửa/thay ảnh |
| A-06 | Định dạng ảnh, dung lượng | Chỉ JPEG, ≤ 5 MB (BR-16, BR-17) |
| A-07 | Staff nào được chấm bài? Có nhiều giám khảo không? | Bất kỳ STAFF nào; 1 Evaluation/bài (không tính điểm trung bình nhiều giám khảo) |
| A-08 | Khi nào học viên thấy kết quả? | Sau khi Staff công bố (GRADED) |
| A-09 | Ai được nhận giải? | Chỉ bài BEST; tối đa 1 giải/HV/cuộc thi; số lượng theo `quantity` |
| A-10 | Học viện có trích % tiền bán tranh? | Không – 100% tiền bán trả cho HV ("handed over to the student itself") |
| A-11 | Ai gửi tranh đi triển lãm: Student hay Staff? (đề bài nói cả hai) | Cả hai: HV đề xuất (PENDING) + Staff duyệt; hoặc Staff tạo trực tiếp với giá HV báo |
| A-12 | Tranh "đẹp nhất" được triển lãm là xếp loại nào? | BEST hoặc BETTER (BR-40) |
| A-13 | "View all postings marked as best" – của riêng HV hay tất cả? | Tất cả bài BEST của mọi HV ở cuộc thi đã GRADED (gallery học hỏi) |
| A-14 | Exhibition được quản lý bởi Staff (mục 4) và Manager (mục 6) | Cả STAFF và MANAGER đều CRUD Exhibition; ExhibitionEntry do STAFF quản lý, MANAGER chỉ xem |
| A-15 | "Award details" gồm gì (Financial: giải sẽ trao) | CompetitionAward: tên, hạng, giá trị tiền (USD), số lượng, mô tả phần thưởng |
| A-16 | "Remarks of the competition" trong giải đã trao | Trường `remarks` trong AwardIssued (nhận xét của BTC) |
| A-17 | "Academic records" của học viên | = lịch sử Submission + Evaluation + AwardIssued (không có điểm môn học) |
| A-18 | Bán tranh: thanh toán thế nào? | Ngoài hệ thống (tiền mặt/chuyển khoản tại triển lãm); hệ thống chỉ ghi nhận |
| A-19 | Đơn vị tiền tệ | **USD** – sản phẩm được team nước ngoài chấm |
| A-20 | "Upload & download image" – ai tải? | HV tải bài của mình; STAFF/MANAGER tải mọi bài; khách chỉ xem ảnh công khai (không tải bản gốc) |
| A-21 | Trang chủ "danh sách học viên đạt giải cuộc thi trước" | Cuộc thi GRADED có `endDate` gần nhất (có thể hiển thị thêm 3 cuộc thi gần nhất) |
| A-22 | Công nghệ | **Giảng viên đã xác nhận làm bằng .NET**: ASP.NET Core MVC (.NET 10) + EF Core + SQL Server 2022 (đặc tả cho phép .Net và SQL Server "hoặc mới hơn") |

### 10.2 Câu hỏi gửi eProjects Team (email tiêu đề bắt đầu bằng **"DOUBT:"**)

> **Tiêu đề:** `DOUBT: Institute of Fine Arts – Clarification on business rules (Team <tên nhóm>)`

| # | Câu hỏi (tiếng Anh, gửi email) | Liên quan |
|---|---|---|
| Q1 | Can a student submit more than one painting per competition, or only one (which they can edit before the end date)? | A-05, BR-15 |
| Q2 | Since the website blocks submissions after the end date, in which cases should staff use the "Disqualified" mark? Should students who did not submit be recorded as disqualified? | A-04, BR-14 |
| Q3 | The English statement says "after the due date the college will not evaluate the designs" — we assume it means evaluation starts *after* the due date. Is that correct? | A-03 |
| Q4 | Which ratings make a painting eligible for awards (only "Best"?) and for exhibitions ("Best" and "Better"?) | A-09, A-12 |
| Q5 | Who forwards a painting to an exhibition — the student, the staff, or both (student proposes, staff approves)? | A-11 |
| Q6 | Does the institute keep any commission from the sold price, or is the whole amount paid to the student? | A-10 |
| Q7 | Should students register themselves, or are all accounts created by the Administrator? | A-01 |
| Q8 | Is there a required maximum file size / minimum resolution for the JPEG files? | A-06 |
| Q9 | We will build the project with ASP.NET Core MVC (.NET 10) and SQL Server 2022, which are newer than the versions listed in the specification. Is that acceptable? | A-22 |
| Q10 | Should "postings marked as best" visible to a student include other students' postings, or only their own? | A-13 |

---

## 11. Glossary Anh – Việt

| Thuật ngữ (EN) | Tiếng Việt | Ghi chú trong hệ thống |
|---|---|---|
| Institute of Fine Arts | Học viện Mỹ thuật | Tên dự án |
| Administrator (ADMIN) | Quản trị viên | Quản lý tài khoản |
| Manager (MANAGER) | Quản lý | Xem toàn bộ, quản lý triển lãm |
| Staff / Teacher / Lecturer (STAFF) | Nhân viên / Giáo viên / Giảng viên | Tạo cuộc thi, chấm bài |
| Student (STUDENT) | Học viên | Nộp bài |
| Guest (GUEST) | Khách | Chưa đăng nhập |
| Account | Tài khoản | Thông tin đăng nhập + role |
| StaffProfile / StudentProfile | Hồ sơ nhân viên / học viên | |
| Admission details | Thông tin nhập học | Mã HV, ngày nhập học, khóa, lớp |
| Work details | Thông tin công việc | Ngày vào làm, lớp, môn phụ trách |
| Remarks | Ghi chú / Nhận xét | |
| Competition | Cuộc thi | |
| Start date / End date (Due date) | Ngày bắt đầu / Ngày kết thúc (hạn nộp) | |
| Conditions | Thể lệ / Điều kiện | |
| CompetitionAward | Giải thưởng (định nghĩa cho cuộc thi) | Nhất, Nhì, Ba… |
| AwardIssued | Giải đã trao | |
| Submission / Posting / Design / Painting | Bài dự thi / Bài đăng / Bài thiết kế / Tranh | Cùng chỉ Submission |
| Poem / Quotation / Story / Description | Thơ / Trích dẫn / Câu chuyện / Mô tả | Phần chữ kèm tranh |
| Evaluation / Marks | Đánh giá / Xếp loại | |
| BEST / BETTER / GOOD / MODERATE / NORMAL | Xuất sắc nhất / Rất tốt / Tốt / Trung bình / Bình thường | Rating |
| DISQUALIFIED | Bị loại | |
| Positive points / Negative points | Điểm mạnh / Điểm yếu | |
| Areas of improvement | Điểm cần cải thiện | |
| Exhibition | Triển lãm | Trong nước |
| ExhibitionEntry | Tranh gửi triển lãm | |
| Asking price (quoted price) | Giá đề xuất (giá học viên đặt) | |
| Sold price | Giá bán thực tế | |
| Paid to student | Đã trả tiền cho học viên | |
| Customer | Khách mua tranh | |
| UPCOMING / ONGOING / CLOSED / GRADED | Sắp diễn ra / Đang diễn ra / Đã đóng / Đã công bố kết quả | Trạng thái cuộc thi |
| PENDING / ACCEPTED / REJECTED / SOLD / UNSOLD | Chờ duyệt / Đã chấp nhận / Bị từ chối / Đã bán / Không bán được | Trạng thái ExhibitionEntry |
| Upload / Download | Tải lên / Tải xuống | Bằng File I/O |
| File I/O (System.IO) | Đọc/ghi file | Bắt buộc theo đề |
| JPEG | Định dạng ảnh .jpg/.jpeg | |
| CRUD / Search & retrieve | Thêm, xem, sửa, xóa / Tìm kiếm & truy xuất | |
| Business Rule (BR) | Quy tắc nghiệp vụ | |
| Use case (UC) | Ca sử dụng | |
| User story (US) / Acceptance criteria (AC) | Câu chuyện người dùng / Tiêu chí chấp nhận | |
| MoSCoW (Must/Should/Could/Won't) | Phải có / Nên có / Có thể có / Không làm | |
| Stakeholder | Bên liên quan | |
| Status mail (STATUS:) / DOUBT: / PROJECT SUBMISSION: | Email báo tiến độ / Email thắc mắc / Email nộp dự án | Quy định eProject |

---

## 12. Phụ lục: Ma trận truy vết yêu cầu (Traceability)

Đối chiếu **từng dòng** đề bài với chức năng đã liệt kê.

| # | Yêu cầu đề bài | Chức năng / Quy tắc |
|---|---|---|
| 1 | 4 module Manager, Administrator, Staff, Student | M03/M04 (ADMIN), M05–M09 (STAFF), M06/M09 (STUDENT), M11 (MANAGER); F-M01-03 |
| 2 | Trang chủ: cuộc thi sắp tới, đang diễn ra, HV đạt giải cuộc thi trước, tổng bài nộp cuộc thi đang diễn ra | F-M02-01..04 |
| 3 | Admin CRUD + tìm kiếm nhân viên (login, cá nhân, công việc, ghi chú) | F-M03-01..05 |
| 4 | Admin CRUD + tìm kiếm học viên (login, cá nhân, nhập học) | F-M04-01..05 |
| 5 | Staff xem thông tin học viên | F-M04-06 |
| 6 | Staff CRUD cuộc thi (ngày, thể lệ, giải sẽ trao) | F-M05-01..06 |
| 7 | Staff quản lý hồ sơ học tập: xem bài, xếp loại BEST…DISQUALIFIED, nhận xét điểm mạnh/yếu/cải thiện | F-M07-01..07, BR-21..24 |
| 8 | Staff CRUD giải đã trao (HV nào, cuộc thi nào, nhận xét) | F-M08-01..04 |
| 9 | Staff CRUD triển lãm sắp tới trong nước | F-M09-01, 02 |
| 10 | Staff tìm bài theo xếp loại để gửi tranh đẹp nhất đi triển lãm với giá HV đặt | F-M07-06, F-M09-05, BR-40, BR-41 |
| 11 | Staff CRUD tranh gửi triển lãm: giá, đã bán?, giá bán, đã trả HV?, khách mua | F-M09-06..09, BR-43..45 |
| 12 | Student xem cuộc thi sắp tới + giải, chi tiết, thể lệ | F-M05-09 |
| 13 | Student upload tranh + trích dẫn, câu chuyện | F-M06-01 |
| 14 | Student sửa tranh/phần chữ/mô tả khi chưa quá hạn | F-M06-02, 03, BR-13 |
| 15 | Student xem giải đã trao theo cuộc thi | F-M06-09 |
| 16 | Student xem bài BEST kèm nhận xét staff | F-M06-08 |
| 17 | Student xem bài gửi triển lãm + trạng thái (giá, bán, trả tiền, khách trả bao nhiêu) | F-M09-10 |
| 18 | Student xem xếp loại & nhận xét mọi bài đã tham gia | F-M06-07 |
| 19 | Manager xem HV, NV | F-M11-02 |
| 20 | Manager xem cuộc thi đã tổ chức | F-M11-03 |
| 21 | Manager xem giải đã trao | F-M11-04 |
| 22 | Manager xem mọi bài theo cuộc thi | F-M11-05 |
| 23 | Manager xem xếp loại/nhận xét 1 bức tranh | F-M11-06 |
| 24 | Manager xem tranh staff đưa đi triển lãm | F-M11-07 |
| 25 | Manager quản lý triển lãm sắp tới | F-M11-08 (= F-M09-01) |
| 26 | Financial: giải sẽ trao, giá trước triển lãm, giá bán | CompetitionAward.prizeValue, ExhibitionEntry.askingPrice/soldPrice; RPT-03, RPT-04 |
| 27 | CSDL lưu: HV, NV (manager, admin, lecturer), cuộc thi, giải, bài đăng (ngày, mô tả, trích dẫn, xếp loại, nhận xét), triển lãm, giải đã trao, bài gửi triển lãm + trạng thái | Entity: Account, StaffProfile, StudentProfile, Competition, CompetitionAward, Submission, Evaluation, AwardIssued, Exhibition, ExhibitionEntry, Customer |
| 28 | Giao diện: HV upload/sửa/xóa/tìm/truy xuất tranh theo điều kiện | F-M06-01..06 |
| 29 | Giao diện: Staff xem bài, xếp loại, nhận xét | F-M07-01..04 |
| 30 | Báo cáo: bài nộp trong khoảng thời gian cho 1 cuộc thi theo xếp loại… | RPT-01 (F-M10-01) + RPT-02..07 |
| 31 | Upload/sửa/download ảnh dùng File I/O | F-M06-01, 03, 06; BR-18; NFR-04 |
| 32 | Website công bố cuộc thi, upload & download ảnh, HV sửa/xóa ảnh (Proposed Solution) | F-M02, F-M05, F-M06 |
| 33 | Quá hạn → không nhận bài, HV bị loại | BR-13, BR-14, BR-12 |
| 34 | Tiền bán tranh trả cho học viên | BR-45, F-M09-08 |
| 35 | Đặc tả: sao lưu mỗi ngày, chú thích mã, tài liệu đầy đủ | NFR-13, NFR-14 |

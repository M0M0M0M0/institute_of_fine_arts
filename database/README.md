# database – CSDL dùng để nộp bài

| File / thư mục | Nội dung | Khi nào có |
|---|---|---|
| `01_schema.sql` | Script tạo bảng, xuất từ EF Core migration: `dotnet ef migrations script --idempotent -o database/01_schema.sql --project src/InstituteOfFineArts.Web` | Day 5–6, xuất lại lần cuối Day 26 |
| `02_seed_data.sql` | Dữ liệu mẫu, xuất từ SSMS (*Generate Scripts → Types of data to script: Data only*) | Day 26 |
| `backups/` | File `.bak` (không đưa lên Git) – bản cuối `IFA_DB.bak` cho vào ZIP nộp | Hằng ngày / Day 26 |
| `seed-images/` | ~30 ảnh JPEG mẫu (800–1600px, ≤ 1MB) + `CREDITS.txt` ghi nguồn; `DbSeeder` chép vào `uploads/` | QA (task QA-01), Day 5–6 |

> CSDL do **EF Core Migrations** quản lý (`src/InstituteOfFineArts.Web/Data/Migrations/`). Thư mục này chỉ để nộp bài và backup – không sửa tay CSDL.

> **Chạy `01_schema.sql` trên máy khác:** tạo CSDL trước với collation tiếng Việt, rồi mới chạy script:
> ```sql
> CREATE DATABASE IFA_DB COLLATE Vietnamese_100_CI_AS_SC_UTF8;
> ```
> (Khi dùng `dotnet ef database update`, EF Core tự tạo CSDL với collation này.)

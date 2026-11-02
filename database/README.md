# database – CSDL dùng để nộp bài

| File / thư mục | Nội dung | Khi nào có |
|---|---|---|
| `01_schema.sql` | Script tạo bảng, xuất từ EF Core migration: `dotnet ef migrations script --idempotent -o database/01_schema.sql --project src/InstituteOfFineArts.Web` | Day 5–6, xuất lại lần cuối Day 26 |
| `02_seed_data.sql` | Dữ liệu mẫu, xuất từ SSMS (*Generate Scripts → Types of data to script: Data only*) | Day 26 |
| `backups/` | File `.bak` (không đưa lên Git) – bản cuối `IFA_DB.bak` cho vào ZIP nộp | Hằng ngày / Day 26 |
| `seed-images/` | ~30 ảnh JPEG mẫu (800–1600px, ≤ 1MB) + `CREDITS.txt` ghi nguồn; `DbSeeder` chép vào `uploads/` | T1/T2, tuần 1 |

> CSDL do **EF Core Migrations** quản lý (`src/InstituteOfFineArts.Web/Data/Migrations/`). Thư mục này chỉ để nộp bài và backup – không sửa tay CSDL.

# Institute of Fine Arts – eProject Sem 3

Website quản lý cuộc thi vẽ tranh của Học viện Mỹ thuật: học viên nộp tranh (JPEG + thơ/trích dẫn),
giáo viên chấm & nhận xét, trao giải, đưa tranh đi triển lãm và bán.

> **Trạng thái:** mới dựng khung (skeleton) – chưa có code chức năng (trang mặc định của template MVC).
> Kế hoạch đầy đủ: [`docs/plan/00_TONG_QUAN.md`](docs/plan/00_TONG_QUAN.md)

## Công nghệ
.NET 10 · ASP.NET Core MVC (C#) · Razor + Bootstrap 5.3 · Entity Framework Core 10 + SQL Server (`IFA_DB`) ·
Cookie Authentication + BCrypt · SkiaSharp (thumbnail) · Serilog · xUnit + Moq

## Cài đặt lần đầu (mỗi thành viên)
1. Cài **.NET SDK 10**, **Git**, **Visual Studio Community** (chọn workload *ASP.NET and web development*)
   hoặc VS Code + C# Dev Kit, và **một trong hai**:
   - **SQL Server** (Developer/Express) + **SSMS** cài trên máy, hoặc
   - **Docker Desktop** để chạy SQL Server trong Docker – xem mục [SQL Server bằng Docker](#sql-server-bằng-docker) bên dưới.
2. Cài công cụ EF Core (1 lần):
   ```powershell
   dotnet tool install --global dotnet-ef
   ```
3. Mở `InstituteOfFineArts.sln` bằng Visual Studio (hoặc chạy `dotnet restore` ở thư mục gốc).
4. Chuỗi kết nối mặc định dùng **Windows Authentication** tới `localhost` (xem `src/InstituteOfFineArts.Web/appsettings.json`).
   - SQL Server Express: đổi `Server=localhost\SQLEXPRESS`.
   - Dùng tài khoản SQL: không ghi mật khẩu vào file, đặt bằng User Secrets:
     ```powershell
     dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=IFA_DB;User Id=<user>;Password=<mật khẩu>;TrustServerCertificate=True" --project src/InstituteOfFineArts.Web
     ```
5. Tạo CSDL (sau khi đã có migration `InitialCreate` – Day 5–6):
   ```powershell
   dotnet ef database update --project src/InstituteOfFineArts.Web
   ```
6. Chạy ứng dụng: **F5** trong Visual Studio, hoặc
   ```powershell
   dotnet run --project src/InstituteOfFineArts.Web
   ```
   Mở http://localhost:5000

## SQL Server bằng Docker
Dùng khi máy **không cài SQL Server**. Chỉ CSDL chạy trong Docker, ứng dụng web vẫn chạy bằng F5 / `dotnet run`.

1. Mở Docker Desktop, chờ biểu tượng chuyển sang "Engine running".
2. Tạo file `.env` từ mẫu rồi **đổi mật khẩu** (tối thiểu 8 ký tự, có chữ hoa, chữ thường, số, ký hiệu). File `.env` không đưa lên Git.
   ```powershell
   Copy-Item .env.example .env
   ```
3. Khởi động SQL Server (lần đầu tải image khoảng 1,5 GB):
   ```powershell
   docker compose up -d
   ```
   Kiểm tra: `docker compose ps` thấy `ifa-sqlserver` ở trạng thái **healthy** là được.
4. Đặt chuỗi kết nối cho máy mình bằng User Secrets (thay `<mật khẩu>` bằng mật khẩu trong `.env`):
   ```powershell
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=IFA_DB;User Id=sa;Password=<mật khẩu>;TrustServerCertificate=True;MultipleActiveResultSets=true" --project src/InstituteOfFineArts.Web
   ```
5. Chạy ứng dụng như bình thường (F5 / `dotnet run`). Ở môi trường Development, ứng dụng tự tạo CSDL `IFA_DB` và dữ liệu mẫu.

| Việc | Lệnh |
|---|---|
| Tắt SQL Server (giữ dữ liệu) | `docker compose stop` |
| Bật lại | `docker compose start` |
| Xem log khi lỗi | `docker compose logs sqlserver` |
| Xóa sạch CSDL để seed lại từ đầu | `docker compose down -v` rồi `docker compose up -d` |
| Kết nối bằng SSMS / Azure Data Studio | Server `localhost,1433`, SQL Server Authentication, user `sa`, mật khẩu trong `.env`, chọn *Trust server certificate* |

> Máy đã có SQL Server dùng cổng 1433 thì đổi `SQL_PORT` trong `.env` (ví dụ `1434`) và sửa `localhost,1434` trong chuỗi kết nối.
> Script `scripts/backup-db.cmd` dùng cho SQL Server cài trên máy; với Docker, backup bằng SSMS (Tasks → Back Up) hoặc chép volume.

## Lệnh thường dùng
| Việc | Lệnh |
|---|---|
| Build | `dotnet build` |
| Chạy test | `dotnet test` |
| Tạo migration mới (chỉ BE) | `dotnet ef migrations add <TenMigration> --project src/InstituteOfFineArts.Web` |
| Cập nhật CSDL | `dotnet ef database update --project src/InstituteOfFineArts.Web` |
| Xuất script SQL để nộp | `dotnet ef migrations script --idempotent -o database/01_schema.sql --project src/InstituteOfFineArts.Web` |

## Cấu trúc thư mục
```
institute-of-fine-arts/
├── InstituteOfFineArts.sln
├── src/InstituteOfFineArts.Web/    ← project ASP.NET Core MVC (xem README.md bên trong để biết từng thư mục)
├── tests/InstituteOfFineArts.Tests/← unit test (xUnit + Moq)
├── database/                       ← script SQL + backup + ảnh mẫu để nộp bài
├── scripts/                        ← backup, restore CSDL
├── docs/                           ← đề bài, kế hoạch, tài liệu nộp, test case, báo cáo tiến độ
└── .github/                        ← mẫu Issue (bug/task) & Pull Request
```

## Quy ước làm việc
- Nhánh: `main` (bản nộp) ← `develop` (tích hợp) ← `feature/Mxx-ten-ngan`. Không push thẳng vào `main`.
- **Mọi khối code phải có comment** (yêu cầu của đề bài): `///` cho class/method, `//` cho từng khối xử lý.
- Chỉ **BE** tạo migration để tránh xung đột.
- Cuối ngày: push code + chạy `scripts/backup-db.cmd`.

## Tài khoản demo
(Bổ sung khi có dữ liệu mẫu – Day 6.)

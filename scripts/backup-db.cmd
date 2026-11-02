@echo off
REM ==========================================================
REM backup-db.cmd : Sao lưu CSDL IFA_DB + nén thư mục uploads
REM Lên lịch chạy hằng ngày bằng Windows Task Scheduler
REM ==========================================================
setlocal

REM --- Cấu hình: đổi theo máy (SQLEXPRESS dùng localhost\SQLEXPRESS) ---
set SERVER=localhost
set DB=IFA_DB
set BACKUP_DIR=D:\ifa-backup
set UPLOAD_DIR=%~dp0..\src\InstituteOfFineArts.Web\uploads

REM --- Lấy ngày giờ dạng yyyyMMdd_HHmm để đặt tên file ---
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmm"') do set STAMP=%%i

REM --- Tạo thư mục backup nếu chưa có ---
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

REM --- Backup CSDL bằng sqlcmd (-E = Windows Authentication) ---
REM     SQL Server Express không hỗ trợ COMPRESSION -> xóa ", COMPRESSION" nếu dùng Express
sqlcmd -S %SERVER% -E -b -Q "BACKUP DATABASE [%DB%] TO DISK = N'%BACKUP_DIR%\%DB%_%STAMP%.bak' WITH INIT, COMPRESSION, CHECKSUM, NAME = N'%DB% daily backup'"
if errorlevel 1 (
    echo [LOI] Backup CSDL that bai & exit /b 1
)

REM --- Nén thư mục ảnh (File I/O) đi kèm ---
powershell -NoProfile -Command "Compress-Archive -Path '%UPLOAD_DIR%\*' -DestinationPath '%BACKUP_DIR%\uploads_%STAMP%.zip' -Force"

REM --- Chỉ giữ 7 ngày gần nhất ---
forfiles /p "%BACKUP_DIR%" /m *.* /d -7 /c "cmd /c del @path" 2>nul

echo [OK] Da backup %DB% va uploads luc %STAMP%
endlocal

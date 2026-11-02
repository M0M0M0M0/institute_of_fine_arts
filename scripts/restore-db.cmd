@echo off
REM ==========================================================
REM restore-db.cmd : Khôi phục IFA_DB từ file .bak
REM Cách dùng: restore-db.cmd D:\ifa-backup\IFA_DB_20261029_2200.bak
REM ==========================================================
setlocal
set SERVER=localhost
set DB=IFA_DB
set BAK=%~1

REM --- Bắt buộc truyền đường dẫn file .bak ---
if "%BAK%"=="" ( echo Thieu duong dan file .bak & exit /b 1 )

REM --- Đưa DB về SINGLE_USER để ngắt kết nối của ứng dụng, restore, rồi mở lại MULTI_USER ---
sqlcmd -S %SERVER% -E -b -Q "ALTER DATABASE [%DB%] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [%DB%] FROM DISK = N'%BAK%' WITH REPLACE, CHECKSUM; ALTER DATABASE [%DB%] SET MULTI_USER;"
if errorlevel 1 ( echo [LOI] Restore that bai & exit /b 1 )

echo [OK] Da khoi phuc %DB%. Nho giai nen file uploads_*.zip tuong ung vao thu muc uploads.
endlocal

@echo off
REM ==========================================================
REM backup-db.cmd : Back up database IFA_DB + zip the uploads folder
REM Schedule it daily with Windows Task Scheduler
REM ==========================================================
setlocal

REM --- Settings: adjust per machine (SQL Server Express uses localhost\SQLEXPRESS) ---
set SERVER=localhost
set DB=IFA_DB
set BACKUP_DIR=D:\ifa-backup
set UPLOAD_DIR=%~dp0..\src\InstituteOfFineArts.Web\uploads

REM --- Current date/time as yyyyMMdd_HHmm for the file names ---
for /f %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmm"') do set STAMP=%%i

REM --- Create the backup folder if it does not exist ---
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

REM --- Back up the database with sqlcmd (-E = Windows Authentication) ---
REM     SQL Server Express does not support COMPRESSION -> remove ", COMPRESSION" when using Express
sqlcmd -S %SERVER% -E -b -Q "BACKUP DATABASE [%DB%] TO DISK = N'%BACKUP_DIR%\%DB%_%STAMP%.bak' WITH INIT, COMPRESSION, CHECKSUM, NAME = N'%DB% daily backup'"
if errorlevel 1 (
    echo [ERROR] Database backup failed & exit /b 1
)

REM --- Zip the image folder (File I/O) together with the backup ---
powershell -NoProfile -Command "Compress-Archive -Path '%UPLOAD_DIR%\*' -DestinationPath '%BACKUP_DIR%\uploads_%STAMP%.zip' -Force"

REM --- Keep only the last 7 days ---
forfiles /p "%BACKUP_DIR%" /m *.* /d -7 /c "cmd /c del @path" 2>nul

echo [OK] Backed up %DB% and uploads at %STAMP%
endlocal

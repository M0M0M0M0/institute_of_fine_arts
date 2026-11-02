@echo off
REM ==========================================================
REM restore-db.cmd : Restore IFA_DB from a .bak file
REM Usage: restore-db.cmd D:\ifa-backup\IFA_DB_20261029_2200.bak
REM ==========================================================
setlocal
set SERVER=localhost
set DB=IFA_DB
set BAK=%~1

REM --- The .bak file path is required ---
if "%BAK%"=="" ( echo Missing .bak file path & exit /b 1 )

REM --- Switch to SINGLE_USER to drop application connections, restore, then back to MULTI_USER ---
sqlcmd -S %SERVER% -E -b -Q "ALTER DATABASE [%DB%] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [%DB%] FROM DISK = N'%BAK%' WITH REPLACE, CHECKSUM; ALTER DATABASE [%DB%] SET MULTI_USER;"
if errorlevel 1 ( echo [ERROR] Restore failed & exit /b 1 )

echo [OK] Restored %DB%. Remember to unzip the matching uploads_*.zip into the uploads folder.
endlocal

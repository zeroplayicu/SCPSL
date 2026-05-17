@echo off
chcp 65001 >nul
title SCP Plugin Backup

set PROJECT_DIR=Z:\codebubby\SCPSL pulint
set CLAW_DIR=Z:\codebubby\claw
set BACKUP_DIR=%TEMP%\SCPSL_Backup
set BACKUP_ZIP=Z:\codebubby\SCPSL_Backup.zip

if exist "%BACKUP_DIR%" rmdir /s /q "%BACKUP_DIR%"
mkdir "%BACKUP_DIR%\SCPSL_Plugin"
mkdir "%BACKUP_DIR%\claw_forum"

echo [1/4] Backing up plugin project...
xcopy "%PROJECT_DIR%\*" "%BACKUP_DIR%\SCPSL_Plugin\" /E /I /Q /Y >nul

echo [2/4] Backing up claw forum...
xcopy "%CLAW_DIR%\*" "%BACKUP_DIR%\claw_forum\" /E /I /Q /Y >nul

echo [3/4] Copying compiled DLLs...
if exist "%PROJECT_DIR%\zeropl\*.dll" copy "%PROJECT_DIR%\zeropl\*.dll" "%BACKUP_DIR%\" /Y >nul

echo [4/4] Creating zip...
if exist "%BACKUP_ZIP%" del /f /q "%BACKUP_ZIP%"
powershell -NoProfile -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::CreateFromDirectory('%BACKUP_DIR%', '%BACKUP_ZIP%', 0, 0)"

echo.
echo === All done! ===
echo Backup saved to: %BACKUP_ZIP%
echo Total size:
dir "%BACKUP_ZIP%" | find "."
pause

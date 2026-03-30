@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ============================================================================
rem Backup SQLite DB with timestamped filename for Task Scheduler
rem Default:
rem   Source DB : E:\MISCMFactoryService\backend\workhour.db
rem   Output dir: E:\WorkhourDBBackups
rem   Output    : workhour_yyyyMMdd_HHmmss.db
rem
rem Usage:
rem   backup_workhour_db.bat
rem   backup_workhour_db.bat "E:\path\to\workhour.db" "E:\backup\folder"
rem ============================================================================

set "SRC_DB=E:\MISCMFactoryService\backend\workhour.db"
set "DEST_DIR=E:\WorkhourDBBackups"

if not "%~1"=="" set "SRC_DB=%~1"
if not "%~2"=="" set "DEST_DIR=%~2"

if not exist "%SRC_DB%" (
  echo [ERROR] Source DB not found: "%SRC_DB%"
  exit /b 1
)

if not exist "%DEST_DIR%" (
  mkdir "%DEST_DIR%" >nul 2>&1
  if errorlevel 1 (
    echo [ERROR] Failed to create backup directory: "%DEST_DIR%"
    exit /b 2
  )
)

rem Build locale-independent timestamp via PowerShell
for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "TS=%%I"
if "%TS%"=="" (
  echo [ERROR] Failed to generate timestamp.
  exit /b 3
)

set "DEST_FILE=%DEST_DIR%\workhour_%TS%.db"

copy /Y "%SRC_DB%" "%DEST_FILE%" >nul
if errorlevel 1 (
  echo [ERROR] Backup failed: "%SRC_DB%" ^> "%DEST_FILE%"
  exit /b 4
)

echo [OK] Backup created: "%DEST_FILE%"
exit /b 0

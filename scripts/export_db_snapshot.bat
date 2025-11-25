@echo off
REM Export DB tables to CSV - wrapper for Task Scheduler
REM This batch calls the repository's export_sqlite.py script with fixed arguments
REM Adjust DB_PATH and OUTPUT_DIR below if your environment differs.

REM Path to the SQLite DB to export
set "DB_PATH=E:\MISCMFactoryService\backend\workhour.db"

REM Output directory where CSV files will be written
set "OUTPUT_DIR=E:\GitHub\WorkHourTool\exports\DB_Snapshot"

REM Relative script path (from repo root)
set "SCRIPT_REL=scripts\export_sqlite.py"

REM Find python launcher (prefer 'py' then 'python')
where py >nul 2>&1
if %ERRORLEVEL%==0 (
  set "PYEXEC=py"
) else (
  where python >nul 2>&1
  if %ERRORLEVEL%==0 (
    set "PYEXEC=python"
  ) else (
    echo Python not found in PATH. Aborting.
    exit /b 3
  )
)

REM Build a timestamp for the log file using PowerShell
for /f "usebackq" %%t in (`powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"`) do set "TIMESTAMP=%%t"

REM Ensure output directory exists
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

REM Change to repository root (parent of this scripts folder)
pushd "%~dp0.."

echo Running export at %TIMESTAMP%
"%PYEXEC%" "%SCRIPT_REL%" --db-path "%DB_PATH%" --tables WorkHours,NcmTimes,Products,Orders --output-dir "%OUTPUT_DIR%" > "%OUTPUT_DIR%\export_%TIMESTAMP%.log" 2>&1
set "EXITCODE=%ERRORLEVEL%"

popd
exit /b %EXITCODE%

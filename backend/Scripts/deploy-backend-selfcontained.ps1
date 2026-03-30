<#
Publish backend as a self-contained build and prepare a start script.
Usage (PowerShell):
  .\deploy-backend-selfcontained.ps1 -PublishDir 'E:\MISCMFactoryService\backend' -Configuration Release -Runtime win-x64 -AspDotNetCorePort 5080

Notes:
- This publishes the project at ../backend/backend.csproj (relative to this script file).
- It will copy appsettings.json and workhour.db (if present) into the publish folder.
- It writes a simple start script `start-backend.ps1` into the publish folder which launches the exe.
- To run at system startup, create a Scheduled Task or register a Windows Service pointing at the start script or exe.
#>
param(
    [string]$PublishDir = 'E:\MISCMFactoryService\backend',
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [int]$AspDotNetCorePort = 5080,
    [string]$DbBackupRoot = 'E:\WorkhourDBBackups'
)

# Resolve project path (script located in repo/scripts)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projPath = Resolve-Path (Join-Path $scriptDir '..\backend.csproj') -ErrorAction Stop
$proj = $projPath.Path

Write-Host "Publishing project: $proj" -ForegroundColor Cyan
Write-Host "PublishDir: $PublishDir" -ForegroundColor Cyan

# Ensure publish directory exists
if (!(Test-Path -Path $PublishDir)) {
    New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
}

# CLEANUP: stop service and remove existing published files to avoid file-in-use errors
try {
    Write-Host "Performing publish-folder cleanup..." -ForegroundColor Cyan

    # Kill any running backend.exe processes (best-effort)
    $procs = Get-Process -Name 'backend' -ErrorAction SilentlyContinue
    if ($procs) {
        foreach ($p in $procs) {
            try {
                Write-Host ("Stopping process Id=" + $p.Id + " Name=" + $p.ProcessName)
                Stop-Process -Id $p.Id -Force -ErrorAction Stop
            } catch {
                Write-Warning ("Failed to stop process Id=$($p.Id)")
            }
        }
        Start-Sleep -Seconds 1
    }

    # Attempt to remove all files in the publish directory (keep the folder itself)
    try {
        Write-Host ("Preparing to remove contents of publish folder: " + $PublishDir)

        # Auto-backup DB files before cleanup to avoid accidental data loss.
        try {
            $backupStamp = Get-Date -Format 'yyyyMMdd_HHmmss'
            $backupDir = Join-Path $DbBackupRoot $backupStamp
            New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

            $dbFiles = Get-ChildItem -Path $PublishDir -Filter 'workhour.db*' -File -ErrorAction SilentlyContinue
            if ($dbFiles -and $dbFiles.Count -gt 0) {
                foreach ($dbf in $dbFiles) {
                    Copy-Item -Path $dbf.FullName -Destination (Join-Path $backupDir $dbf.Name) -Force
                    Write-Host "Backed up $($dbf.Name) -> $backupDir"
                }
            } else {
                Write-Host "No workhour.db* files found in publish folder to back up."
            }
        } catch {
            Write-Warning ("Failed to back up DB files to {0}: {1}" -f $DbBackupRoot, $_)
        }

        Write-Host ("Removing contents of publish folder: " + $PublishDir)
        Get-ChildItem -Path $PublishDir -Force | Remove-Item -Recurse -Force -ErrorAction Stop
    } catch {
        Write-Warning ("Failed to fully clean publish folder: $PublishDir")
    }
} catch {
    Write-Warning ("Cleanup step encountered an error")
}

# Run dotnet publish (self-contained)
$dotnetArgs = @('publish', $proj, '-c', $Configuration, '-r', $Runtime, '--self-contained', 'true', '-o', $PublishDir, '/p:PublishTrimmed=false')
$proc = Start-Process -FilePath 'dotnet' -ArgumentList $dotnetArgs -NoNewWindow -Wait -PassThru
if ($proc.ExitCode -ne 0) {
    Write-Error "dotnet publish failed with exit code $($proc.ExitCode)"
    exit $proc.ExitCode
}

# Copy appsettings.json and supporting files from backend folder if present
$backendDir = Resolve-Path (Join-Path $scriptDir '..\')
$filesToCopy = @('appsettings.json','appsettings.Development.json','MIProdCommInfo.json','ProductDefinitions.csv','workhour.db')
foreach ($f in $filesToCopy) {
    $src = Join-Path $backendDir $f
    if (Test-Path $src) {
        try {
            Copy-Item -Path $src -Destination $PublishDir -Force
            Write-Host "Copied $f to publish folder"
        } catch {
            Write-Warning ("Failed to copy {0}: {1}" -f $f, $_)
        }
    }
}

# RESTORE: If we backed up DB files earlier, restore the latest backup's workhour.db* files into the publish folder.
try {
    $parentDir = Split-Path -Parent $PublishDir
    $backupRoot = Join-Path $parentDir 'db-backups'
    if (Test-Path $backupRoot) {
        $latestBackup = Get-ChildItem -Path $backupRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
        if ($latestBackup) {
            $backupDbFiles = Get-ChildItem -Path $latestBackup.FullName -Filter 'workhour.db*' -File -ErrorAction SilentlyContinue
            if ($backupDbFiles -and $backupDbFiles.Count -gt 0) {
                foreach ($dbf in $backupDbFiles) {
                    try {
                        Copy-Item -Path $dbf.FullName -Destination (Join-Path $PublishDir $dbf.Name) -Force
                        Write-Host "Restored $($dbf.Name) -> $PublishDir"
                    } catch {
                        Write-Warning "Failed to restore $($dbf.Name) from backup: $_"
                    }
                }
            }
        }
    }
} catch {
    Write-Warning "Error while attempting to restore DB files from backups: $_"
}

# If no backup files were restored, ensure any DB files from the backend source folder are copied across (shm/wal/bak if present)
try {
    $expectedDbFiles = @('workhour.db','workhour.db-shm','workhour.db-wal','workhour.db.bak')
    foreach ($f in $expectedDbFiles) {
        $src = Join-Path $backendDir $f
        if (Test-Path $src) {
            try {
                Copy-Item -Path $src -Destination $PublishDir -Force
                Write-Host "Copied $f to publish folder (from backend source)"
            } catch {
                Write-Warning ("Failed to copy {0}: {1}" -f $f, $_)
            }
        }
    }
} catch {
    Write-Warning "Error while attempting to copy DB files from backend source: $_"
}

# Create a simple start script in the publish folder to run the exe
$exeName = 'backend.exe'
$startScriptPath = Join-Path $PublishDir 'start-backend.ps1'

# Use a double-quoted here-string so $AspDotNetCorePort is interpolated into the default
$startScriptContent = @"
param(
  [int]$Port = $AspDotNetCorePort
)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Push-Location -Path $scriptDir
# Set ASP.NET Core URLs environment variable for this process so the backend binds to the requested port
$env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
# Launch backend.exe from the publish folder; child process will inherit ASPNETCORE_URLS
Start-Process -FilePath (Join-Path '$scriptDir' '..\$exeName') -WorkingDirectory (Join-Path '$scriptDir' '..') -WindowStyle Hidden
Pop-Location
"@

Set-Content -Path $startScriptPath -Value $startScriptContent -Encoding UTF8
Write-Host ("Wrote start script: " + $startScriptPath)

Write-Host ("Publish complete. You can test by running:`n  PowerShell -ExecutionPolicy Bypass -File " + $startScriptPath) -ForegroundColor Green
Write-Host "To run at system startup, create a Scheduled Task or register a Windows Service that runs the start script or the exe." -ForegroundColor Yellow

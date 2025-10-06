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
    [int]$AspDotNetCorePort = 5080
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

    # Stop the service if it exists
    $existingSvc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($existingSvc) {
        if ($existingSvc.Status -ne 'Stopped') {
            Write-Host ("Stopping service " + $serviceName + " before cleanup...")
            try {
                Stop-Service -Name $serviceName -Force -ErrorAction Stop
                Start-Sleep -Seconds 1
            } catch {
                Write-Warning ("Failed to stop service $serviceName")
            }
        }
    }

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

# Temporarily commenting out Windows Service registration; can be done manually via RegisterToService.ps1

# # --- Windows Service registration (idempotent) ---
# $serviceName = 'MISCMBackend'
# $displayName = 'MISCM Factory Backend'
# $exePath = Join-Path $PublishDir $exeName   # e.g. E:\MISCMFactoryService\backend\backend.exe

# # Stop / remove existing service if present
# $svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
# if ($svc) {
#     if ($svc.Status -ne 'Stopped') {
#         Write-Host ("Stopping existing service " + $serviceName + "...")
#         Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
#         Start-Sleep -Seconds 1
#     }
#     Write-Host ("Deleting existing service " + $serviceName + "...")
#     sc.exe delete $serviceName | Out-Null
#     Start-Sleep -Seconds 1
# }

# # Create service (runs as LocalSystem). Use obj= '<DOMAIN\\User>' and password= '...' to run under a specific account.
# $displayNameArg = 'DisplayName= "' + $displayName + '"'

# # Build a binPath that launches the backend.exe directly with a --port argument.
# # sc.exe expects the entire binPath value to be quoted, e.g. binPath= "C:\path\backend.exe --port=5080"
# $fullCmd = $exePath + ' --port ' + $AspDotNetCorePort
# $binPathArg = 'binPath= "' + $fullCmd + '"'

# # Build argument lists and call sc.exe via Start-Process to avoid PowerShell parsing/quoting issues
# $createArgs = @('create', $serviceName, $binPathArg, 'start= auto', $displayNameArg, 'obj= LocalSystem')
# Write-Host ("Creating service with command: sc.exe " + ($createArgs -join ' '))
# $proc = Start-Process -FilePath 'sc.exe' -ArgumentList $createArgs -NoNewWindow -Wait -PassThru
# if ($proc.ExitCode -ne 0) {
#     Write-Warning "sc.exe create returned exit code $($proc.ExitCode)"
# }

# # Optional: set a friendly description
# $descArgs = @('description', $serviceName, 'Backend for MISCM Factory Service (self-contained)')
# Start-Process -FilePath 'sc.exe' -ArgumentList $descArgs -NoNewWindow -Wait | Out-Null

# # Optional: set simple failure/recovery policy (restart on first/second failure after 5s)
# # Format: actions= restart/<milliseconds>/restart/<milliseconds>/restart/<milliseconds>  (must include spaces after =)
# $failureArgs = @('failure', $serviceName, 'reset= 86400', 'actions= restart/5000/restart/5000/restart/5000')
# Start-Process -FilePath 'sc.exe' -ArgumentList $failureArgs -NoNewWindow -Wait | Out-Null

# # Start the service
# Write-Host ("Starting service " + $serviceName)
# Start-Service -Name $serviceName
# Start-Sleep -Seconds 2
# Get-Service -Name $serviceName | Select-Object Name, Status, StartType | Format-List

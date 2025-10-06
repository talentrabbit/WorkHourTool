param(
  [string]$ExePath = 'E:\MISCMFactoryService\backend\backend.exe',
  [string]$StartArgs    = '--port=5080',
  [int]$StartTimeoutSec = 5,
  [string]$LogPath = 'E:\MISCMFactoryService\backend\logs\watchdog-start.log'
)

$exeName   = [IO.Path]::GetFileName($ExePath)
$exeDir    = Split-Path -Parent $ExePath
$portRegex = '(^|\s)--port(=|\s)5080(\s|$)'

function Write-Log($msg) {
  $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
  $line  = "[$stamp] $msg"
  Write-Host $line
  try {
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $LogPath) -Force -ErrorAction SilentlyContinue
    Add-Content -LiteralPath $LogPath -Value $line
  } catch {}
}

# Find running instance of the exact exe path with --port=5080
$running = Get-CimInstance Win32_Process -Filter "Name='$exeName'" -ErrorAction SilentlyContinue |
  Where-Object {
    ($_.ExecutablePath -ieq $ExePath) -and ($_.CommandLine -match $portRegex)
  }

if ($running) {
  Write-Log "Backend already running. PID(s): $($running.ProcessId -join ', ')"
  exit 0
}

if (-not (Test-Path -LiteralPath $ExePath)) {
  Write-Log "Executable not found: $ExePath"
  exit 1
}

Write-Log "Starting backend: `"$ExePath`" $StartArgs (wd: $exeDir)"
Start-Process -FilePath $ExePath -ArgumentList $StartArgs -WorkingDirectory $exeDir -WindowStyle Hidden

Start-Sleep -Seconds $StartTimeoutSec

# Verify it started
$running = Get-CimInstance Win32_Process -Filter "Name='$exeName'" -ErrorAction SilentlyContinue |
  Where-Object {
    ($_.ExecutablePath -ieq $ExePath) -and ($_.CommandLine -match $portRegex)
  }

if ($running) {
  Write-Log "Backend started. PID(s): $($running.ProcessId -join ', ')"
  exit 0
} else {
  Write-Log "Backend failed to start."
  exit 2
}

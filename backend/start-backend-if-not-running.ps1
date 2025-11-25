param(
  [string]$ExePath = 'E:\MISCMFactoryService\backend\backend.exe',
  [string]$StartArgs    = '--port=5080',
  [int]$StartTimeoutSec = 5,
  [string]$LogPath = 'E:\MISCMFactoryService\backend\logs\watchdog-start.log',
  [switch]$VerifyPort,  # optional quick netstat port verification
  [string]$BackupDir = '',
  [int]$KeepBackups = 28
)

function Write-Log($msg) {
  $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
  $line  = "[$stamp] $msg"
  Write-Output $line
  try {
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $LogPath) -Force -ErrorAction SilentlyContinue
    Add-Content -LiteralPath $LogPath -Value $line
  } catch {}
}

# Basic quick check: process name
$exeName = [IO.Path]::GetFileNameWithoutExtension($ExePath)
try {
  $proc = Get-Process -Name $exeName -ErrorAction SilentlyContinue
} catch {
  $proc = $null
}

if ($proc) {
  Write-Log "Found running process by name '$exeName'. PID(s): $($proc.Id -join ', ') - exiting."
  exit 0
}

# Optional: quick netstat check for the port if caller requested it
if ($VerifyPort) {
  # extract port from StartArgs if possible
  $port = $null
  if ($StartArgs -match '--port=(\d+)') { $port = $matches[1] }
  elseif ($StartArgs -match '--port\s+(\d+)') { $port = $matches[1] }

  if ($port) {
    try {
      $netstat = (netstat -ano -p tcp) -split "`n"
      $listening = $netstat | Where-Object { $_ -match "LISTENING" -and $_ -match ":$port\b" }
      if ($listening) {
        Write-Log "Port $port appears to be LISTENING (netstat). Exiting."
        exit 0
      }
    } catch {
      # ignore netstat errors
    }
  }
}

if (-not (Test-Path -LiteralPath $ExePath)) {
  Write-Log "Executable not found: $ExePath"
  exit 1
}

# Start the backend detached. Use -WindowStyle Hidden and do NOT wait.
Write-Log "Starting backend: `"$ExePath`" $StartArgs"
Start-Process -FilePath $ExePath -ArgumentList $StartArgs -WorkingDirectory (Split-Path -Parent $ExePath) -WindowStyle Hidden -ErrorAction SilentlyContinue

# Sleep briefly to allow process to start and then exit. Task Scheduler will see the script complete.
Start-Sleep -Seconds $StartTimeoutSec

# Final quick check: process name again
try {
  $proc2 = Get-Process -Name $exeName -ErrorAction SilentlyContinue
} catch {
  $proc2 = $null
}

if ($proc2) {
  Write-Log "Backend started. PID(s): $($proc2.Id -join ', ')"
  exit 0
} else {
  Write-Log "Backend failed to start (no process with name '$exeName' detected)."
  exit 2
}

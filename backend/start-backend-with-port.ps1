Param(
    [int]$Port = 5080,
    [string]$ExePath = 'E:\MISCMFactoryService\backend\backend.exe'
)

# Validate exe path
if (-not (Test-Path $ExePath)) {
    Write-Error "Executable not found: $ExePath"
    exit 1
}

# Set environment variable for this process (inherited by launched exe)
# $env:ASPNETCORE_PORT = $Port.ToString()
# Write-Output "Set ASPNETCORE_PORT=$env:ASPNETCORE_PORT"

$workDir = Split-Path -Parent $ExePath
Write-Output "Starting backend from: $ExePath (working dir: $workDir)"

# Start the backend process with the port argument. Process is detached from the current shell.
try {
    $proc = Start-Process -FilePath $ExePath -ArgumentList "--port=$Port" -WorkingDirectory $workDir -WindowStyle Hidden -PassThru
    Write-Output "Started backend.exe (PID: $($proc.Id))."
} catch {
    Write-Error "Failed to start backend.exe: $_"
    exit 1
}

Write-Output "Use Get-Process -Id $($proc.Id) to inspect the process."

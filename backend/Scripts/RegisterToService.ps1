# --- Windows Service registration (idempotent) ---
$serviceName = 'MISCMBackend'
$displayName = 'MISCM Factory Backend'
$exePath = Join-Path $PublishDir $exeName   # e.g. E:\MISCMFactoryService\backend\backend.exe

# Stop / remove existing service if present
$svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne 'Stopped') {
        Write-Host "Stopping existing service $serviceName..."
        Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
    }
    Write-Host "Deleting existing service $serviceName..."
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 1
}

# Create service (runs as LocalSystem). Use obj= '<DOMAIN\User>' and password= '...' to run under a specific account.
$binPathQuoted = "\"$exePath\""
Write-Host "Creating service $serviceName -> $exePath"
sc.exe create $serviceName binPath= $binPathQuoted start= auto DisplayName= "\"$displayName\"" obj= LocalSystem | Out-Null

# Optional: set a friendly description
sc.exe description $serviceName "Backend for MISCM Factory Service (self-contained)" | Out-Null

# Optional: set simple failure/recovery policy (restart on first/second failure after 5s)
# Format: actions= restart/<milliseconds>/restart/<milliseconds>/restart/<milliseconds>  (must include spaces after =)
sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null

# Start the service
Write-Host "Starting service $serviceName"
Start-Service -Name $serviceName
Start-Sleep -Seconds 2
Get-Service -Name $serviceName | Select-Object Name, Status, StartType | Format-List
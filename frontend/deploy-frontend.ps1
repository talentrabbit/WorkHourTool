param(
  [string]$Source = (Join-Path $PSScriptRoot 'dist'),
  [string]$Destination = 'E:\MISCMFactoryService\backend\wwwroot',
  [switch]$Mirror
)

Write-Host "[deploy] Source: $Source"
Write-Host "[deploy] Destination: $Destination"

if (-not (Test-Path -LiteralPath $Source)) {
  Write-Error "[deploy] Source does not exist: $Source. Build the frontend (vite build) first."
  exit 1
}

if (-not (Test-Path -LiteralPath $Destination)) {
  Write-Host "[deploy] Creating destination directory..."
  New-Item -ItemType Directory -Path $Destination -Force | Out-Null
}

$robocopy = Get-Command robocopy -ErrorAction SilentlyContinue
if ($robocopy) {
  $roboArgs = @(
    $Source,
    $Destination,
    ($(if ($Mirror) { '/MIR' } else { '/E' })),
    '/COPY:DAT', '/DCOPY:DAT',
    '/R:2', '/W:2',
    '/NFL', '/NDL', '/NP', '/NJH', '/NJS'
  )
  Write-Host "[deploy] Running: robocopy $($roboArgs -join ' ')"
  & $robocopy @roboArgs
  $rc = $LASTEXITCODE
  if ($rc -le 7) {
    Write-Host "[deploy] Completed with robocopy exit code $rc (success)"
    exit 0
  } else {
    Write-Error "[deploy] Robocopy failed with exit code $rc"
    exit $rc
  }
} else {
  Write-Host "[deploy] Robocopy not found. Falling back to Copy-Item."
  if ($Mirror) {
    Write-Host "[deploy] Mirroring enabled: cleaning destination before copy..."
    Get-ChildItem -LiteralPath $Destination -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  }
  Copy-Item -LiteralPath (Join-Path $Source '*') -Destination $Destination -Recurse -Force
  Write-Host "[deploy] Completed with Copy-Item"
  exit 0
}

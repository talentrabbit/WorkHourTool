param(
  [string]$BackendPublishDir = 'E:\MISCMFactoryService\backend',
  [string]$BackendConfiguration = 'Release',
  [string]$BackendRuntime = 'win-x64',
  [int]$BackendPort = 5080,
  [string]$DbBackupRoot = 'E:\WorkhourDBBackups',
  [string]$FrontendSource = '',
  [string]$FrontendDestination = 'E:\MISCMFactoryService\backend\wwwroot',
  [switch]$Mirror
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendDeployScript = Join-Path $repoRoot 'backend\Scripts\deploy-backend-selfcontained.ps1'
$frontendDeployScript = Join-Path $repoRoot 'frontend\deploy-frontend.ps1'

if (-not (Test-Path -LiteralPath $backendDeployScript)) {
  Write-Error "[deploy-all] Backend deploy script not found: $backendDeployScript"
  exit 1
}

if (-not (Test-Path -LiteralPath $frontendDeployScript)) {
  Write-Error "[deploy-all] Frontend deploy script not found: $frontendDeployScript"
  exit 1
}

Write-Host '[deploy-all] Step 1/2: Deploy backend'
$backendArgs = @(
  '-ExecutionPolicy', 'Bypass',
  '-File', $backendDeployScript,
  '-PublishDir', $BackendPublishDir,
  '-Configuration', $BackendConfiguration,
  '-Runtime', $BackendRuntime,
  '-AspDotNetCorePort', $BackendPort,
  '-DbBackupRoot', $DbBackupRoot
)

$backendProc = Start-Process -FilePath 'powershell.exe' -ArgumentList $backendArgs -NoNewWindow -Wait -PassThru
if ($backendProc.ExitCode -ne 0) {
  Write-Error "[deploy-all] Backend deploy failed with exit code $($backendProc.ExitCode)"
  exit $backendProc.ExitCode
}

Write-Host '[deploy-all] Step 2/2: Deploy frontend'
$frontendArgs = @(
  '-ExecutionPolicy', 'Bypass',
  '-File', $frontendDeployScript,
  '-Destination', $FrontendDestination
)
if ($FrontendSource -and $FrontendSource.Trim().Length -gt 0) {
  $frontendArgs += @('-Source', $FrontendSource)
}
if ($Mirror) {
  $frontendArgs += '-Mirror'
}

$frontendProc = Start-Process -FilePath 'powershell.exe' -ArgumentList $frontendArgs -NoNewWindow -Wait -PassThru
if ($frontendProc.ExitCode -ne 0) {
  Write-Error "[deploy-all] Frontend deploy failed with exit code $($frontendProc.ExitCode)"
  exit $frontendProc.ExitCode
}

Write-Host '[deploy-all] Deployment complete (backend + frontend).'
exit 0

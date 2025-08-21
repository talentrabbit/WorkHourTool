# Clean up dev servers for WorkHourTool (frontend Vite, frontend preview, backend ASP.NET Core)
$ErrorActionPreference = 'SilentlyContinue'

$ports = @(5173, 4173, 5063)
foreach ($port in $ports) {
  try {
    $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($conns) {
      $pids = $conns | Select-Object -ExpandProperty OwningProcess -Unique
      foreach ($pid in $pids) {
        try {
          Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
          Write-Host "Killed PID $pid listening on port $port"
        } catch {}
      }
    } else {
      Write-Host "No listener on port $port"
    }
  } catch {}
}

# Also kill vite/node processes launched from this workspace
$workspace = $PSScriptRoot
try {
  $nodeProcs = Get-CimInstance Win32_Process | Where-Object {
    ($_.Name -in @('node.exe','node','vite','vite.exe')) -or ($_.CommandLine -match 'vite')
  }
  foreach ($p in $nodeProcs) {
    if ($p.CommandLine -and ($p.CommandLine -match [regex]::Escape($workspace))) {
      try {
        Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
        Write-Host "Killed $($p.Name) PID $($p.ProcessId)"
      } catch {}
    }
  }
} catch {}

# Kill dotnet processes for backend in this workspace
try {
  $dotnets = Get-CimInstance Win32_Process | Where-Object { $_.Name -in @('dotnet.exe','dotnet') }
  foreach ($d in $dotnets) {
    if ($d.CommandLine -and (
        $d.CommandLine -match [regex]::Escape($workspace) -or
        $d.CommandLine -match 'backend.dll' -or
        $d.CommandLine -match 'backend.csproj')) {
      try {
        Stop-Process -Id $d.ProcessId -Force -ErrorAction SilentlyContinue
        Write-Host "Killed dotnet PID $($d.ProcessId)"
      } catch {}
    }
  }
} catch {}

Write-Host "Cleanup complete."

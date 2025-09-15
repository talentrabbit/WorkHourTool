# Basic API smoke tests for backend (PowerShell)
$baseUri = "http://localhost:5063/api/workhours"

function PostJson($uri, $obj) {
    try {
        $json = $obj | ConvertTo-Json -Depth 6
        Write-Host "POST $uri`n$json`n" -ForegroundColor Cyan
        $res = Invoke-RestMethod -Uri $uri -Method Post -Body $json -ContentType 'application/json; charset=utf-8' -ErrorAction Stop
        return $res
    } catch {
        Write-Host "ERROR POST $uri: $_" -ForegroundColor Red
        return $null
    }
}

function GetJson($uri) {
    try {
        Write-Host "GET $uri" -ForegroundColor Cyan
        $res = Invoke-RestMethod -Uri $uri -Method Get -ErrorAction Stop
        return $res
    } catch {
        Write-Host "ERROR GET $uri: $_" -ForegroundColor Red
        return $null
    }
}

Write-Host "=== 1) Start a WorkSession (start-session) ===" -ForegroundColor Green
$startReq = @{
    WorkHourId = $null
    WorkerName = "TestWorker"
    SerialNo = "SN-TEST-001"
    ProcessName = "Assembly"
    ElapsedSeconds = 0
    ActiveClock = "work"
    MetadataJson = @{ ncmRows = @() } | ConvertTo-Json -Depth 5
    State = "Working"
}
$startRes = PostJson "$baseUri/start-session" $startReq
if ($startRes -ne $null) { $sessionId = $startRes.sessionId; Write-Host "Started session: $sessionId" -ForegroundColor Yellow } else { Write-Host "Failed to start session" -ForegroundColor Red; exit 1 }

Start-Sleep -Seconds 1

Write-Host "=== 2) Send heartbeat ===" -ForegroundColor Green
$hbReq = @{
    SessionId = $sessionId
    ElapsedSeconds = 10
    ActiveClock = "work"
    MetadataJson = @{ ncmCount = 0 } | ConvertTo-Json -Depth 5
    State = "Working"
}
$hbRes = PostJson "$baseUri/session-heartbeat" $hbReq
Write-Host ("Heartbeat response: " + ($hbRes | ConvertTo-Json -Depth 4))

Start-Sleep -Seconds 1

Write-Host "=== 3) Get session ===" -ForegroundColor Green
$getRes = GetJson "$baseUri/session/$sessionId"
Write-Host ("Session object: " + ($getRes | ConvertTo-Json -Depth 6))

Start-Sleep -Seconds 1

Write-Host "=== 4) Complete session ===" -ForegroundColor Green
$completeReq = @{
    SessionId = $sessionId
    ElapsedSeconds = 12
}
$completeRes = PostJson "$baseUri/complete-session" $completeReq
Write-Host ("Complete response: " + ($completeRes | ConvertTo-Json -Depth 4))

Start-Sleep -Seconds 1

Write-Host "=== 5) Verify session state after completion ===" -ForegroundColor Green
$getAfter = GetJson "$baseUri/session/$sessionId"
Write-Host ("Session after complete: " + ($getAfter | ConvertTo-Json -Depth 6))

Write-Host "=== 6) Basic WorkHour create + complete flow (if product exists) ===" -ForegroundColor Green
# Attempt to create a WorkHour for an existing SerialNo; adjust SerialNo as needed for your DB
$workReq = @{
    SerialNo = "SN-001"
    WorkerName = "TestWorker"
    MainTime = 3600
    IssueTime = 0
}
try {
    $whRes = PostJson "$baseUri" $workReq
    Write-Host ("WorkHour create response: " + ($whRes | ConvertTo-Json -Depth 4))
} catch {}

Write-Host "=== Tests completed ===" -ForegroundColor Green
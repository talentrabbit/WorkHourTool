# Basic API smoke tests for backend (PowerShell)
# $baseUri = "http://localhost:5080/api/workhours"
$baseUri = "http://localhost:5063/api/workhours"

function PostJson($uri, $obj) {
    try {
        $json = $obj | ConvertTo-Json -Depth 6
        Write-Host "POST $uri`n$json`n" -ForegroundColor Cyan
        $res = Invoke-RestMethod -Uri $uri -Method Post -Body $json -ContentType 'application/json; charset=utf-8' -ErrorAction Stop
        return $res
    } catch {
        Write-Host "ERROR POST $uri`n$json`n" -ForegroundColor Red
        return $null
    }
}

function GetJson($uri) {
    try {
        Write-Host "GET $uri" -ForegroundColor Cyan
        $res = Invoke-RestMethod -Uri $uri -Method Get -ErrorAction Stop
        return $res
    } catch {
        Write-Host "ERROR GET $uri" -ForegroundColor Red
        return $null
    }
}

# New test: Fetch all product states
Write-Host "=== 1) Get all product states (all-product-states) ===" -ForegroundColor Green
$allProdStates = GetJson "$baseUri/all-product-states"
if ($allProdStates -ne $null) {
    Write-Host ("All product states: " + ($allProdStates | ConvertTo-Json -Depth 6)) -ForegroundColor Yellow
} else {
    Write-Host "Failed to fetch all product states" -ForegroundColor Red
}

Write-Host "=== Tests completed ===" -ForegroundColor Green
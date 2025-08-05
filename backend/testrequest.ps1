
$baseUri = "http://localhost:5063/api/workhours"

# 1. Test POST /api/workhours
Write-Host "Testing POST /api/workhours..."
$body = @{
    workerName = "John"
    mainTime = 3600
    issueTime = 120
    serialNo = "SN-001"
} | ConvertTo-Json
$response = Invoke-RestMethod -Uri $baseUri -Method Post -Body $body -ContentType "application/json; charset=utf-8"
$response

# 2. Test GET /api/workhours/product-status/{serialNo}
Write-Host "Testing GET /api/workhours/product-status/{serialNo}..."
$serialNo = "SN-001"
$response = Invoke-RestMethod -Uri "$baseUri/product-status/$serialNo" -Method Get
$response

# 3. Test GET /api/workhours/all-product-states
Write-Host "Testing GET /api/workhours/all-product-states..."
$response = Invoke-RestMethod -Uri "$baseUri/all-product-states" -Method Get
$response
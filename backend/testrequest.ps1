$uri = "http://localhost:5063/api/workhours"
$body = @{
    workerName = "John"
    mainTime = 3600
    issueTime = 120
} | ConvertTo-Json
$response = Invoke-RestMethod -Uri $uri -Method Post -Body $body -ContentType "application/json; charset=utf-8"
$response
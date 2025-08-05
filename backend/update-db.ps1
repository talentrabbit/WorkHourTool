# PowerShell script to add migration and update database
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$migrationName = "DBChange_$timestamp"

Write-Host "Adding migration: $migrationName"
dotnet ef migrations add $migrationName --project backend.csproj

Write-Host "Updating database..."
dotnet ef database update

Write-Host "Done."

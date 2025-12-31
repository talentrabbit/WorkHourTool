# Backend

ASP.NET Core Web API providing the WorkHour Tool services.

## Running locally

```bash
cd backend
dotnet restore
dotnet run
```

## Version file

The `GET /api/WorkHours/version` endpoint reads `backend/Version.json` at runtime. Update the `backend` (and/or `version`) fields in that file during deployment to broadcast a new backend release number without recompiling.

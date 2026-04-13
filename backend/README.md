# Backend

ASP.NET Core Web API providing the WorkHour Tool services.

## Technical architecture

- **Framework**: ASP.NET Core Web API
- **Persistence**: Entity Framework Core (`Microsoft.EntityFrameworkCore`) with SQLite by default
- **Logging**: Serilog (rolling file + console)
- **Auth**: Negotiate/Windows auth (with dev/test overrides)

### Core runtime flow

1. `Program.cs` configures host, logging, authentication/CORS, static-file SPA fallback, and DI registrations.
2. `AppDbContext` is registered via `AddDbContextPool` and reads DB path/provider from configuration (`DbPath`, `DbProvider`).
3. API routes in controllers (mainly `WorkHoursController`) process CRUD and timer/session calls.
4. `WorkSessionManager` + `WorkSessionCleanupService` handle dynamic in-memory task/session behavior and persistence.

### Key backend files

- `Data/AppDbContext.cs`
	- EF Core wrapper around database access.
	- Defines `DbSet` for `Product`, `WorkHour`, `NcmTime`, `Order`, `User`, `WorkSession`.
	- Configures entity relationships, indexes, defaults, and schema details.

- `Controllers/WorkHoursController.cs`
	- Main API entry point for work-hour/task operations.
	- Handles start/pause/heartbeat/complete session calls and workhour/product/ncm endpoints.
	- Calls `WorkSessionManager` for dynamic session state updates.

- `Services/WorkSessionManager.cs`
	- In-memory session orchestrator for active timers.
	- Owns `ManagedSession` timers, heartbeat updates, pause/complete operations.
	- Periodically persists snapshots into `WorkSession` table.

- `Services/WorkSessionCleanupService.cs`
	- Hosted background service registered in `Program.cs`.
	- Reads `WorkSessionCleanup` config (`Enabled`, `RestoreOnStartup`, `RestoreWindowDays`, `ScheduleTime`) from `appsettings.json`.
	- Restores sessions on startup and performs scheduled end-of-day cleanup.

### Task/timer dynamic logic (important)

- Worker starts/pauses/resumes timer from frontend.
- API route in `WorkHoursController` receives request.
- Controller calls `WorkSessionManager` methods (`StartOrResume`, `Heartbeat`, `PauseSession`, `CompleteSession`).
- Session state is updated in memory first, then persisted back to DB snapshots and final `WorkHour`/`NcmTime` rows as needed.

### Configuration keys (appsettings)

- `DbProvider`, `DbPath`
- `WorkSessionCleanup:Enabled`
- `WorkSessionCleanup:RestoreOnStartup`
- `WorkSessionCleanup:RestoreWindowDays`
- `WorkSessionCleanup:ScheduleTime`

## Running locally

```bash
cd backend
dotnet restore
dotnet run
```

## Version file

The `GET /api/WorkHours/version` endpoint reads `backend/Version.json` at runtime. Update the `backend` (and/or `version`) fields in that file during deployment to broadcast a new backend release number without recompiling.

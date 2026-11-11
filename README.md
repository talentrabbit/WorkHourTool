# WorkHourapp

A web application for manufacturing departments to track exact working hours for each worker. 

## Structure
- **frontend/**: Vue 3 + Vite single-page app using Vue Router, Axios, and VXETable. `main.js` creates the app, registers routes, and configures API calls to same-origin `/api`. `App.vue` owns the portal shell, navigation, sign-in state, and role context. Routed views cover planning, worker timer workflow, NCM time, product registration, maintenance, orders, and the main entry dashboard. `WorkHourTool.vue` coordinates `WorkSeat.vue` and `TimerClock.vue` for start/pause/resume/submit behavior.
- **backend/**: ASP.NET Core Web API using controllers, EF Core, Serilog, Negotiate authentication, and SQLite or SQL Server. `Program.cs` configures dependency injection, CORS, authentication, static SPA hosting, and auth helper endpoints. `WorkHoursController` exposes product, planning, work hour, NCM, and session APIs; `OrdersController` handles order CRUD. `AppDbContext` maps products, work hours, NCM records, users, orders, and work sessions. `WorkSessionManager` maintains live timer state, while `WorkSessionCleanupService` restores and expires sessions on schedule.

## Usage
- Workers start/pause/resume/end their workday using the frontend.
- Data is sent to the backend and archived in a database.

## Setup
See each folder for setup instructions.

## User administration

- Administrators see an **Admin** navigation link at `/admin`, with a searchable user directory and add/remove actions.
- Adding a user requires only full name and at least one role; GID and email are optional. Select any combination of **Administrator**, **ProductionManager**, **ProcessEngineer**, and **Worker**.
- Roles are stored as canonical comma-separated values in the existing `Users.Role` column. No schema migration is required. Newly assigned roles are loaded on the next sign-in/current-user refresh.
- The backend exposes `GET /api/users`, `GET /api/users/{id}`, `POST /api/users`, and `DELETE /api/users/{id}`. All require a server-authenticated account whose matching database user has the Administrator role. Manual name selection, local browser role values, and `X-Dev-User` do not authorize these APIs.
- Ensure the existing administrator's GID matches their Windows account (or their authenticated identity matches the stored full name/email). This page does not bootstrap the first administrator. Duplicate account IDs/email addresses, self-deletion, and deleting the last administrator are rejected.
- Removing a directory user does not remove historical work-hour or NCM records. Request examples are in [backend/users.http](backend/users.http).
- During Vite development, Admin requests use credentialed direct access to `http://<frontend-host>:5063` to avoid connection-based Windows authentication through the Vite proxy. Set `VITE_ADMIN_API_ORIGIN` to the actual development backend origin if different (for example `http://shai571a:5000`), then restart Vite. Its origin must be allowed by backend CORS. Production uses same-origin requests. If Windows sign-in is blocked, use **Open backend authentication**, authenticate with an administrator Windows account, and return to **Refresh**; browser intranet/Negotiate settings may need administrator configuration.

## Version identifiers

Both the frontend and backend display static version numbers that are managed outside of the code build. Update these files during deployment as needed:

| Component | File | Notes |
| --- | --- | --- |
| Frontend | `frontend/public/frontend-version.xml` | The Vue app fetches this XML at runtime and displays the `<version>` value in the header. No rebuild required—just update the file on disk or with your deployment pipeline. |
| Backend | `backend/Version.json` | The `GET /api/WorkHours/version` endpoint reads this JSON. Update `backend`/`version` fields to broadcast a new backend version. |

If either file is missing or malformed the UI/API will fall back to blank values, so make sure your release process writes the desired version strings before announcing a deployment.

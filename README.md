# WorkHourapp

A web application for manufacturing departments to track exact working hours for each worker. 

## Structure
- **frontend/**: Vue 3 + Vite app for UI and timer controls
- **backend/**: ASP.NET Core Web API for receiving and storing work session data

## Usage
- Workers start/pause/resume/end their workday using the frontend.
- Data is sent to the backend and archived in a database.

## Setup
See each folder for setup instructions.

## Version identifiers

Both the frontend and backend display static version numbers that are managed outside of the code build. Update these files during deployment as needed:

| Component | File | Notes |
| --- | --- | --- |
| Frontend | `frontend/public/frontend-version.xml` | The Vue app fetches this XML at runtime and displays the `<version>` value in the header. No rebuild required—just update the file on disk or with your deployment pipeline. |
| Backend | `backend/Version.json` | The `GET /api/WorkHours/version` endpoint reads this JSON. Update `backend`/`version` fields to broadcast a new backend version. |

If either file is missing or malformed the UI/API will fall back to blank values, so make sure your release process writes the desired version strings before announcing a deployment.

# Frontend

This folder hosts the Vue 3 + Vite UI for the WorkHour Tool. The app uses `<script setup>` SFCs and Vite for bundling.

## Technical architecture

- **Framework**: Vue 3 (Composition API + `<script setup>`)
- **Build tool**: Vite
- **Routing**: Vue Router (`createWebHistory`)
- **HTTP**: Axios (`/api/*` relative base URL)
- **Data-grid**: `vxe-table` for editable maintenance pages

### Runtime flow (high level)

1. `src/main.js` bootstraps Vue app, configures router, axios defaults, and registers `vxe-table`.
2. `src/App.vue` is the application shell:
	 - Header + nav + footer
	 - Role-based route visibility
	 - Sign-in/sign-out overlay logic
	 - Frontend/backend version display
3. Route components under `src/components` implement domain pages (planning, worker timer, maintenance, products, ncm, orders).

## Vue file structure summary

### Entry and global files

- `src/main.js`
	- Defines route mapping:
		- `/` / `/main` → `Main.vue`
		- `/worker` → `WorkHourTool.vue`
		- `/planning` → `Planning.vue`
		- `/product-register` → `ProductRegister.vue`
		- `/maintenance` → `WorkHourMaintenance.vue`
		- `/ncm` → `Ncm.vue`
		- `/orders` → `OrderInfo.vue`
	- Adds optional dev-user header (`X-Dev-User`) support.

- `src/App.vue`
	- Global portal layout and nav.
	- Reads current user/role from backend and localStorage.
	- Provides shared state (`username`, `userRole`, `isCountingTimerActive`) to child components.
	- Loads FE version from `public/frontend-version.xml` and BE version from `/api/WorkHours/version`.

- `src/style.css`
	- Global typography and shared style tokens.
	- Shared button styles (e.g., `.switch-btn`) used by multiple pages.

### Feature components (core business pages)

- `components/Main.vue`
	- Landing/hero page and role-based quick links.

- `components/WorkHourTool.vue`
	- Worker workflow container.
	- Coordinates seat/task/timer workflow.

- `components/TimerClock.vue`
	- Real-time timer state (`active`/`paused`).
	- Sends heartbeat/start/complete calls to backend session APIs.
	- Handles NCM metadata capture and submit interactions.

- `components/Planning.vue`
	- Planning/task assignment and schedule-oriented operations.

- `components/WorkHourMaintenance.vue`
	- Editable `WorkHours` maintenance grid with filters, paging, save/delete.

- `components/ProductRegister.vue`
	- New product registration form + products maintenance table editor.

- `components/Ncm.vue`
	- NCM list/editor management, grouping/filtering/paging and actions.

- `components/OrderInfo.vue`
	- Order list/maintenance view integrated with product serials.

### Supporting components

- `components/WorkSeat.vue`, `components/CustomerInfo.vue`
	- Reusable UI blocks used in worker/planning flows.

- `components/Kanban.vue`
	- Placeholder/under-construction view.

## Development

```bash
npm install
npm run dev
```

## Production build

```bash
npm run build
```

## Version number

The header shows "FE: <version>" by fetching `public/frontend-version.xml` at runtime. To change the number (without rebuilding):

1. Edit `frontend/public/frontend-version.xml`.
2. Update the `<version>` element with the release identifier (for example `2552.301`).
3. Deploy the updated XML alongside the built assets. The app will refetch it on the next load.

If the XML is missing or malformed the UI falls back to blank/N/A, so make sure CI/CD always publishes the file.

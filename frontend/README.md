# Frontend

This folder hosts the Vue 3 + Vite UI for the WorkHour Tool. The app uses `<script setup>` SFCs and Vite for bundling.

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

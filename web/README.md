# React Web Application (Admin / Approval / Monitoring)

Purpose: administrative, operational, and Agentic AI monitoring/approval — not where
end users (donors/hospital field staff) transact; that's Flutter's job.

## Layout
- `src/features/auth/` — login, protected-route wrapper, role-based nav.
- `src/features/donors/` — Student 1's admin view of the donor registry.
- `src/features/requests/` — Student 2's hospital request triage queue.
- `src/features/inventory/` — Student 3's stock dashboard + expiry alerts.
- `src/features/camps/` — Student 4's camp scheduling admin.
- `src/features/agent-workflows/` — shared. Execution monitor + approve/reject/revise UI.
- `src/api/client.ts` — one typed API client every feature imports (build this first).

## Sprint 0/1 TODO
1. `npm create vite@latest web -- --template react-ts` (or CRA, record choice in ADR).
2. Add React Router, and the chosen state-management library (Redux Toolkit / Zustand — ADR-02).
3. Build `src/api/client.ts` with a shared fetch wrapper (JWT header injection, base URL from env).

## Frontend review and local checks

The redesigned pages live in `src/pages/`, with shared layout, icons and theme control in `src/components/`. Theme tokens and responsive styles are in `src/styles.css`. Photos and licensed fonts are bundled under `public/`.

Run `npm run build` and `npm test` for normal checks. `npm run test:browser` starts an isolated Vite server on port 5174 and reviews all pages against mock API fixtures. It uses installed Edge on Windows; on other platforms run `npx playwright install chromium` first. `PLAYWRIGHT_CHANNEL` overrides that default.

See [the redesign plan](../docs/frontend-redesign-plan.md) and [preview gallery](../docs/frontend-previews/README.md).

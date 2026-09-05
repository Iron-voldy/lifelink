# LifeLink - commands to run the project (Windows / PowerShell)

Prerequisites found on this machine: .NET 8, Python 3.13, Node 22, PostgreSQL (running), Flutter at `D:\flutter\flutter`. Docker is NOT installed, so run each service natively.

## 1. Agent service (Python, port 8000)
```powershell
cd "D:\SEF project\agentic-ai"
.\.venv\Scripts\Activate.ps1
pip install -e .
$env:LIFELINK_ENVIRONMENT = "development"   # required: placeholder keys are rejected otherwise
$env:LIFELINK_INTERNAL_API_KEY = "replace-with-a-local-development-key"
$env:LIFELINK_MODEL_PROVIDER = "deterministic"
uvicorn lifelink_agents.main:app --host 0.0.0.0 --port 8000
```

## 2. Backend API (.NET, http://localhost:5224)
Needs Postgres with database `lifelink`, user `lifelink`, password `lifelink_local_only` (see appsettings.json). One-time setup as the postgres superuser:
```powershell
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -h localhost -U postgres -c "CREATE USER lifelink WITH PASSWORD 'lifelink_local_only';" -c "CREATE DATABASE lifelink OWNER lifelink;"
```
```powershell
cd "D:\SEF project\backend"
dotnet restore
dotnet test
$env:Database__MigrateOnStartup = "true"
$env:Bootstrap__Enabled = "true"
$env:Bootstrap__AdminEmail = "admin@lifelink.local"
$env:Bootstrap__AdminPassword = "Admin!ChangeMe12345"
$env:DemoData__Enabled = "true"          # optional: seed demo data (runs once, never in Production)
$env:DemoData__Password = "Demo!Pass12345"
$env:Cors__AllowedOrigins__0 = "http://localhost:5173"
$env:Cors__AllowedOrigins__1 = "http://localhost:5300"   # Flutter web (flutter run -d chrome --web-port 5300)
dotnet run --project LifeLink.Api --launch-profile http
```
Swagger: http://localhost:5224/swagger

## 3. Web (React, http://localhost:5173)
```powershell
cd "D:\SEF project\web"
npm install
$env:VITE_API_BASE_URL = "http://localhost:5224"
npm run dev
```

## 4. Mobile (Flutter)
```powershell
$env:Path = "D:\flutter\flutter\bin;" + $env:Path
cd "D:\SEF project\mobile"
flutter pub get
flutter analyze
flutter test
flutter emulators --launch <emulator_id>   # or plug in a device
flutter run --dart-define=LIFELINK_API_BASE_URL=http://10.0.2.2:5224 --dart-define=LIFELINK_FIREBASE_ENABLED=false
```
Physical device: replace `10.0.2.2` with this PC's LAN IP and allow port 5224 through the firewall.

## 5. With Docker (only if you install it)
```powershell
cd "D:\SEF project"
copy .env.example .env   # then edit the passwords
docker compose up --build
```
Web: http://localhost:8080, API: http://localhost:5080.

## Demo data and logins
With `DemoData__Enabled=true` the API seeds, once, 10+ rows for every table: users for all 4 roles, hospitals and staff,
donors (eligible / temporarily ineligible / pending / permanently ineligible), donation history, blood centres,
inventory lots (fresh, near-expiry, expired, quarantined), camps in every state with booked/checked-in/no-show slots,
blood requests in every status, agent workflows with steps and approvals, reservations, dispatches, notifications and devices.

All demo accounts use the password `Demo!Pass12345`.

| Role | Logins |
|---|---|
| BloodBankAdmin | `admin01@demo.lifelink.local` ... `admin10@demo.lifelink.local` (plus bootstrap `admin@lifelink.local`) |
| CampCoordinator | `coordinator01@demo.lifelink.local` ... `coordinator10@...` (each organises one camp) |
| HospitalRequester | `hospital01@demo.lifelink.local` ... `hospital12@...` (hospital09 = pending hospital, hospital10 = suspended) |
| Donor | `donor01@demo.lifelink.local` ... `donor20@...` (01-12 mostly eligible, 13-17 temporarily ineligible, 18-19 pending, 20 inactive/over age) |

Try it: sign in as `admin01`, open agent workflows and approve one of the 3 **PendingApproval** workflows. It reserves stock, dispatches it and updates the request.

To reseed from scratch, drop and recreate the database, then start the API again:
```powershell
& "C:\Program Files\PostgreSQL\16\bin\psql.exe" -h localhost -U postgres -c "DROP DATABASE lifelink WITH (FORCE);" -c "CREATE DATABASE lifelink OWNER lifelink;"
```

## 6. Hosted on the Hostinger VPS (Docker)
| | URL |
|---|---|
| Web app | https://lifelink.72-62-255-113.sslip.io |
| API | https://lifelink-api.72-62-255-113.sslip.io (health: `/health`; Swagger only in Development) |

The web container proxies same-origin `/api/*` to the API container, so the hosted web app needs no CORS entry.
The mobile app defaults to the hosted API; pass `--dart-define=LIFELINK_API_BASE_URL=...` to use a local one.

On the VPS the stack lives in `/opt/lifelink` (Postgres + agent + API + web in Docker; API on 127.0.0.1:5090, web on 127.0.0.1:5091,
exposed via the nginx sites `lifelink-api` and `lifelink-web` with Let's Encrypt certificates). Postgres data is kept in the
`lifelink_lifelink-postgres` Docker volume and survives redeploys. Secrets are generated on first deploy into `/opt/lifelink/deploy/vps/.env` (mode 600).

Show the admin and demo-account passwords (run on the VPS):
```bash
grep -E '^(BOOTSTRAP_ADMIN_PASSWORD|DEMO_DATA_PASSWORD)=' /opt/lifelink/deploy/vps/.env
```
Logins are the same as the local demo table above, but with the VPS `DEMO_DATA_PASSWORD`; the bootstrap admin is `admin@lifelink.local`.

Redeploy after code changes (from this PC, Git Bash):
```bash
cd "/d/SEF project"
tar --exclude=bin --exclude=obj --exclude=.venv --exclude=__pycache__ --exclude=.pytest_cache --exclude=node_modules --exclude=dist --exclude=.env -czf - backend agentic-ai web deploy/vps \
  | ssh root@72.62.255.113 'cd /opt/lifelink && tar -xzf - && bash deploy/vps/deploy.sh'
```
Point clients at the hosted API:
```powershell
flutter run --dart-define=LIFELINK_API_BASE_URL=https://lifelink-api.72-62-255-113.sslip.io --dart-define=LIFELINK_FIREBASE_ENABLED=false
$env:VITE_API_BASE_URL = "https://lifelink-api.72-62-255-113.sslip.io"; npm run dev
```
Browser clients on other origins (e.g. local dev) must be listed in CORS: edit `CORS_ORIGIN_0` / `CORS_ORIGIN_1` in the VPS `.env` (defaults: localhost:5173 and :5300), then `docker compose up -d` in `/opt/lifelink/deploy/vps`.

Database on the VPS:
```bash
docker exec -it lifelink-postgres-1 psql -U lifelink -d lifelink          # SQL shell (schema: lifelink)
docker exec lifelink-postgres-1 pg_dump -U lifelink lifelink > lifelink-backup.sql   # backup
```
To reseed from scratch: `cd /opt/lifelink/deploy/vps && docker compose down && docker volume rm lifelink_lifelink-postgres && bash deploy.sh`

# LifeLink

LifeLink is a blood and emergency-resource coordination platform for donors, hospital requesters, and blood-bank administrators. It combines deterministic ASP.NET Core business rules with an auditable, human-approved LangGraph workflow.

## Applications

| Application | Purpose | Technology |
|---|---|---|
| API | Authentication, business authority, persistence, third-party boundary | ASP.NET Core 8, EF Core, PostgreSQL |
| Agent service | Planning, domain analysis, dispatch proposals, safety validation | Python, FastAPI, LangGraph |
| Admin web | Triage, inventory, camps, reports, AI approval | React, TypeScript, Vite |
| Field mobile | Donor profile/camps and hospital request workflows | Flutter, Riverpod |

The web and mobile clients call only the API. The API owns database access, privileged integrations, deterministic validation, and approval side effects. No raw chain-of-thought is stored.

## Local development

Copy `.env.example` to `.env`, replace its development secrets, then start the complete browser stack:

```text
docker compose up --build
```

Services become available in dependency order: PostgreSQL → agent → API → web.

- Admin web: `http://localhost:8080`
- API and OpenAPI in Development: `http://localhost:5080`
- API health: `http://localhost:5080/health`
- Agent health inside the stack: `http://agent:8000/health`

The API applies committed EF Core migrations when the Compose stack starts. Flutter runs separately because it targets an emulator or device:

```text
cd mobile
flutter create --project-name lifelink_mobile --platforms android .
flutter pub get
flutter run --dart-define=LIFELINK_API_BASE_URL=http://10.0.2.2:5080
```

Use the host machine's LAN address instead of `10.0.2.2` on a physical device.

## Run services without containers

Start PostgreSQL, then run:

```text
dotnet run --project backend/LifeLink.Api
python -m uvicorn lifelink_agents.main:app --app-dir agentic-ai --port 8000
npm --prefix web run dev
```

Configuration templates live in the root and each application folder. Never commit populated secrets, Firebase credentials, access tokens, or real patient information.

For real push delivery, set `FIREBASE_ENABLED=true`, provide the Firebase project ID, and place the base64-encoded service-account JSON in `FIREBASE_SERVICE_ACCOUNT_JSON_BASE64`. The credential stays in the API environment; mobile clients receive through the Firebase SDK and never hold privileged service credentials. When Firebase is disabled, the backend uses an auditable local logging provider.

## Verification

```text
dotnet test backend/LifeLink.sln --configuration Release
python -m pytest agentic-ai/tests -q
npm --prefix web test
npm --prefix web run build
flutter analyze mobile
flutter test mobile
```

GitHub Actions also builds the container stack and produces downloadable web, migration-script, and Android APK artifacts.

## Documentation

- [Complete build plan](docs/complete-build-plan.md)
- [Reviewed ER model](docs/er-diagram.md)
- [Architecture decisions](docs/adr/README.md)
- [Full project interpretation](LifeLink-Project-Documentation.md)

The assignment PDF remains the authoritative source for grading and submission mechanics. Synthetic data must be used for development and demonstrations.

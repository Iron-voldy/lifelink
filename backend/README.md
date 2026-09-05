# Backend — ASP.NET Core Web API

## Layout
- `LifeLink.Api/` — Controllers, DTOs, Program.cs/appsettings, Middleware.
- `LifeLink.Application/` — service layer, one folder per component (business rules live here).
- `LifeLink.Domain/` — entities, enums, shared value objects (from `docs/er-diagram.md`).
- `LifeLink.Infrastructure/` — EF Core `DbContext`, Migrations, repositories.
- `LifeLink.Tests/` — xUnit, mirrors `Application/` per student.

## Sprint 0 TODO
1. `dotnet new sln -n LifeLink` at this level, then `dotnet new webapi/classlib/xunit` for each project above and add them to the sln.
2. Add EF Core + `Npgsql.EntityFrameworkCore.PostgreSQL` to `LifeLink.Infrastructure`.
3. Model entities from `docs/er-diagram.md` in `LifeLink.Domain/Entities/`.
4. First migration + local Postgres connection string in `appsettings.Development.json` (gitignored).

## Rule
React and Flutter only ever call this API — never the Agentic AI service or third-party
services directly (spec §2 "Mandatory backend rule").

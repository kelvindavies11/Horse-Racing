# British Horse Racing Data Project

An in-progress, local-first .NET application for collecting and managing British horse-racing data. The repository starts with a Clean Architecture foundation, an ASP.NET Core API, and PostgreSQL persistence through Entity Framework Core.

The target data flow is:

```text
British Horseracing Authority website
                |
                v
       Local scraping jobs
                |
                v
           Raw layer
                |
                v
         Created layer
           /       \
          v         v
Local domain job   Local read API
          |               |
          v               v
     Domain objects   React website
                      - view data
                      - future predictions
```

Raw ingestion and promotion into the created layer are audited. Created records retain source provenance and `FirstObserved` / `LastObserved` timestamps so the application can explain where a record came from, when it was first identified, and when it was most recently seen. A straightforward, locally hosted React website sits over the data through the local API. Its initial purpose is to browse the collected data, with prediction views planned for the future.

## Solution structure

```text
src/
  HorseRacing.Domain/          Enterprise rules and entities
  HorseRacing.Application/     Use cases and persistence abstractions
  HorseRacing.Infrastructure/  EF Core, PostgreSQL, repositories, migrations
  HorseRacing.Api/             HTTP host and endpoints
tests/
  HorseRacing.Domain.UnitTests/
docs/
  ARCHITECTURE.md
  DATABASE.md
  DEVELOPMENT-WORKFLOW.md
  DOMAIN-MODEL.md
  DATA-DICTIONARY.md
  SOURCE-LINKS.md
  FEATURES.md
  PROJECT-STATUS.md
  sql/DOMAIN-SCHEMA.sql
```

Dependencies point inwards: `Api -> Infrastructure/Application -> Domain`. The Domain project has no framework or database dependency.

## Local development

Prerequisites:

- .NET 10 SDK
- Docker Desktop or another PostgreSQL 17 instance

Start PostgreSQL:

```powershell
docker compose up -d postgres
```

When the verification gates are ready to be run:

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet ef database update --project src/HorseRacing.Infrastructure --startup-project src/HorseRacing.Infrastructure
dotnet run --project src/HorseRacing.Api
```

The local API defaults to `http://localhost:5080`. Its basic health endpoint is `GET /health`.

## Current status

The implementation is **IN PROGRESS**. The domain now covers racecourses, meetings, races, horses, stables, trainers, owners, jockeys, runners and race/runner results, with EF mappings, migration and PostgreSQL SQL. The scraping jobs, raw and created data layers, audit trail, domain-projection job, locally hosted React website, and future prediction views are planned. See [Project status](docs/PROJECT-STATUS.md) for actual verification evidence and remaining gaps.

The [domain dictionary](docs/DOMAIN-MODEL.md) explains entities and relationships; the [data dictionary](docs/DATA-DICTIONARY.md) records physical columns and constraints. External references are in [source links](docs/SOURCE-LINKS.md), and planned delivery is in the [feature backlog](docs/FEATURES.md).

The race creation request now uses `meetingId`, `raceNumber`, `name`, `scheduledStartUtc`, `code`, `surface`, and `distanceMetres`; it no longer accepts `racecourseId`. A meeting must already exist. This is still a scaffold endpoint, not a complete management API. Review [migration precautions](docs/DATABASE.md) before upgrading a populated database.

Development branches from and merges back into `main`; see [Development workflow](docs/DEVELOPMENT-WORKFLOW.md).

# British Horse Racing Data Project

An in-progress .NET service for collecting and managing British horse-racing data. The repository starts with a Clean Architecture foundation, an ASP.NET Core API, and PostgreSQL persistence through Entity Framework Core.

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
  PROJECT-STATUS.md
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
dotnet ef database update --project src/HorseRacing.Infrastructure --startup-project src/HorseRacing.Api
dotnet run --project src/HorseRacing.Api
```

The local API defaults to `http://localhost:5080`. Its basic health endpoint is `GET /health`.

## Current status

The implementation is **IN PROGRESS**. The solution, database model, and initial migration are present, but build, test, and migration-application gates have not yet been run. See [Project status](docs/PROJECT-STATUS.md) for the explicit gate record.

Development branches from and merges back into `main`; see [Development workflow](docs/DEVELOPMENT-WORKFLOW.md).

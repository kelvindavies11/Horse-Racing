# Database

## Technology

- PostgreSQL 17 for local development.
- Entity Framework Core 10 with the Npgsql provider.
- Migrations live under `src/HorseRacing.Infrastructure/Persistence/Migrations`.

## Initial schema

The initial migration creates:

- `racecourses`
- `horses`
- `races`
- `runners`

Foreign keys protect racecourse, race, and horse relationships. A race cannot contain duplicate cloth numbers or duplicate horses.

## Planned data layers

The target architecture adds three logical data boundaries. These are logical responsibilities; the implementation may use PostgreSQL schemas or another explicit separation when it is built.

| Layer | Purpose | Primary consumers |
| --- | --- | --- |
| Raw | Retain data collected from the BHA website before application-specific transformation | Raw-to-created processing and diagnostics |
| Created | Store validated, consistently shaped source records with provenance and observation metadata | Local API for the React website, and domain-projection job |
| Domain | Store the application's domain objects and relationships | Application use cases and API |

Raw ingestion and raw-to-created promotion both require audit records. A created record must link to its source/raw lineage and include `FirstObserved` and `LastObserved`. `FirstObserved` is retained from the record's initial identification; `LastObserved` advances whenever the same source object is seen again successfully.

The existing `racecourses`, `horses`, `races`, and `runners` tables belong to the initial domain persistence model. Raw, created, and audit storage have not yet been implemented.

Prediction persistence is intentionally left open until the future prediction process and its outputs are defined. Prediction results will be served to the React website through the local API rather than read directly from storage.

## Local connection

`compose.yaml` provides a development-only database and matches the connection string in `src/HorseRacing.Api/appsettings.Development.json`.

Start the database:

```powershell
docker compose up -d postgres
```

Apply migrations after the build gate has passed:

```powershell
dotnet ef database update --project src/HorseRacing.Infrastructure --startup-project src/HorseRacing.Api
```

The migration-application gate is currently **IN PROGRESS / NOT RUN**. Committing a migration is not evidence that it has been successfully applied.

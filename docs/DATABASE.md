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

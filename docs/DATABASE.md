# Database

## Technology

- PostgreSQL 17 for local development.
- Entity Framework Core 10.0.12 (including Relational and Design) with Npgsql EF provider 10.0.3.
- Migrations live under `src/HorseRacing.Infrastructure/Persistence/Migrations`.

## Initial schema

The initial migration creates:

- `racecourses`
- `horses`
- `races`
- `runners`

Foreign keys protect racecourse, race, and horse relationships. A race cannot contain duplicate cloth numbers or duplicate horses.

## Expanded domain schema

`ExpandRacingDomain` adds `meetings`, `stables`, `trainers`, `owners`, `jockeys`, `race_results` and `runner_results`, and expands `races` and `runners`. The eleven tables and every field are documented in the [data dictionary](DATA-DICTIONARY.md); business relationships are in the [domain dictionary](DOMAIN-MODEL.md).

Races belong to meetings, and meetings belong to racecourses. Runners reference their race, horse and race-time connections. Composite foreign keys ensure runner results and their parent result refer to the same race. Unique indexes protect race numbers, declaration identity, cloth/draw numbers and one current result per race. SQL CHECK constraints cover enum values, positive measures and result-position consistency; aggregate publication rules remain in the domain.

The versioned [PostgreSQL SQL script](sql/DOMAIN-SCHEMA.sql) is generated from migrations and includes EF migration history checks so it can be reapplied. Regenerate it whenever a migration changes:

```powershell
dotnet ef migrations script --idempotent --project src/HorseRacing.Infrastructure --startup-project src/HorseRacing.Infrastructure --output docs/sql/DOMAIN-SCHEMA.sql
dotnet ef migrations has-pending-model-changes --project src/HorseRacing.Infrastructure --startup-project src/HorseRacing.Infrastructure
```

### Upgrade and rollback

The original scaffold does not contain enough facts to infer meetings, distances, trainers or owners. The expansion therefore checks for existing races/runners and fails before changing them if either table is populated. Back up such a database and supply a reviewed, source-backed data mapping before upgrading it; do not invent connections or erase records to pass the check. Empty scaffold databases and existing course/horse reference rows are supported.

Rollback to `InitialCreate` restores the original course references on retained races, but removes new tables and expanded attributes. It is a destructive schema downgrade and should be used only on a disposable verification database or with an approved restore plan:

```powershell
# Set HORSE_RACING_CONNECTION_STRING to an explicitly disposable database first.
dotnet ef database update InitialCreate --project src/HorseRacing.Infrastructure --startup-project src/HorseRacing.Infrastructure
```

Reapplying the expansion after a populated downgrade requires the same source-backed mapping. Migration history is retained; the original migration is unchanged.

## Data layers

The target architecture adds three logical data boundaries. These are logical responsibilities; the implementation may use PostgreSQL schemas or another explicit separation when it is built.

| Layer | Purpose | Primary consumers |
| --- | --- | --- |
| Raw | Retain data collected from the BHA website before application-specific transformation | Raw-to-created processing and diagnostics |
| Created | Store validated, consistently shaped source records with provenance and observation metadata | Local API for the React website, and domain-projection job |
| Domain | Store the application's domain objects and relationships | Application use cases and API |

Raw ingestion and raw-to-created promotion both require audit records. A created record must link to its source/raw lineage and include `FirstObserved` and `LastObserved`. `FirstObserved` is retained from the record's initial identification; `LastObserved` advances whenever the same source object is seen again successfully.

All eleven domain tables use the default `public` schema. The `AddRawIngestion` migration
adds `raw.collection_runs` and `raw.payloads`: the former records source-to-Raw results and
errors, while the latter stores immutable response bytes, source/HTTP metadata and a
SHA-256 hash. The `AddCuratedPromotion` migration adds `curated.promotion_runs` and
`curated.domain_objects`: the former records Raw-to-Curated results and errors, while the
latter stores typed source-object records with Raw lineage, `FirstObserved`,
`LastObserved`, and JSONB source data. The tables and constraints are documented in the
[data dictionary](DATA-DICTIONARY.md); operating instructions are in the
[BHA Raw collector guide](BHA-RAW-COLLECTOR.md) and
[BHA Curated promoter guide](BHA-CURATED-PROMOTER.md).

`AddRaceResultsWeather` adds `curated.racecourse_locations` and
`curated.race_weather`. The former upserts one normalized location per BHA course
identity with Raw provenance. The latter stores one Open-Meteo hourly observation per
Curated Race record, linked to its location and Raw weather response. Both tables use
restricted foreign keys so audited Raw evidence cannot be deleted through an enrichment
delete. Operating instructions are in the
[race results and weather guide](RACE-RESULTS-WEATHER.md).

Domain persistence remains a later projection target rather than the Raw collector's or
Curated promoter's direct output.

Prediction persistence is intentionally left open until the future prediction process and its outputs are defined. Prediction results will be served to the React website through the local API rather than read directly from storage.

## Local connection

`compose.yaml` provides a development-only database and matches the connection string in `src/HorseRacing.Api/appsettings.Development.json`.

Start the database:

```powershell
docker compose up -d postgres
```

Apply migrations after the build gate has passed:

```powershell
dotnet ef database update --project src/HorseRacing.Infrastructure --startup-project src/HorseRacing.Infrastructure
```

The design-time factory reads `HORSE_RACING_CONNECTION_STRING`; the API reads `ConnectionStrings:HorseRacing` (environment override `ConnectionStrings__HorseRacing`). Set both consistently when using a database other than Compose. Using Infrastructure as the EF startup project directly loads its design-time factory.

The schema was exercised on an isolated local PostgreSQL 16 instance; the Compose target remains PostgreSQL 17 and needs a separate compatibility run. [Project status](PROJECT-STATUS.md) contains detailed evidence. Repeat the row/relationship checks on a disposable migrated database with:

```powershell
psql -h localhost -U horse_racing -d horse_racing -v ON_ERROR_STOP=1 -f tests/sql/verify-domain.sql
```

The SQL checks use a transaction and roll back all sample data. External design references are recorded in [source links](SOURCE-LINKS.md).

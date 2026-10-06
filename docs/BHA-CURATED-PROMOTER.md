# BHA Curated promoter

Reviewed: 5 October 2026.

`HorseRacing.Bha.CuratedPromoter` is a manually run .NET console application. Its
responsibility is to read successful immutable BHA Raw payloads from PostgreSQL and
promote supported JSON source records into typed Curated domain-object records.

It is intentionally separate from `HorseRacing.Bha.RawCollector`. Raw collection captures
source evidence. Curated promotion parses and maps that evidence. The promoter never
updates Raw payload bytes or Raw collection audit rows.

## Current promotion scope

The first promoter supports JSON payloads from the reviewed BHA source families already
captured by the Raw collector. It maps source rows into generic curated domain-object
records keyed by `source_system`, `domain_object_type`, and `source_key`.

The current domain-object types are:

- `Racecourse`
- `Horse`
- `Jockey`
- `Trainer`
- `Owner`
- `Meeting`
- `Race`
- `Runner`
- `RaceResult`
- `RaceGoing`
- `StewardReport`

Unsupported raw media such as HTML pages, calendars, PDFs and spreadsheets are audited as
`Skipped` with an explanatory error code. Invalid JSON is audited as `Failed`. This keeps
the job replayable without pretending that non-parsed source bytes have been promoted.

## Curated persistence

The `AddCuratedPromotion` migration creates two dedicated tables:

- `curated.promotion_runs` records the Raw-to-Curated attempt. It stores the local job and
  promoter version, Raw payload/run lineage, source job/source URL, timestamps, outcome,
  record counts, and any structured error details.
- `curated.domain_objects` stores the latest curated view of each identified BHA source
  object. It retains the BHA source identity, domain-object type, display name, source URL,
  Raw payload/run lineage, last promotion run, `FirstObserved`, `LastObserved`, and a JSONB
  `source_data` document containing the found source row and extracted identity fields.

Repeated observations upsert the same curated domain object by source identity. The first
observation timestamp is retained, and the last observation timestamp advances only after
that source object is successfully found in a supported Raw payload.

## Local run

Start PostgreSQL and apply migrations:

```powershell
docker compose up -d postgres
dotnet ef database update --project src/HorseRacing.Infrastructure --startup-project src/HorseRacing.Infrastructure
```

Run the promoter from the repository root:

```powershell
dotnet run --project src/HorseRacing.Bha.CuratedPromoter
```

Configuration comes from `appsettings.json`, environment variables, or command-line
configuration. PowerShell environment-variable examples:

```powershell
$env:ConnectionStrings__HorseRacing = "Host=localhost;Port=5433;Database=horse_racing;Username=horse_racing;Password=..."
$env:BhaCuratedPromotion__BatchSize = "250"
$env:BhaCuratedPromotion__RetryFailedPayloads = "true"
dotnet run --project src/HorseRacing.Bha.CuratedPromoter
```

To limit a run to specific Raw collector job names, configure `SourceJobNames`:

```powershell
$env:BhaCuratedPromotion__SourceJobNames__0 = "bha-racecourses-api"
$env:BhaCuratedPromotion__SourceJobNames__1 = "bha-jockeys-list-api"
dotnet run --project src/HorseRacing.Bha.CuratedPromoter
```

By default, successfully promoted, skipped and failed payloads are not selected again.
Set `RetryFailedPayloads` to `true` when parser logic has been fixed and failed Raw
payloads should be replayed.

## Inspecting promotion

```sql
SELECT id, job_name, promoter_version, raw_payload_id, raw_collection_run_id,
       source_job_name, source_url, started_at_utc, completed_at_utc, outcome,
       records_found, records_upserted, error_code, error_message
FROM curated.promotion_runs
ORDER BY started_at_utc DESC;

SELECT id, source_system, domain_object_type, source_key, display_name,
       source_url, raw_payload_id, raw_collection_run_id, last_promotion_run_id,
       first_observed_at_utc, last_observed_at_utc
FROM curated.domain_objects
ORDER BY domain_object_type, display_name;
```

The `source_data` JSONB column is intentionally omitted from the default inspection query.
Read it explicitly when diagnosing a particular curated object.

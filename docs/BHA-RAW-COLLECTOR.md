# BHA Raw collector

Reviewed: 5 October 2026.

`HorseRacing.Bha.RawCollector` is a manually run .NET console application. Its only
responsibility is to request the public BHA racecourses page and persist the received
source response and collection result in the PostgreSQL `raw` schema.

It does **not** parse racecourses, create application records, or write to domain tables.
Raw-to-Curated conversion is a separate process boundary: a future converter will read
immutable `raw.payloads` rows and write validated, consistently shaped records to a
separate Created/Curated schema. It must record its own attempt history and Raw lineage,
and must never update a Raw payload in place.

## Source and collection policy

The collector is deliberately restricted in code to:

```text
https://www.britishhorseracing.com/racing/racecourses/
```

The BHA page is publicly readable. Its current `robots.txt` allows this path and specifies
`crawl-delay: 10`; the configured minimum interval therefore cannot be less than ten
seconds. Each process performs one request and has no concurrent fetches or automatic
retries. The default timeout is 30 seconds and the default response limit is 5 MB.

The BHA terms permit personal-use extracts and prohibit automated extraction for
commercial purposes. This local collector is labelled and configured for non-commercial
use only. Do not run it for commercial or business use without an appropriate BHA licence
or written permission. It uses a descriptive user agent and no bearer token, credential,
browser cookie, access-control bypass, or hidden API.

## Raw persistence

The `AddRawIngestion` migration creates two dedicated tables:

- `raw.collection_runs` is the source-to-Raw audit. It records the job, source name and
  URL, collector version, start/completion timestamps, outcome, HTTP status, and structured
  error code/message. A request failure is retained even when no payload exists.
- `raw.payloads` stores the exact response bytes received by the collector, including
  non-success HTTP bodies. It records requested/effective URLs, retrieval time, HTTP and
  content metadata, byte count, and a lowercase SHA-256 hash. Each payload belongs to
  exactly one collection run and is never updated by the application.

Outcomes are `Running`, `Succeeded`, `Failed`, and `Cancelled`. HTTP non-success responses
store both the received body and an `http_<status>` error result. Network, timeout, policy,
and response-size failures store a failed audit result without inventing a payload.

## Local run

Start PostgreSQL and apply migrations:

```powershell
docker compose up -d postgres
dotnet ef database update --project src/HorseRacing.Infrastructure --startup-project src/HorseRacing.Infrastructure
```

Run the console application from the repository root:

```powershell
dotnet run --project src/HorseRacing.Bha.RawCollector
```

Configuration comes from `appsettings.json`, environment variables, or command-line
configuration. Keep environment-specific connection strings out of source control. For
example:

```powershell
$env:ConnectionStrings__HorseRacing = "Host=localhost;Port=5432;Database=horse_racing;Username=horse_racing;Password=..."
dotnet run --project src/HorseRacing.Bha.RawCollector
```

The source URL validation is intentionally not configurable beyond the reviewed page.
Adding another BHA source requires a fresh source-policy review, documentation, fixtures,
and tests rather than weakening this allow-list.

## Inspecting a run

```sql
SELECT id, job_name, source_name, source_url, started_at_utc, completed_at_utc,
       outcome, http_status_code, error_code, error_message
FROM raw.collection_runs
ORDER BY started_at_utc DESC;

SELECT id, collection_run_id, source_url, effective_url, retrieved_at_utc,
       http_status_code, media_type, character_encoding, content_length, sha256
FROM raw.payloads
ORDER BY retrieved_at_utc DESC;
```

The `content` bytea column is intentionally omitted from the default diagnostic query.
Read it explicitly only when inspecting or replaying a Raw record.

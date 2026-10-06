# BHA Raw collector

Reviewed: 5 October 2026.

`HorseRacing.Bha.RawCollector` is a manually run .NET console application. Its only
responsibility is to request the reviewed BHA racecourse, fixture, racecard, racehorse,
jockey, trainer, owner, result and racing-status raw sources and persist each received
source response and collection result in the PostgreSQL `raw` schema.

It does **not** parse racecourses, meetings, races, runners, horses, jockeys, results or
steward reports, create application records, or write to domain tables. Trainer records
and owner championship rows are also not parsed or promoted. Raw-to-Curated conversion is
a separate process boundary: `HorseRacing.Bha.CuratedPromoter` reads immutable
`raw.payloads` rows and writes consistently shaped records to the `curated` schema. It
records its own attempt history and Raw lineage, and never updates a Raw payload in place.

## Source and collection policy

The collector is deliberately restricted in code to the reviewed racecourse, fixture,
racecard, racehorse, jockey, trainer, owner, result and racing-status sources:

```text
https://www.britishhorseracing.com/racing/racecourses/
https://api09.horseracing.software/bha/v1/racecourses/
https://www.britishhorseracing.com/racing/fixtures/full-year/
https://www.britishhorseracing.com/racing/fixtures/upcoming/
https://www.britishhorseracing.com/racing/fixtures/upcoming/racecard/race/
https://www.britishhorseracing.com/racing/horses/racehorse-search-results/
https://www.britishhorseracing.com/racing/participants/jockeys/
https://www.britishhorseracing.com/racing/jockeys-winners-totals/
https://www.britishhorseracing.com/racing/participants/trainers/
https://www.britishhorseracing.com/racing/participants/trainers/trainers-map/
https://www.britishhorseracing.com/racing/participants/trainers/trainers-non-runners/
https://www.britishhorseracing.com/racing/participants/owners/
https://www.britishhorseracing.com/racing/participants/owners/all-owners/
https://www.britishhorseracing.com/racing/results/
https://www.britishhorseracing.com/racing/stewards-reports/
https://www.britishhorseracing.com/racing/racing-updates/
https://api09.horseracing.software/bha/v1/fixtures?per_page=250
https://api09.horseracing.software/bha/v1/fixtures/?resultsAvailable=1&fields=courseId,courseName,fixtureDate,fixtureType,fixtureSession,abandonedReasonCode,highlightTitle
https://api09.horseracing.software/bha/v1/stewards-room/reports
https://crate.horseracing.software/ics/fixtures?year=2026
https://crate.horseracing.software/ics/fixtures?year=2027
https://media.britishhorseracing.com/bha/Fixture_List/2027-Fixture-list.xlsx
https://media.britishhorseracing.com/bha/Fixture_List/2027_Fixture_List.pdf
```

Configured racecard API sources may also be collected when their URLs match one of these
reviewed path shapes:

```text
https://api09.horseracing.software/bha/v1/fixtures/{year}/{fixtureId}/races
https://api09.horseracing.software/bha/v1/fixtures/{year}/{fixtureId}/going
https://api09.horseracing.software/bha/v1/races/{year}/{raceId}/{divisionSequence}
https://api09.horseracing.software/bha/v1/races/{year}/{raceId}/{divisionSequence}/entries
https://api09.horseracing.software/bha/v1/races/{year}/{raceId}/{divisionSequence}/balloted
https://api09.horseracing.software/bha/v1/races/{year}/{raceId}/{divisionSequence}/results
https://api09.horseracing.software/bha/v1/races/{year}/{raceId}/{divisionSequence}/nominations
https://api09.horseracing.software/bha/v1/races/{year}/{raceId}/{divisionSequence}/trans
```

Configured racehorse API sources may also be collected when their URLs match this
reviewed path and query shape:

```text
https://api09.horseracing.software/bha/v1/racehorses?q={at-least-3-characters}[&page={positive-number}][&per_page={1-to-100}][&rated={0-or-1}]
```

Configured jockey API sources may also be collected when their URLs match one of these
reviewed path shapes:

```text
https://api09.horseracing.software/bha/v1/championships/jockeys[?type={flat-or-jump}][&page={positive-number}][&per_page={1-to-100}][&sort={field:asc-or-desc}]
https://api09.horseracing.software/bha/v1/jockeys[?name={at-least-2-characters}][&page={positive-number}][&per_page={1-to-100}]
https://api09.horseracing.software/bha/v1/jockeys/{jockeyId}
https://api09.horseracing.software/bha/v1/jockeys/milestones[?sortby={entryName|totalWins|totalRun|racingToday}:{asc-or-desc}]
```

Configured trainer API sources may also be collected when their URLs match one of these
reviewed path shapes:

```text
https://api09.horseracing.software/bha/v1/championships/trainers[?type={flat-or-jump}][&page={positive-number}][&per_page={1-to-100}][&sort={field:asc-or-desc}]
https://api09.horseracing.software/bha/v1/trainers[?name={at-least-2-characters}][&page={positive-number}][&per_page={1-to-100}]
https://api09.horseracing.software/bha/v1/trainers/nonrunners?type={flat-or-jump}[&page={positive-number}][&per_page={1-to-100}]
https://api09.horseracing.software/bha/v1/trainers/{trainerId}
https://api09.horseracing.software/bha/v1/trainers/{trainerId}/performances[?page={positive-number}][&per_page={1-to-100}]
https://api09.horseracing.software/bha/v1/trainers/{trainerId}/nonrunners[?page={positive-number}][&per_page={1-to-100}]
```

Configured owner API sources may also be collected when their URLs match this reviewed
path shape:

```text
https://api09.horseracing.software/bha/v1/championships/owners[?type={flat-or-jump}][&page={positive-number}][&per_page={1-to-100}][&sort={field:asc-or-desc}]
```

The BHA website pages are publicly readable. Its current `robots.txt` does not disallow
these public page paths and specifies `crawl-delay: 10`; the configured minimum interval
for every enabled source therefore cannot be less than ten seconds. Each process performs
one request per enabled source and has no concurrent fetches or automatic retries. The
default timeout is 30 seconds. The default response limits are 5 MB for website pages and
calendar feeds, 10 MB for the racecourses API, and 20 MB for the fixtures API and fixture
list downloads, racecard API sources, racehorse API sources, jockey API sources, trainer
API sources, owner API sources, result fixture API and stewards reports API. The
`/feeds/` endpoints seen on some BHA pages are not collected because current `robots.txt`
disallows that path family.

The racecourses, fixtures, racecard, racehorse, jockey, trainer, owner, result fixture and
stewards reports API sources are the JSON endpoints used by the current BHA Angular
pages. They require operator-supplied bearer tokens. The repository must not contain those
tokens or copied tokens from public website JavaScript. Configure
`BhaCollection:RacecoursesApi:BearerToken` and
`BhaCollection:FixturesApi:BearerToken` through environment variables, user secrets or
another local configuration source outside version control. Configure
`BhaCollection:RacecardApiBearerToken` for any configured racecard API source,
`BhaCollection:RacehorseApiBearerToken` for any configured racehorse API source and
`BhaCollection:JockeyApiBearerToken` for the configured jockey API sources and
`BhaCollection:TrainerApiBearerToken` for the configured trainer API sources and
`BhaCollection:OwnerApiBearerToken` for the configured owner API sources and
`BhaCollection:RacingStatusApiBearerToken` for the result fixture and stewards reports
API sources. If an API source is enabled but no token is supplied, the collector records a
failed `raw.collection_runs` audit row with the error details and no payload.

The BHA terms permit personal-use extracts and prohibit automated extraction for
commercial purposes. This local collector is labelled and configured for non-commercial
use only. Do not run it for commercial or business use without an appropriate BHA licence
or written permission. It uses a descriptive user agent, no browser cookies and no access
control bypass. API credentials are an explicit local operator concern and are never
committed.

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

To collect the API payloads, provide bearer tokens outside source control. PowerShell
environment-variable configuration uses double underscores for nested settings:

```powershell
$env:BhaCollection__RacecoursesApi__BearerToken = "..."
$env:BhaCollection__FixturesApi__BearerToken = "..."
$env:BhaCollection__RacecardApiBearerToken = "..."
$env:BhaCollection__RacehorseApiBearerToken = "..."
$env:BhaCollection__JockeyApiBearerToken = "..."
$env:BhaCollection__TrainerApiBearerToken = "..."
$env:BhaCollection__OwnerApiBearerToken = "..."
$env:BhaCollection__RacingStatusApiBearerToken = "..."
dotnet run --project src/HorseRacing.Bha.RawCollector
```

Without those tokens, the public page, calendar and fixture-list sources can still be
collected, but the process returns a failure exit code because the enabled API sources
failed and were audited.

Configuration comes from `appsettings.json`, environment variables, or command-line
configuration. Keep environment-specific connection strings out of source control. For
example:

```powershell
$env:ConnectionStrings__HorseRacing = "Host=localhost;Port=5432;Database=horse_racing;Username=horse_racing;Password=..."
dotnet run --project src/HorseRacing.Bha.RawCollector
```

The source URL validation is intentionally not configurable beyond the reviewed
racecourse, fixture, racecard, racehorse, jockey, trainer, owner, result and racing-status
sources. Adding another BHA source requires a fresh source-policy review, documentation,
fixtures and tests rather than weakening this allow-list.

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

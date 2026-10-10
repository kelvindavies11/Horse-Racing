# Race results and race-time weather

The `HorseRacing.RaceDataSync` console job collects a bounded historical date range, retains every source response in Raw, promotes race meetings, races and finishing-order rows into Curated, and then attaches a location and historical weather observation to each Curated race. The website and API read only Curated records and normalized Curated enrichment tables; they never read live BHA or Open-Meteo data.

## Data sources and decisions

- BHA result fixtures identify meetings with results. Each fixture is expanded to races and each race to its runner result rows. The BHA course list supplies course name, postcode and WGS84 coordinates.
- Source identities are `fixtureYear:fixtureId` for meetings, `yearOfRace:raceId:divisionSequence` for races, and `yearOfRace:raceId:divisionSequence:animalId` for runner results. This prevents runners in one race from overwriting each other. The embedded horse, jockey, trainer and owner identities are also materialized for Explore. Because result rows do not expose an official stable name or location, a transparent `Stable of {trainer}` identity is derived from each trainer attribution and marked as derived in its Curated data.
- BHA coordinates are preferred because they share the same audited source hierarchy as the race. Open-Meteo's GeoNames-backed GB geocoding endpoint is used only when BHA coordinates are missing.
- Historical weather comes from the free Open-Meteo archive API. One response is captured per course and local calendar day, using `Europe/London`; the observation for the local hour containing the advertised race time is persisted. This is hourly historical weather, not a claim that a sensor was physically located at the course.
- BHA and Open-Meteo responses, including failures, use the existing Raw payload/run audit. External bearer tokens are operator configuration and are never stored in source control or returned to the browser.

## Run a date range

Apply migrations, set the API connection string, and use the
[documented refresh procedure](BHA-RAW-COLLECTOR.md#refresh-the-public-results-token) to
capture and validate the current BHA public results-client token outside source control:

```powershell
$env:ConnectionStrings__HorseRacing = 'Host=localhost;Port=5433;Database=horse_racing;Username=horse_racing;Password=horse_racing_local'
# Refresh BhaCollection__RacingStatusApiBearerToken, then run:
dotnet run --project src/HorseRacing.RaceDataSync -- --from 2026-09-29 --to 2026-10-05
```

Refresh the token by default before an unattended multi-month import and after a `401` or
`403`. Never echo it, paste it into logs or chat, or store it in source-controlled files.

The inclusive range is limited to 32 days. With no arguments, the job imports the seven most recently completed UTC dates. The supervised archive uses one PostgreSQL-coordinated ten-second BHA request gate plus bounded audited 429 retry/backoff; do not lower it for unattended use. A missing upstream race result remains a failed Raw audit row and does not fabricate a result; a `404` result for a BHA placeholder race is counted as an audited unavailable result rather than making the entire historical month permanently fail.

The result flow is safe to rerun: Raw attempts remain immutable, promotion upserts by source identity while retaining observation history, and the location/weather rows are upserted by course identity and Curated race respectively.

## Restartable Raw-to-Curated batches

Use the restartable monthly runner for longer periods. The fast path deliberately separates each month into two visible process boundaries:

1. `HorseRacing.RaceDataSync --mode raw --reuse-successful` captures only Raw payloads that have not already succeeded. A BHA throttle response is retained as an immutable Raw attempt, followed by bounded 15, 30 and 60 minute recovery delays. Timeouts, network failures, HTTP 408 and HTTP 5xx responses are also audited and retried after 1, 2 and 4 minutes. A successful retry continues the same batch; a `401` or `403` stops immediately so the supervisor can refresh the public token.
2. One coordinator runs `HorseRacing.Bha.CuratedPromoter --drain` every eight minutes while Raw collection continues and again whenever a monthly Raw runner completes. The serialized checkpoints make newly downloaded results visible without allowing Curated drains to overlap.
3. A separate Open-Meteo worker enriches one completed month while the next BHA month is collected. The coordinator polls it during Raw collection and starts the next weather month as soon as the prior one exits. It uses its own one-second cadence and `weather-progress.csv`, so it does not increase BHA traffic and can independently resume missing months.

The runner records one immutable status row per monthly attempt and skips months that have already succeeded. Weather has a separate checkpoint so an older raw-successful month with missing weather is backfilled without redownloading BHA data.

The reviewed Admin import plans expose a clean 2026 pass plus annual archive passes for 2025 through 2020. The 2025 phase is queued to start automatically after the current 2026 pass reaches the latest available Europe/London date; the older archive phases remain manual. Because future 2026 results do not exist yet, that phase uses the date-aware available-results runner:

```powershell
./scripts/resume-available-race-data-tail.ps1 `
  -StartMonth 2026-01 `
  -EndMonth 2026-12 `
  -StateDirectory artifacts/2026-results-sync `
  -RequestDelayMilliseconds 10000 `
  -MaxParallelism 1
```

The resume process refreshes the public BHA results-client token using the official-site workflow, validates it against a bounded fixture month, waits through `418`/`429` throttling, and refuses to run alongside another Raw, Curated or resume process. The unattended runners use a conservative ten-second request cadence. The historical-results client makes one HTTP call per audited Raw attempt; bounded recovery re-enters the shared request gate instead of issuing hidden retry bursts. If all in-process retries fail, the supervisor reports the next retry time and checks readiness again after one minute. The runner bounds the current month at today's Europe/London date, reruns that month when a later day becomes available, and only records a final month when its calendar end has been reached. Successful source payloads are reused by exact job name and source URL when retrying the same range. When a partial month advances, its fixture index is refreshed—including the transition to the final calendar day—while completed fixture and result children remain reusable. Fixture and result work is stored in `raw.result_work_queue`: result items have priority over fixture expansion, terminal failed work becomes retryable after one minute, and audited placeholder `404` results wait 24 hours before another attempt. On completion it performs the idempotent participant backfill used by Explore.

The supervisor owns one phase at a time and normally runs one monthly Raw worker. All workers share a PostgreSQL-coordinated request gate, so BHA calls remain at least ten seconds apart across the whole pool. Adding workers does not increase the upstream request rate. Fixture discovery requests use pages of 250 records to avoid unnecessary page calls. Completed months reuse successful payloads, which also prevents shared lookups from being downloaded once per worker; only a partial current month is refreshed as new results become available. A partial unique index prevents the same Raw job and source from running twice, while an orphaned five-minute-old claim is cancelled before retry. The single coordinator runs periodic and completion Curated drains, so they never overlap; Curated payload claims are unique, stale claims are recovered, and Curated writes also use a database advisory lock.

The completed-year phases use `resume-monthly-race-data-history.ps1` with isolated state directories such as `artifacts/2025-results-sync`. Import Control shows request-level download coverage beside the Raw audit totals. The automation starts 2025 only after the current 2026 pass is caught up and the protected runner pool is free; use the web application's two-step control for 2024 through 2020.

## API and website

`GET /api/v1/curated/results?from=YYYY-MM-DD&to=YYYY-MM-DD` returns an inclusive range of up to 32 days. Omitting dates selects the seven-day window ending on the latest race that has both meeting context and runner results in Curated, so a historical or actively importing local copy opens with populated results. Each race contains its meeting/course context, ordered finishers and non-runners, declared winner, location provenance and race-hour weather when available.

After upgrading an existing local database, materialize participant identities from result rows that were promoted by an older build:

```powershell
./scripts/backfill-curated-result-participants.ps1
```

The backfill is idempotent, preserves Raw payload and promotion lineage, and does not call or interrupt any Raw source job. Direct participant records are left unchanged; only result-derived records are refreshed.

The website's **Results** workspace provides search, race selection, winner and race facts, course coordinates/postcode, weather, and the full finishing order. **Calendar** opens on the latest imported month, groups races by their BHA meeting identity, and provides one tab per race with runners, jockeys, trainers, owners, odds, status, course details and race-time weather. **Explore** shows each entity's direct and inbound curated links and lets the operator open a related race, meeting, runner, horse, jockey, trainer, owner, stable or racecourse record in place. **Admin audit** refreshes every five seconds and shows running state, start time, finish time and duration for Raw jobs and Curated promotions without exposing response bytes or credentials. **Import control** attributes every new audit run to its monthly dispatch item, estimates completed versus expected BHA source responses, and shows separate Raw and Curated totals split by running, succeeded, skipped, failed and cancelled outcomes. A database backfill plus source-key inference supplies the same monthly breakdown for historical result imports where the month can be established from curated lineage.

## Persisted enrichment

- `curated.racecourse_locations` stores the stable BHA course identity, display location, coordinates, timezone and Raw provenance.
- `curated.race_weather` stores one weather observation per Curated race, linked to its course location, Open-Meteo Raw payload/run and source URL.

See the [data dictionary](DATA-DICTIONARY.md), [database guide](DATABASE.md), [source register](SOURCE-LINKS.md) and [project status](PROJECT-STATUS.md) for constraints and verification evidence.

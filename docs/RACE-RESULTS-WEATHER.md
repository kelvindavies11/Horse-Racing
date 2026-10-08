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

The inclusive range is limited to 32 days. With no arguments, the job imports the seven most recently completed UTC dates. The default one-second BHA request interval plus bounded 429 retry/backoff is deliberate; do not lower it for unattended use. A missing upstream race result remains a failed Raw audit row and does not fabricate a result; a `404` result for a BHA placeholder race is counted as an audited unavailable result rather than making the entire historical month permanently fail.

The result flow is safe to rerun: Raw attempts remain immutable, promotion upserts by source identity while retaining observation history, and the location/weather rows are upserted by course identity and Curated race respectively.

## Restartable Raw-to-Curated batches

Use the restartable monthly runner for longer periods. The fast path deliberately separates each month into two visible process boundaries:

1. `HorseRacing.RaceDataSync --mode raw --reuse-successful` captures only Raw payloads that have not already succeeded. If the BHA API throttles or the network fails, the raw pass stops promptly instead of continuing to issue doomed requests.
2. `HorseRacing.Bha.CuratedPromoter --drain` runs immediately afterward and drains every pending Raw payload in batches until none remain.

The runner records one immutable status row per monthly attempt and skips months that have already succeeded. Weather is deferred from this fast Raw-to-Curated import path; run `RaceDataSync --mode weather` separately after the result archive is current.

The reviewed Admin import plan is a clean 2026 pass. Because future results do not exist yet, it uses the date-aware available-results runner:

```powershell
./scripts/resume-available-race-data-tail.ps1 `
  -StartMonth 2026-01 `
  -EndMonth 2026-12 `
  -StateDirectory artifacts/2026-results-sync `
  -RequestDelayMilliseconds 1000
```

The resume process refreshes and validates the public BHA results-client token using the official-site workflow, waits through `418`/`429` throttling, and refuses to run alongside another Raw, Curated or resume process. The runner bounds the current month at today's Europe/London date, reruns that month when a later day becomes available, and only records a final month when its calendar end has been reached. Successful source payloads are reused by exact job name and source URL when retrying the same range. On completion it performs the idempotent participant backfill used by Explore.

## API and website

`GET /api/v1/curated/results?from=YYYY-MM-DD&to=YYYY-MM-DD` returns an inclusive range of up to 32 days. Omitting dates selects the seven-day window ending on the latest race that has both meeting context and runner results in Curated, so a historical or actively importing local copy opens with populated results. Each race contains its meeting/course context, ordered finishers and non-runners, declared winner, location provenance and race-hour weather when available.

After upgrading an existing local database, materialize participant identities from result rows that were promoted by an older build:

```powershell
./scripts/backfill-curated-result-participants.ps1
```

The backfill is idempotent, preserves Raw payload and promotion lineage, and does not call or interrupt any Raw source job. Direct participant records are left unchanged; only result-derived records are refreshed.

The website's **Results** workspace provides search, race selection, winner and race facts, course coordinates/postcode, weather, and the full finishing order. **Calendar** opens on the latest imported month, groups races by their BHA meeting identity, and provides one tab per race with runners, jockeys, trainers, owners, odds, status, course details and race-time weather. **Explore** shows each entity's direct and inbound curated links and lets the operator open a related race, meeting, runner, horse, jockey, trainer, owner, stable or racecourse record in place. **Admin audit** refreshes every five seconds and shows running state, start time, finish time and duration for Raw jobs and Curated promotions without exposing response bytes or credentials.

## Persisted enrichment

- `curated.racecourse_locations` stores the stable BHA course identity, display location, coordinates, timezone and Raw provenance.
- `curated.race_weather` stores one weather observation per Curated race, linked to its course location, Open-Meteo Raw payload/run and source URL.

See the [data dictionary](DATA-DICTIONARY.md), [database guide](DATABASE.md), [source register](SOURCE-LINKS.md) and [project status](PROJECT-STATUS.md) for constraints and verification evidence.

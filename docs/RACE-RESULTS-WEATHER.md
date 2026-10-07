# Race results and race-time weather

The `HorseRacing.RaceDataSync` console job collects a bounded historical date range, retains every source response in Raw, promotes race meetings, races and finishing-order rows into Curated, and then attaches a location and historical weather observation to each Curated race. The website and API read only Curated records and normalized Curated enrichment tables; they never read live BHA or Open-Meteo data.

## Data sources and decisions

- BHA result fixtures identify meetings with results. Each fixture is expanded to races and each race to its runner result rows. The BHA course list supplies course name, postcode and WGS84 coordinates.
- Source identities are `fixtureYear:fixtureId` for meetings, `yearOfRace:raceId:divisionSequence` for races, and `yearOfRace:raceId:divisionSequence:animalId` for runner results. This prevents runners in one race from overwriting each other.
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

The inclusive range is limited to 32 days. With no arguments, the job imports the seven most recently completed UTC dates. The default one-second BHA request interval plus bounded 429 retry/backoff is deliberate; do not lower it for unattended use. A missing upstream race result remains a failed Raw audit row and does not fabricate a result.

The result flow is safe to rerun: Raw attempts remain immutable, promotion upserts by source identity while retaining observation history, and the location/weather rows are upserted by course identity and Curated race respectively.

## API and website

`GET /api/v1/curated/results?from=YYYY-MM-DD&to=YYYY-MM-DD` returns an inclusive range of up to 32 days. Omitting dates selects the last seven completed dates. Each race contains its meeting/course context, ordered finishers and non-runners, declared winner, location provenance and race-hour weather when available.

The website's **Results** workspace provides search, race selection, winner and race facts, course coordinates/postcode, weather, and the full finishing order. **Explore** remains the generic Curated entity and inferred-pattern workspace. **Admin audit** exposes the Raw result/weather jobs and Curated promotions without exposing response bytes or credentials.

## Persisted enrichment

- `curated.racecourse_locations` stores the stable BHA course identity, display location, coordinates, timezone and Raw provenance.
- `curated.race_weather` stores one weather observation per Curated race, linked to its course location, Open-Meteo Raw payload/run and source URL.

See the [data dictionary](DATA-DICTIONARY.md), [database guide](DATABASE.md), [source register](SOURCE-LINKS.md) and [project status](PROJECT-STATUS.md) for constraints and verification evidence.

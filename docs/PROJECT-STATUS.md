# Project status

Last updated: 6 October 2026

Overall status: **IN PROGRESS**

## Present in the repository

- [x] Clean Architecture solution structure
- [x] Eleven-entity domain: racecourses, meetings, races, horses, stables, trainers, owners, jockeys, runners, race results and runner results
- [x] Application use case and repository abstraction
- [x] EF Core PostgreSQL context and entity mappings
- [x] Initial and expanded-domain migrations with matching model snapshot
- [x] Generated idempotent PostgreSQL SQL, row constraints and composite same-race result foreign keys
- [x] ASP.NET Core API composition root, initial endpoint and read-only Curated browsing/audit endpoints
- [x] React and TypeScript Curated results/data explorer with API-only data access, finishing-order, course-location and race-hour-weather views, entity search/filtering, lineage inspection, inferred relationship patterns, responsive design, Admin audit views and UI tests
- [x] Local PostgreSQL container definition
- [x] Manual .NET BHA raw collector with bounded racecourse, fixture/meeting, racecard/race, racehorse, jockey, trainer, owner, result and racing-status source access and external API bearer-token configuration
- [x] Dedicated PostgreSQL Raw payload and source-to-Raw audit tables with migration
- [x] Manual .NET BHA Raw-to-Curated promoter for supported JSON payloads
- [x] Dedicated PostgreSQL Curated domain-object and Raw-to-Curated promotion audit tables with migration
- [x] Bounded historical BHA result sync with fixture/race/runner Raw audit, stable Curated identities and rate-limit backoff
- [x] BHA course-location resolution plus Open-Meteo historical race-hour weather with normalized Curated persistence and Raw lineage
- [x] Typed historical result API and website Results workspace
- [x] Architecture, database, workflow, domain/data dictionaries, relationship diagram and source-links register
- [x] Domain unit tests and transactional SQL integrity checks

## Planned target architecture

- [ ] Additional entity and scheduled jobs to scrape reviewed British Horseracing Authority sources beyond the implemented raw source families
- [ ] Raw extractor records beyond the implemented immutable BHA source-payload foundation
- [ ] Entity-specific Raw-to-Curated validation and mapping beyond the implemented generic JSON promoter
- [x] Generic Curated read API contracts over source provenance, `FirstObserved`, and `LastObserved`
- [ ] Local job converting created records into domain objects
- [x] Local API read endpoints over the Curated layer
- [x] Live Curated-layer data in the locally hosted React website
- [ ] Future prediction generation, persistence, API endpoints, and React views

## Verification gates

| Gate | Status | Evidence |
| --- | --- | --- |
| Dependency restore | **PASSED** | `dotnet restore`, .NET SDK 10.0.301, 6 October 2026 |
| Solution build | **PASSED** | `dotnet build --no-restore`: zero warnings/errors, including `HorseRacing.Bha.RawCollector`, `HorseRacing.Bha.CuratedPromoter` and unit-test projects |
| Unit tests | **PASSED** | `dotnet test --no-build`: 207 passed, zero failed/skipped; includes historical result endpoint allowlists/parsing/composite identities and Open-Meteo allowlists, geocoding and local-hour/DST selection |
| EF model/snapshot consistency | **PASSED** | `dotnet ef migrations has-pending-model-changes`: no changes after `AddRaceResultsWeather` |
| Generated SQL | **PASSED** | Regenerated `docs/sql/DOMAIN-SCHEMA.sql` through `AddRaceResultsWeather`, 6 October 2026 |
| Diff hygiene | **PASSED** | `git diff --check`: no whitespace errors; Git reported line-ending normalization warnings only |
| Result/weather migration apply/rollback/reapply | **PASSED on PostgreSQL 16.14** | Full chain through `AddRaceResultsWeather` applied to disposable `race_results_weather_verification`, rolled back to `AddCuratedPromotion`, both enrichment tables verified absent, and reapplied |
| Raw migration apply/rollback/reapply | **PASSED on PostgreSQL 16** | `AddRawIngestion` applied to isolated `raw_collector_verification`, rolled back to `ExpandRacingDomain`, verified both Raw tables absent, and reapplied |
| Raw collector smoke test | **PASSED on PostgreSQL 16 for page source** | Console run received HTTP 200 and stored one 79,398-byte `text/html; UTF-8` payload with matching byte count, BHA source/effective URLs and SHA-256; audit outcome Succeeded with no error. The API source now requires an operator-supplied token and has unit coverage but no token-backed smoke run in this table |
| Migration apply | **PASSED on PostgreSQL 16** | InitialCreate and ExpandRacingDomain applied to isolated `domain_verification` on loopback port 55432 |
| SQL relationships/constraints | **PASSED on PostgreSQL 16** | `tests/sql/verify-domain.sql` rerun after `AddRaceResultsWeather`: 12 rejection checks, valid dead heat, optional connections/licences and relationship traversal; test data rolled back |
| Migration rollback/reapply | **PASSED on PostgreSQL 16** | Expansion downgraded to InitialCreate and reapplied on empty test DB; populated downgrade retained two races with correct course references across two meetings |
| Legacy-upgrade protection | **PASSED** | Re-upgrade of populated legacy test DB rejected before DDL; both legacy races preserved |
| Last-week result/weather sync | **PASSED with one audited upstream gap** | Live 29 September–5 October 2026 sync produced 203 displayable races, 1,858 runner results and 203 weather-enriched results across 21 courses. Throttled rerun had 252 HTTP 200 collections, zero 429s and one BHA 404 for placeholder race `9999998`; no result was fabricated. |
| API smoke test | **PASSED** | `/health` returned healthy and `/api/v1/curated/results?from=2026-09-29&to=2026-10-05` returned 203 races, 1,858 runners and 203 location/weather enrichments from PostgreSQL 16.14 |
| Frontend lint | **PASSED** | `npm run lint`: zero errors/warnings, 6 October 2026 |
| Frontend tests | **PASSED** | `npm run test -- --reporter=dot`: 5 passed, zero failed, covering result/winner/weather/location/finishing-order display plus API-only browsing, filtering, lineage, patterns and Admin audit navigation, 6 October 2026 |
| Frontend production build | **PASSED** | `npm run build`: TypeScript project build and Vite 7.3.6 production bundle succeeded, 6 October 2026 |
| Website visual smoke | **PASSED** | Playwright reviewed the live PostgreSQL-backed Results ledger/detail, weather/location and full finishing order plus Raw and Curated Admin audit ledgers; screenshots retained under `output/playwright`, 6 October 2026 |
| PostgreSQL 17 target | **NOT RUN** | Docker/PostgreSQL 17 unavailable locally; PostgreSQL 16 verification is not a claim of a 17 run |

The presence of source code, tests, or migrations does not imply that a gate passed. Update this table only with evidence from an actual run.

## Verification notes and limits

The PostgreSQL 16 tests used a separate temporary cluster under ignored `artifacts/postgres-domain-verification`; the existing local PostgreSQL service/data was not altered. EF CLI 10.0.7 emitted an older-tools notice against the 10.0.12 runtime; migration generation, model checks and the PostgreSQL 16.14 apply/rollback/reapply nevertheless completed successfully. The disposable `race_results_weather_verification` database received the requested live seven-day verification data and was used for the API/browser smoke test.

The API exposes generic Curated overview, entity, inferred-relationship and job-audit endpoints plus the typed historical result feed. The website consumes only these Curated API endpoints and deliberately shows unavailable/empty states instead of fallback records. The result sync parses and promotes the BHA meeting/race/runner-result hierarchy; other source families still use generic JSON promotion or remain Raw-only as documented in their guides. The explorer's inferred links are observational patterns, not persisted domain relationships. HTML pages, calendars, PDFs and spreadsheets are skipped with recorded reasons until entity-specific parsers are added. Existing databases with races/runners require a reviewed legacy data mapping; the expansion deliberately fails before DDL when these tables are populated.

Pre-declaration entries, detailed race conditions, pedigree, temporal ownership/training, and official result revisions remain explicit gaps in the [domain dictionary](DOMAIN-MODEL.md). The [data dictionary](DATA-DICTIONARY.md) reflects implemented tables only. Entity-specific Curated parsers, typed domain projections and predictions remain planned in the [feature backlog](FEATURES.md). Research provenance is in [source links](SOURCE-LINKS.md).

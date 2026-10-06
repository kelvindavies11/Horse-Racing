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
- [x] ASP.NET Core API composition root and initial endpoint
- [x] React and TypeScript website foundation with typed data access, responsive race-day dashboard, representative fallback data and UI tests
- [x] Local PostgreSQL container definition
- [x] Manual .NET BHA raw collector with bounded racecourse, fixture/meeting, racecard/race, racehorse, jockey, trainer, owner, result and racing-status source access and external API bearer-token configuration
- [x] Dedicated PostgreSQL Raw payload and source-to-Raw audit tables with migration
- [x] Manual .NET BHA Raw-to-Curated promoter for supported JSON payloads
- [x] Dedicated PostgreSQL Curated domain-object and Raw-to-Curated promotion audit tables with migration
- [x] Architecture, database, workflow, domain/data dictionaries, relationship diagram and source-links register
- [x] Domain unit tests and transactional SQL integrity checks

## Planned target architecture

- [ ] Additional entity and scheduled jobs to scrape reviewed British Horseracing Authority sources beyond the implemented raw source families
- [ ] Raw extractor records beyond the implemented immutable BHA source-payload foundation
- [ ] Entity-specific Raw-to-Curated validation and mapping beyond the implemented generic JSON promoter
- [ ] Created/Curated read API contracts over source provenance, `FirstObserved`, and `LastObserved`
- [ ] Local job converting created records into domain objects
- [ ] Local API read endpoints over the created layer
- [ ] Live Created-layer data in the locally hosted React website (the website foundation currently uses representative data)
- [ ] Future prediction generation, persistence, API endpoints, and React views

## Verification gates

| Gate | Status | Evidence |
| --- | --- | --- |
| Dependency restore | **PASSED** | `dotnet restore`, .NET SDK 10.0.301, 6 October 2026 |
| Solution build | **PASSED** | `dotnet build --no-restore`: zero warnings/errors, including `HorseRacing.Bha.RawCollector`, `HorseRacing.Bha.CuratedPromoter` and unit-test projects |
| Unit tests | **PASSED** | `dotnet test --no-build`: 186 passed, zero failed/skipped; Raw audit paths, BHA source token/URL behavior, Curated promotion handler paths and Curated JSON extraction are covered |
| EF model/snapshot consistency | **PASSED** | `dotnet ef migrations has-pending-model-changes`: no changes after `AddCuratedPromotion` |
| Generated SQL | **PASSED** | Regenerated `docs/sql/DOMAIN-SCHEMA.sql` through `AddCuratedPromotion`, 6 October 2026 |
| Diff hygiene | **PASSED** | `git diff --check`: no whitespace errors; Git reported line-ending normalization warnings only |
| Curated migration apply/rollback | **NOT RUN** | Docker is not installed on this host, so no disposable PostgreSQL apply/rollback gate was available for `AddCuratedPromotion` |
| Raw migration apply/rollback/reapply | **PASSED on PostgreSQL 16** | `AddRawIngestion` applied to isolated `raw_collector_verification`, rolled back to `ExpandRacingDomain`, verified both Raw tables absent, and reapplied |
| Raw collector smoke test | **PASSED on PostgreSQL 16 for page source** | Console run received HTTP 200 and stored one 79,398-byte `text/html; UTF-8` payload with matching byte count, BHA source/effective URLs and SHA-256; audit outcome Succeeded with no error. The API source now requires an operator-supplied token and has unit coverage but no token-backed smoke run in this table |
| Migration apply | **PASSED on PostgreSQL 16** | InitialCreate and ExpandRacingDomain applied to isolated `domain_verification` on loopback port 55432 |
| SQL relationships/constraints | **PASSED on PostgreSQL 16** | `tests/sql/verify-domain.sql` rerun after `AddRawIngestion`: 12 rejection checks, valid dead heat, optional connections/licences and relationship traversal; test data rolled back |
| Migration rollback/reapply | **PASSED on PostgreSQL 16** | Expansion downgraded to InitialCreate and reapplied on empty test DB; populated downgrade retained two races with correct course references across two meetings |
| Legacy-upgrade protection | **PASSED** | Re-upgrade of populated legacy test DB rejected before DDL; both legacy races preserved |
| API smoke test | **PASSED** | `GET /health` returned healthy; `POST /api/races` returned HTTP 201; PostgreSQL query verified race → meeting → course |
| Frontend lint | **PASSED** | `npm run lint`: zero errors/warnings, 5 October 2026 |
| Frontend tests | **PASSED** | `npm run test`: 3 passed, zero failed, 5 October 2026 |
| Frontend production build | **PASSED** | `npm run build`: TypeScript project build and Vite production bundle succeeded, 5 October 2026 |
| PostgreSQL 17 target | **NOT RUN** | Docker/PostgreSQL 17 unavailable locally; PostgreSQL 16 verification is not a claim of a 17 run |

The presence of source code, tests, or migrations does not imply that a gate passed. Update this table only with evidence from an actual run.

## Verification notes and limits

The PostgreSQL 16 tests used a separate temporary cluster under ignored `artifacts/postgres-domain-verification`, with sample databases only. The Raw collector verification databases were removed after their migration, SQL and smoke checks; the existing local PostgreSQL service/data was not altered. PostgreSQL warned that it could not remove physical directories while dropping those disposable databases, so useless files may remain inside the ignored temporary cluster even though the databases were removed from the cluster catalogue. EF CLI 10.0.7 emitted an older-tools notice against the 10.0.12 runtime; migration generation and model checks nevertheless completed successfully. The `AddCuratedPromotion` migration has not been applied or rolled back against PostgreSQL in this environment because Docker is unavailable here.

The API is still a scaffold: reference/meeting creation endpoints, Created-layer read endpoints, full validation/error contracts, result promotion and management endpoints are not implemented. The website therefore defaults to representative fixture data behind the same repository contract expected for the future read API. The Raw collector captures the public BHA racecourse, fixture, racecard, racehorse search, jockey, jockey winners totals, trainer, trainers map, trainers non-runners, owners championship, all owners, results, stewards reports and racing updates pages, reviewed API payloads when configured with operator-supplied bearer tokens and concrete racecard, racehorse, jockey, trainer or owner API URLs, public fixture calendars and reviewed fixture-list downloads; it does not parse or promote racecourse, meeting, race, runner, horse, jockey, trainer, owner, result or steward-report records. The Curated promoter reads successful Raw payloads and promotes supported JSON records into generic `curated.domain_objects` rows with lineage and promotion audit. HTML pages, calendars, PDFs and spreadsheets are skipped with recorded reasons until entity-specific parsers are added. Existing databases with races/runners require a reviewed legacy data mapping; the expansion deliberately fails before DDL when these tables are populated.

Pre-declaration entries, detailed race conditions, pedigree, temporal ownership/training, and official result revisions remain explicit gaps in the [domain dictionary](DOMAIN-MODEL.md). The [data dictionary](DATA-DICTIONARY.md) reflects implemented tables only. Entity-specific Curated parsers, live React browsing, domain projection and predictions remain planned in the [feature backlog](FEATURES.md). Research provenance is in [source links](SOURCE-LINKS.md).

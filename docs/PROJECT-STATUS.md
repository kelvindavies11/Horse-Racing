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
- [x] React and TypeScript Curated data explorer with API-only data access, entity search/filtering, lineage inspection, inferred relationship patterns, responsive design, Admin audit views and UI tests
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
| Unit tests | **PASSED** | `dotnet test --no-build`: 186 passed, zero failed/skipped; Raw audit paths, BHA source token/URL behavior, Curated promotion handler paths and Curated JSON extraction are covered |
| EF model/snapshot consistency | **PASSED** | `dotnet ef migrations has-pending-model-changes`: no changes after `AddCuratedPromotion` |
| Generated SQL | **PASSED** | Regenerated `docs/sql/DOMAIN-SCHEMA.sql` through `AddCuratedPromotion`, 6 October 2026 |
| Diff hygiene | **PASSED** | `git diff --check`: no whitespace errors; Git reported line-ending normalization warnings only |
| Curated migration apply/rollback | **APPLY PASSED; ROLLBACK NOT RUN** | Full migration chain through `AddCuratedPromotion` applied to disposable `curated_explorer_visual` on PostgreSQL 16.14; no migration was added by this feature |
| Raw migration apply/rollback/reapply | **PASSED on PostgreSQL 16** | `AddRawIngestion` applied to isolated `raw_collector_verification`, rolled back to `ExpandRacingDomain`, verified both Raw tables absent, and reapplied |
| Raw collector smoke test | **PASSED on PostgreSQL 16 for page source** | Console run received HTTP 200 and stored one 79,398-byte `text/html; UTF-8` payload with matching byte count, BHA source/effective URLs and SHA-256; audit outcome Succeeded with no error. The API source now requires an operator-supplied token and has unit coverage but no token-backed smoke run in this table |
| Migration apply | **PASSED on PostgreSQL 16** | InitialCreate and ExpandRacingDomain applied to isolated `domain_verification` on loopback port 55432 |
| SQL relationships/constraints | **PASSED on PostgreSQL 16** | `tests/sql/verify-domain.sql` rerun after `AddRawIngestion`: 12 rejection checks, valid dead heat, optional connections/licences and relationship traversal; test data rolled back |
| Migration rollback/reapply | **PASSED on PostgreSQL 16** | Expansion downgraded to InitialCreate and reapplied on empty test DB; populated downgrade retained two races with correct course references across two meetings |
| Legacy-upgrade protection | **PASSED** | Re-upgrade of populated legacy test DB rejected before DDL; both legacy races preserved |
| API smoke test | **PASSED** | Existing health/create checks passed; on 6 October 2026 the four Curated browsing/audit endpoints returned 6 objects across 6 types, a filtered entity, 5 inferred links/patterns, 3 Raw runs and 2 promotion runs from PostgreSQL 16.14 |
| Frontend lint | **PASSED** | `npm run lint`: zero errors/warnings, 6 October 2026 |
| Frontend tests | **PASSED** | `npm run test -- --reporter=dot`: 4 passed, zero failed, covering API-only browsing, filtering, lineage, patterns and Admin audit navigation, 6 October 2026 |
| Frontend production build | **PASSED** | `npm run build`: TypeScript project build and Vite 7.3.6 production bundle succeeded, 6 October 2026 |
| Curated explorer visual smoke | **PASSED** | Explore, lineage detail, Patterns and Admin audit views reviewed in the in-app browser against the disposable PostgreSQL-backed API at desktop and responsive widths, 6 October 2026 |
| PostgreSQL 17 target | **NOT RUN** | Docker/PostgreSQL 17 unavailable locally; PostgreSQL 16 verification is not a claim of a 17 run |

The presence of source code, tests, or migrations does not imply that a gate passed. Update this table only with evidence from an actual run.

## Verification notes and limits

The PostgreSQL 16 tests used a separate temporary cluster under ignored `artifacts/postgres-domain-verification`, with sample databases only. The Raw collector verification databases were removed after their migration, SQL and smoke checks; the existing local PostgreSQL service/data was not altered. PostgreSQL warned that it could not remove physical directories while dropping those disposable databases, so useless files may remain inside the ignored temporary cluster even though the databases were removed from the cluster catalogue. EF CLI 10.0.7 emitted an older-tools notice against the 10.0.12 runtime; migration generation and model checks nevertheless completed successfully. The full migration chain through `AddCuratedPromotion` was applied to a disposable PostgreSQL 16.14 database for the Curated explorer API and visual smoke tests; rollback was not exercised because this feature does not change the schema.

The API now exposes read-only generic Curated overview, entity, inferred-relationship and job-audit endpoints. Entity-specific read contracts, full validation/error contracts, result promotion and management endpoints are not implemented. The website consumes only these Curated API endpoints and deliberately shows an unavailable or empty state instead of representative fallback records. The Raw collector captures the public BHA racecourse, fixture, racecard, racehorse search, jockey, jockey winners totals, trainer, trainers map, trainers non-runners, owners championship, all owners, results, stewards reports and racing updates pages, reviewed API payloads when configured with operator-supplied bearer tokens and concrete racecard, racehorse, jockey, trainer or owner API URLs, public fixture calendars and reviewed fixture-list downloads; it does not parse or promote racecourse, meeting, race, runner, horse, jockey, trainer, owner, result or steward-report records. The Curated promoter reads successful Raw payloads and promotes supported JSON records into generic `curated.domain_objects` rows with lineage and promotion audit. The explorer infers links only from matching scalar identifier/name fields in those generic records; those links are observational patterns, not persisted domain relationships. HTML pages, calendars, PDFs and spreadsheets are skipped with recorded reasons until entity-specific parsers are added. Existing databases with races/runners require a reviewed legacy data mapping; the expansion deliberately fails before DDL when these tables are populated.

Pre-declaration entries, detailed race conditions, pedigree, temporal ownership/training, and official result revisions remain explicit gaps in the [domain dictionary](DOMAIN-MODEL.md). The [data dictionary](DATA-DICTIONARY.md) reflects implemented tables only. Entity-specific Curated parsers, typed domain projections and predictions remain planned in the [feature backlog](FEATURES.md). Research provenance is in [source links](SOURCE-LINKS.md).

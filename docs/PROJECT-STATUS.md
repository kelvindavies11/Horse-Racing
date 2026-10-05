# Project status

Last updated: 5 October 2026

Overall status: **IN PROGRESS**

## Present in the repository

- [x] Clean Architecture solution structure
- [x] Eleven-entity domain: racecourses, meetings, races, horses, stables, trainers, owners, jockeys, runners, race results and runner results
- [x] Application use case and repository abstraction
- [x] EF Core PostgreSQL context and entity mappings
- [x] Initial and expanded-domain migrations with matching model snapshot
- [x] Generated idempotent PostgreSQL SQL, row constraints and composite same-race result foreign keys
- [x] ASP.NET Core API composition root and initial endpoint
- [x] Local PostgreSQL container definition
- [x] Architecture, database, workflow, domain/data dictionaries, relationship diagram and source-links register
- [x] Domain unit tests and transactional SQL integrity checks

## Planned target architecture

- [ ] Local jobs to scrape the British Horseracing Authority website
- [ ] Raw data layer retaining collected source payloads
- [ ] Raw-to-created validation and mapping
- [ ] Created data layer with source provenance, `FirstObserved`, and `LastObserved`
- [ ] Audit trail for raw ingestion and raw-to-created promotion
- [ ] Local job converting created records into domain objects
- [ ] Local API read endpoints over the created layer
- [ ] Locally hosted React website for viewing the collected data
- [ ] Future prediction generation, persistence, API endpoints, and React views

## Verification gates

| Gate | Status | Evidence |
| --- | --- | --- |
| Dependency restore | **PASSED** | `dotnet restore`, .NET SDK 10.0.301, 5 October 2026 |
| Solution build | **PASSED** | `dotnet build --no-restore`: zero warnings/errors |
| Unit tests | **PASSED** | `dotnet test --no-build`: 18 passed, zero failed/skipped |
| EF model/snapshot consistency | **PASSED** | `dotnet ef migrations has-pending-model-changes`: no changes |
| Migration apply | **PASSED on PostgreSQL 16** | InitialCreate and ExpandRacingDomain applied to isolated `domain_verification` on loopback port 55432 |
| Generated SQL | **PASSED on PostgreSQL 16** | Idempotent script applied to fresh `domain_roundtrip`, and reapplied to an already migrated database |
| SQL relationships/constraints | **PASSED on PostgreSQL 16** | `tests/sql/verify-domain.sql`: 12 rejection checks, valid dead heat, optional connections/licences and relationship traversal; test data rolled back |
| Migration rollback/reapply | **PASSED on PostgreSQL 16** | Expansion downgraded to InitialCreate and reapplied on empty test DB; populated downgrade retained two races with correct course references across two meetings |
| Legacy-upgrade protection | **PASSED** | Re-upgrade of populated legacy test DB rejected before DDL; both legacy races preserved |
| API smoke test | **PASSED** | `GET /health` returned healthy; `POST /api/races` returned HTTP 201; PostgreSQL query verified race → meeting → course |
| PostgreSQL 17 target | **NOT RUN** | Docker/PostgreSQL 17 unavailable locally; PostgreSQL 16 verification is not a claim of a 17 run |

The presence of source code, tests, or migrations does not imply that a gate passed. Update this table only with evidence from an actual run.

## Verification notes and limits

The PostgreSQL 16 tests used a separate temporary cluster under ignored `artifacts/postgres-domain-verification`, with sample databases only. The existing local PostgreSQL service/data was not altered. EF CLI 10.0.7 emitted an older-tools notice against the 10.0.12 runtime; migration generation, apply, rollback and model checks nevertheless completed successfully.

The API is still a scaffold: reference/meeting creation endpoints, full validation/error contracts, result ingestion and management endpoints are not implemented. Existing databases with races/runners require a reviewed legacy data mapping; the expansion deliberately fails before DDL when these tables are populated.

Pre-declaration entries, detailed race conditions, pedigree, temporal ownership/training, and official result revisions remain explicit gaps in the [domain dictionary](DOMAIN-MODEL.md). The [data dictionary](DATA-DICTIONARY.md) reflects implemented tables only. Raw/Created/audit storage, React browsing and predictions remain planned in the [feature backlog](FEATURES.md). Research provenance is in [source links](SOURCE-LINKS.md).

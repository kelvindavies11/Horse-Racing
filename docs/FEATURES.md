# Product feature list

This document is the durable product backlog for the Horse Racing project. Completion is shown only by checked items backed by working software and verification evidence. Last reconciled with the repository on 6 October 2026.

The current domain foundation models eleven entities: racecourses, meetings, races, horses, stables, trainers, owners, jockeys, runners, race results and runner results. See the [domain dictionary](DOMAIN-MODEL.md), [data dictionary](DATA-DICTIONARY.md), [PostgreSQL SQL](sql/DOMAIN-SCHEMA.sql), [source links](SOURCE-LINKS.md) and [verification evidence](PROJECT-STATUS.md). This does not mean any ingestion slice below is complete. **Curated layer** is the implemented name for the validated, consistently shaped source-data read model; the earlier **Created layer** label is retired and is distinct from domain persistence.

## Product principles

- [x] Deliver BHA ingestion as end-to-end entity slices, rather than completing one large scraper before any entity is usable.
- [x] Make every successful Curated layer record traceable to an immutable Raw input, collection run, and BHA source location.
- [ ] Define a repeatable stable identity for each entity before using `FirstObserved` and `LastObserved`.
- [x] Keep the React website behind the API; it must not read persistence directly or scrape BHA.
- [x] Treat failures as audit evidence, not observations: a failed scrape or promotion must not advance `LastObserved`.
- [x] Keep collection local-first, repeatable, polite to the source, and safe to reprocess.

## Epic 1: Safe ingestion and lineage foundation

### Features

- [x] **BHA source discovery and collection policy**
  - [x] Catalogue candidate BHA pages/endpoints, entity coverage, navigation paths, identifiers, pagination, expected update cadence, and representative fixtures.
  - [x] Review applicable terms, `robots.txt`, and access constraints before automating each source; record the decision and unresolved restrictions.
  - [x] Set a descriptive user agent, bounded sequential collection, minimum request intervals, response-size limits, timeouts, and bounded retry/backoff. An unattended circuit breaker is not applicable while jobs remain explicitly bounded manual runs.
  - [x] Do not bypass access controls or anti-bot measures; retain a manual or alternative-source path when automated collection is not permitted.
- [ ] **Immutable Raw layer**
  - [x] Store the received payload without in-place mutation, with a content hash, source/location, retrieval timestamp, media type/encoding, and collection run/job identifier.
  - [x] Record request/response metadata needed for diagnosis without storing secrets or unnecessary personal data.
  - [x] Version the collector/promoter and retain enough evidence to replay Raw-to-Curated processing.
  - [ ] Detect payload/schema drift, quarantine affected inputs, and alert without silently producing partial or misleading Curated layer records.
- [x] **Source-to-Raw audit**
  - [x] Record run/job identifier, source/location, start and end timestamps, outcome/status, Raw payload identity and structured errors; each atomic request produces at most one payload, so a separate payload count is not meaningful.
  - [x] Record every failed request even when no Raw payload is produced; partial batch outcomes remain visible as their successful and failed constituent attempts.
- [x] **Raw-to-Curated promotion and audit foundation**
  - [x] Validate supported media/JSON, normalize, map, and upsert supported records by stable source identity.
  - [ ] Record outcome, validation/mapping errors, created/updated/unchanged/rejected counts where meaningful, links to all Raw inputs, and affected Curated identifiers.
  - [x] Preserve the original `FirstObserved`; advance `LastObserved` only after a successful observation and promotion of the same stable identity.
  - [x] Make retries and Raw reprocessing idempotent, while retaining every processing attempt and the promoter version used.
- [ ] **Local job operation**
  - [x] Support explicit manual runs for configured sources and a bounded result date scope.
  - [ ] Add a configurable local schedule if unattended collection becomes a requirement.
  - [ ] Prevent unsafe overlapping runs, expose run status, and allow a failed or quarantined Raw input to be reprocessed without scraping again.
  - [x] Keep connection strings, credentials, and environment-specific settings out of source control; validate required local configuration at startup.

### Epic acceptance criteria

- [x] One entity slice can run source-to-Raw-to-Curated repeatedly without duplicating logical records or losing attempt history.
- [ ] An operator can distinguish success, partial success, source failure, parsing failure, validation rejection, and promotion failure from audit data.
- [x] A Curated layer record can be traced to its Raw payload and collection run, and a failed attempt does not change its observation timestamps.

## Epic 2: Curated layer API and React website foundation

### Features

- [x] **Curated layer read API**
  - [x] Provide versioned generic browse/detail data and a typed result/finishing-order contract for the delivered result slice.
  - [x] Support bounded pagination and stable sorting for generic records, type/search filters, a bounded result date filter, and clear result-range validation errors.
  - [x] Expose Curated identifiers, inferred relationships, source provenance, `FirstObserved`, `LastObserved`, and audit/Raw references suitable for local inspection.
  - [x] Prevent transport models from leaking persistence internals or Raw payload contents by default.
- [x] **React website foundation**
  - [x] Create a locally runnable React application with routing, API client configuration, shared layout/navigation, and loading, empty, and error states.
  - [x] Add a reusable browse page and record detail/lineage view driven only by the Curated layer API.
  - [x] Add type/search filters and bounded pagination controls.
  - [x] Configure the local API proxy safely, with no credentials or environment secrets bundled into the client.
  - [x] Establish accessible semantic markup, keyboard operation, and a responsive baseline for later features.
- [ ] **Browse-state enhancements** — add user-selectable sorting and URL-persisted filter/page state if the operational need justifies them.
- [ ] **API and UI diagnostics**
  - [x] Show data freshness and collection/promotion audit status without presenting failed runs as fresh observations.
  - [x] Provide a local inspection route from a displayed Curated record to its source, Raw and promotion lineage references without exposing Raw response bytes.
  - [ ] Add validation-history inspection and an explicitly privileged full-Raw diagnostic action if local operations require them.

### Epic acceptance criteria

- [x] A developer can start the API and website locally using documented configuration and browse Curated entities.
- [x] List filters, pagination, detail data, freshness, and lineage are consistent between API responses and the UI.
- [ ] API contract tests and React component/integration tests cover the happy path plus loading, empty, validation, and server-error states.

## Epic 3: BHA entity ingestion vertical slices

Each feature below is independently deliverable and must complete the entire slice contract before it is checked. Shared ingestion components should be extracted while delivering early slices; they must not become a reason to postpone all inspectable entity data.

### Standard slice contract

- [ ] Discover and document the permitted BHA source, source identifier or fallback identity, fields, update cadence, edge cases, and representative fixtures.
- [ ] Scrape politely and capture an immutable Raw payload with a complete source-to-Raw audit, including failed attempts.
- [ ] Validate/map/promote into the Curated layer with stable identity, `FirstObserved`/`LastObserved`, idempotency, drift handling, and a complete promotion audit.
- [ ] Expose list, detail, relevant filters/relationships, freshness, and lineage through the API and React inspection UI.
- [ ] Add parser fixture tests, mapping/validation tests, identity/upsert and reprocessing tests, audit/timestamp failure tests, API contract tests, and a UI smoke/integration test.
- [ ] Document entity-specific data-quality rules, known source limitations, and operational/run instructions.

### Reference entities

- [ ] **Racecourses** — first proving slice; establish BHA identity, canonical name, location/country and active/status handling. Dependencies: Epics 1 and 2's minimum foundations.
- [ ] **Trainers** — use the repository/domain term *trainer* (not coach); define identity independently of mutable display names and retain profile/status details available from BHA. Dependencies: shared foundations.
- [ ] **Jockeys** — define identity, name variants, status and available profile attributes; do not merge homonyms without evidence. Dependencies: shared foundations.
- [ ] **Owners** — define person/organisation identity and display-name normalization without assuming names are unique. Dependencies: shared foundations.
- [ ] **Stables** — discover whether BHA exposes a durable stable identity and validate the modelled trainer/yard relationship against source evidence; mark unavailable fields explicitly rather than inferring them. Dependencies: trainers and source discovery.
- [ ] **Horses** — define identity using a durable BHA identifier where available, with name, country, foaling, status and available connections to owner/trainer/stable represented as observed relationships. Dependencies: relevant reference slices, or explicit unresolved-reference handling.

### Racing records

- [ ] **Meetings** — map the implemented domain concept into Curated records with stable source identity, date/status and racecourse relationship; handle abandonment, postponement, and rescheduling without identity churn. Dependencies: racecourses.
- [ ] **Races** — identify a race within its meeting, capture scheduled/actual timing, name/type/class/distance/status and other available conditions, and reconcile reschedules/cancellations. Dependencies: meetings and racecourses.
- [ ] **Runners and entries** — design pre-declaration entries separately from implemented Runner declarations; preserve time-varying participation, including horse and available jockey/trainer/owner/race-time stable links, cloth/draw, weight, odds and non-runner state without overwriting observation history. Dependencies: races, horses, and applicable reference entities.
- [ ] **Race results** — capture race-level result facts such as outcome/status, timing and result publication/correction state, preserving later official corrections as new observations. Dependencies: races.
- [ ] **Runner results** — capture finishing position or non-finish code, distances, starting price and other available performance facts; link unambiguously to the race result and runner/horse. Dependencies: race results and runners/entries.

Implemented subset: the bounded historical sync now retains BHA result fixture, race and
runner payloads in Raw; promotes stable Meeting, Race and RunnerResult source identities;
serves a typed result/finishing-order read model; and shows it with BHA course location
and Open-Meteo race-hour weather in the website. Official correction history, complete
entity-specific validation and domain projection remain open, so the broader slices above
are intentionally not marked complete.

### Completed increments within the open slices

- [x] **Bounded historical result inspection** — collect an inclusive range of up to 32 days, retain fixture/race/runner attempts in Raw, promote stable composite identities, and expose races, winners, finishing order and non-runners through the API and website.
- [x] **Racecourse location and race-hour weather** — prefer BHA coordinates, fall back to bounded Open-Meteo geocoding, persist normalized course locations and historical weather, retain Raw lineage, and display the enrichment beside each result.
- [x] **Curated-only website boundary** — load result, entity, relationship and audit data only through local versioned API routes; do not fetch BHA, Open-Meteo or third-party race assets directly from the browser.

Implementation and verification evidence for these increments is recorded in the
[result/weather runbook](RACE-RESULTS-WEATHER.md) and [project status](PROJECT-STATUS.md).

### Epic acceptance criteria

- [ ] Every checked entity has a documented stable identity and satisfies every item in the standard slice contract.
- [ ] Relationship-heavy records handle missing or late-arriving references deterministically and become linkable through safe reprocessing.
- [ ] Corrections and repeated observations update the intended Curated layer record, retain lineage, and never erase prior audit evidence.

## Epic 4: Operations, observability, and data quality

### Features

- [x] **Admin audit workspace foundation** — show Raw collection and Curated promotion totals plus recent run source, time, outcome, HTTP/error and promotion record counts without exposing payload bytes.
- [ ] **Advanced run diagnostics** — add entity/source/time/status filters, duration and last-success summaries, and links from runs to affected Raw/Curated records.
- [ ] **Structured telemetry** — use correlated structured logs and metrics for request rates, retry/throttle behaviour, payload counts, promotion counts, duration, failures and freshness.
- [ ] **Data-quality rules** — report missing required values, invalid references, duplicates, unexpected cardinality/count changes, stale entities, and source/schema drift with configurable severity.
- [ ] **Recovery procedures** — document replay, quarantine release, parser-version reprocessing, partial-run recovery, backup/restore and safe local database reset procedures.
- [ ] **Retention and privacy review** — define Raw/audit retention and deletion policies consistent with diagnostic, source-policy, and personal-data obligations.

### Epic acceptance criteria

- [ ] A developer can identify the failing transition and impacted records without inspecting application internals.
- [ ] Alerts are actionable and rate-limited, and expected no-change runs are distinguishable from missing or stale data.
- [ ] Recovery exercises demonstrate that immutable Raw inputs can rebuild Curated layer records deterministically for a recorded parser/mapping version.

## Epic 5: Domain projection

### Features

- [ ] Define how each completed Curated layer entity maps to domain entities and relationships, including the target concepts not present in the current code.
- [ ] Implement an idempotent local projection job with its own run status, errors, Curated identifiers, and domain identifiers.
- [ ] Handle late-arriving references and corrections without coupling Curated layer availability to successful domain projection.
- [ ] Extend the existing domain invariants and tests with source-driven cases, temporal ownership/stable assignments, pre-declaration entries and result revisions as those slices are introduced.

### Epic acceptance criteria

- [ ] Re-running projection creates no duplicate domain objects and produces the same state from the same Curated layer inputs and projection version.
- [ ] Projection failures remain visible and recoverable while Curated layer data stays browsable through the website.

## Epic 6: Predictions (future discovery / TBD)

No prediction target, modelling approach, or delivery commitment has been chosen. The following are discovery questions and prerequisites, not decided features.

- [ ] Define the user decision to support and the prediction target, horizon, unit of prediction, and success criteria.
- [ ] Assess historical depth, coverage, correction behaviour, survivorship bias, missingness, and label quality across the Curated layer.
- [ ] Define candidate features and their point-in-time availability; prevent target leakage and future-information leakage.
- [ ] Choose evaluation and backtesting methodology, temporal splits, calibration and comparison metrics appropriate to the target.
- [ ] Establish transparent baseline heuristics/models before considering more complex approaches.
- [ ] Define reproducibility and provenance for datasets, feature definitions, code, model versions, training runs and prediction runs.
- [ ] Decide prediction storage, lifecycle, refresh/correction behaviour, API contracts and React views only after the target and evaluation plan are agreed.
- [ ] Define responsible interpretation: uncertainty, limitations, non-guarantee language, monitoring for degradation, and any gambling-related safeguards.
- [ ] Decide whether predictions belong in the Curated layer, a separate analytical boundary, or another store; do not assume the answer in advance.

### Discovery exit criteria

- [ ] A reviewed discovery note answers the target, data-readiness, leakage, baseline, evaluation, provenance, storage, API/UI and interpretation questions, with unresolved decisions explicitly recorded.
- [ ] A small time-aware baseline can be backtested reproducibly before any production prediction feature is committed.

## Shared definition of done

An epic feature or entity slice is complete only when all applicable items below are satisfied.

- [ ] Acceptance criteria and entity-specific stable identity are documented and reviewed.
- [x] Source-policy/robots review and rate limits are recorded for every BHA location used.
- [ ] Success, no-change, partial, invalid-input, source-failure and processing-failure paths are tested as applicable.
- [ ] Audit, lineage, `FirstObserved`/`LastObserved`, idempotency and reprocessing behaviour are verified with automated tests.
- [x] No Raw response fixtures are retained in the repository; unit tests construct bounded in-memory JSON and no credential is committed. This supersedes the earlier fixture-retention check.
- [ ] API and UI behaviour is documented, accessible, observable, and covered by proportionate contract/integration tests.
- [ ] Schema/parser changes include drift handling and migration or replay guidance.
- [x] Local configuration and run instructions are current; no secret is committed or exposed to the browser.
- [x] Build, test, migration and smoke-test gates are actually run and their evidence is recorded before status is marked complete.

## Revised delivery order

The original racecourse-first sequence is retired: the user-prioritized historical
result/weather slice proved the Raw-to-screen path first. Remaining work should proceed
in dependency order without undoing that completed increment.

1. [x] Deliver the minimum Raw/audit/promotion foundation plus generic Curated API and React browse/detail inspection.
2. [x] Deliver bounded historical results, runner finishing order, course location, race-hour weather and Raw/Curated Admin audit visibility.
3. [ ] Finish typed racecourse, meeting, race and runner/result slices, including validation, correction history and reprocessing tests.
4. [ ] Deliver trainers, jockeys and owners; discover stables in parallel, then deliver stables when identity and source support are clear.
5. [ ] Deliver horses, including explicit handling for unresolved or changing observed relationships.
6. [ ] Harden cross-entity observability, data-quality and recovery capabilities before relying on scheduled unattended runs.
7. [ ] Extend domain projection behind completed Curated layer slices without blocking API/UI inspection.
8. [ ] Begin Predictions discovery only after sufficient historical result data and trustworthy point-in-time lineage exist.

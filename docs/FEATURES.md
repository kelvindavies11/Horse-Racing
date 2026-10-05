# Product feature list

This document is the durable product backlog for the Horse Racing project. It describes intended work, not implemented capability. Completion is shown only by checked items backed by working software and verification evidence.

The current code models racecourses, races, horses, and runners. Meetings, jockeys, trainers, stables, owners, race results, and runner results are target domain concepts that still require modelling. In this backlog, **Created layer** always means the validated, consistently shaped source-data read model; it is distinct from domain persistence.

## Product principles

- [ ] Deliver BHA ingestion as end-to-end entity slices, rather than completing one large scraper before any entity is usable.
- [ ] Make every successful Created layer record traceable to an immutable Raw input, collection run, and BHA source location.
- [ ] Define a repeatable stable identity for each entity before using `FirstObserved` and `LastObserved`.
- [ ] Keep the React website behind the API; it must not read persistence directly or scrape BHA.
- [ ] Treat failures as audit evidence, not observations: a failed scrape or promotion must not advance `LastObserved`.
- [ ] Keep collection local-first, repeatable, polite to the source, and safe to reprocess.

## Epic 1: Safe ingestion and lineage foundation

### Features

- [ ] **BHA source discovery and collection policy**
  - [ ] Catalogue candidate BHA pages/endpoints, entity coverage, navigation paths, identifiers, pagination, expected update cadence, and representative fixtures.
  - [ ] Review applicable terms, `robots.txt`, and access constraints before automating each source; record the decision and unresolved restrictions.
  - [ ] Set a descriptive user agent, bounded concurrency, rate limits, timeouts, retry/backoff rules, and a stop/circuit-breaker policy.
  - [ ] Do not bypass access controls or anti-bot measures; retain a manual or alternative-source path when automated collection is not permitted.
- [ ] **Immutable Raw layer**
  - [ ] Store the received payload without in-place mutation, with a content hash, source/location, retrieval timestamp, media type/encoding, and collection run/job identifier.
  - [ ] Record request/response metadata needed for diagnosis without storing secrets or unnecessary personal data.
  - [ ] Version the extractor/parser and retain enough evidence to replay Raw-to-Created processing.
  - [ ] Detect payload/schema drift, quarantine affected inputs, and alert without silently producing partial or misleading Created layer records.
- [ ] **Source-to-Raw audit**
  - [ ] Record run/job identifier, source/location, start and end timestamps, outcome/status, payload and record counts where meaningful, Raw identifiers, and structured errors.
  - [ ] Record failed and partial attempts even when no Raw payload is produced.
- [ ] **Raw-to-Created promotion and audit**
  - [ ] Validate, normalize, map, and upsert by the entity's documented stable identity.
  - [ ] Record outcome, validation/mapping errors, created/updated/unchanged/rejected counts where meaningful, links to all Raw inputs, and affected Created identifiers.
  - [ ] Preserve the original `FirstObserved`; advance `LastObserved` only after a successful observation and promotion of the same stable identity.
  - [ ] Make retries and Raw reprocessing idempotent, while retaining every processing attempt and the parser/mapping version used.
- [ ] **Local job operation**
  - [ ] Support an explicit manual run for a chosen entity/source/date scope and a configurable local schedule.
  - [ ] Prevent unsafe overlapping runs, expose run status, and allow a failed or quarantined Raw input to be reprocessed without scraping again.
  - [ ] Keep connection strings, credentials, and environment-specific settings out of source control; validate required local configuration at startup.

### Epic acceptance criteria

- [ ] One entity slice can run source-to-Raw-to-Created repeatedly without duplicating logical records or losing attempt history.
- [ ] An operator can distinguish success, partial success, source failure, parsing failure, validation rejection, and promotion failure from audit data.
- [ ] A Created layer record can be traced to its Raw payload and collection run, and a failed attempt does not change its observation timestamps.

## Epic 2: Created layer API and React website foundation

### Features

- [ ] **Created layer read API**
  - [ ] Provide versioned list and detail contracts for each delivered entity slice.
  - [ ] Support bounded pagination, stable sorting, useful entity-specific filters, and clear validation/error responses.
  - [ ] Expose Created identifiers, relationships, source provenance, `FirstObserved`, `LastObserved`, and audit/Raw references suitable for local inspection.
  - [ ] Prevent transport models from leaking persistence internals or Raw payload contents by default.
- [ ] **Boilerplate React website**
  - [ ] Create a locally runnable React application with routing, API client configuration, shared layout/navigation, and loading, empty, and error states.
  - [ ] Add a reusable browse page and record detail/lineage view driven only by the Created layer API.
  - [ ] Add filter, sort, and pagination controls that preserve state in the URL where practical.
  - [ ] Configure local API origin/CORS safely, with no credentials or environment secrets bundled into the client.
  - [ ] Establish accessible semantic markup, keyboard operation, and a responsive baseline for later features.
- [ ] **API and UI diagnostics**
  - [ ] Show data freshness and last successful collection/promotion status without presenting failed runs as fresh observations.
  - [ ] Provide a local inspection route from a displayed Created record to its lineage and validation history, while keeping full Raw payload access an explicit diagnostic action.

### Epic acceptance criteria

- [ ] A developer can start the API and website locally using documented configuration and browse at least the first completed entity slice.
- [ ] List filters, pagination, detail data, freshness, and lineage are consistent between API responses and the UI.
- [ ] API contract tests and React component/integration tests cover the happy path plus loading, empty, validation, and server-error states.

## Epic 3: BHA entity ingestion vertical slices

Each feature below is independently deliverable and must complete the entire slice contract before it is checked. Shared ingestion components should be extracted while delivering early slices; they must not become a reason to postpone all inspectable entity data.

### Standard slice contract

- [ ] Discover and document the permitted BHA source, source identifier or fallback identity, fields, update cadence, edge cases, and representative fixtures.
- [ ] Scrape politely and capture an immutable Raw payload with a complete source-to-Raw audit, including failed attempts.
- [ ] Validate/map/promote into the Created layer with stable identity, `FirstObserved`/`LastObserved`, idempotency, drift handling, and a complete promotion audit.
- [ ] Expose list, detail, relevant filters/relationships, freshness, and lineage through the API and React inspection UI.
- [ ] Add parser fixture tests, mapping/validation tests, identity/upsert and reprocessing tests, audit/timestamp failure tests, API contract tests, and a UI smoke/integration test.
- [ ] Document entity-specific data-quality rules, known source limitations, and operational/run instructions.

### Reference entities

- [ ] **Racecourses** — first proving slice; establish BHA identity, canonical name, location/country and active/status handling. Dependencies: Epics 1 and 2's minimum foundations.
- [ ] **Trainers** — use the repository/domain term *trainer* (not coach); define identity independently of mutable display names and retain profile/status details available from BHA. Dependencies: shared foundations.
- [ ] **Jockeys** — define identity, name variants, status and available profile attributes; do not merge homonyms without evidence. Dependencies: shared foundations.
- [ ] **Owners** — define person/organisation identity and display-name normalization without assuming names are unique. Dependencies: shared foundations.
- [ ] **Stables** — discover whether BHA exposes a durable stable identity and clarify its relationship to trainers before modelling; mark unavailable fields explicitly rather than inferring them. Dependencies: trainers and source discovery.
- [ ] **Horses** — define identity using a durable BHA identifier where available, with name, country, foaling, status and available connections to owner/trainer/stable represented as observed relationships. Dependencies: relevant reference slices, or explicit unresolved-reference handling.

### Racing records

- [ ] **Meetings** — introduce the target Created/domain concept, stable identity, date/status and racecourse relationship; handle abandonment, postponement, and rescheduling without identity churn. Dependencies: racecourses.
- [ ] **Races** — identify a race within its meeting, capture scheduled/actual timing, name/type/class/distance/status and other available conditions, and reconcile reschedules/cancellations. Dependencies: meetings and racecourses.
- [ ] **Runners and entries** — model declarations/entries as time-varying participation, including horse and available jockey/trainer/owner links, cloth/draw, weight, odds and non-runner state without overwriting observation history. Dependencies: races, horses, and applicable reference entities.
- [ ] **Race results** — capture race-level result facts such as outcome/status, timing and result publication/correction state, preserving later official corrections as new observations. Dependencies: races.
- [ ] **Runner results** — capture finishing position or non-finish code, distances, starting price and other available performance facts; link unambiguously to the race result and runner/horse. Dependencies: race results and runners/entries.

### Epic acceptance criteria

- [ ] Every checked entity has a documented stable identity and satisfies every item in the standard slice contract.
- [ ] Relationship-heavy records handle missing or late-arriving references deterministically and become linkable through safe reprocessing.
- [ ] Corrections and repeated observations update the intended Created layer record, retain lineage, and never erase prior audit evidence.

## Epic 4: Operations, observability, and data quality

### Features

- [ ] **Run dashboard and diagnostics** — browse collection and promotion runs by entity, source, time, status and error; show counts, duration, last success, and links to affected Raw/Created records.
- [ ] **Structured telemetry** — use correlated structured logs and metrics for request rates, retry/throttle behaviour, payload counts, promotion counts, duration, failures and freshness.
- [ ] **Data-quality rules** — report missing required values, invalid references, duplicates, unexpected cardinality/count changes, stale entities, and source/schema drift with configurable severity.
- [ ] **Recovery procedures** — document replay, quarantine release, parser-version reprocessing, partial-run recovery, backup/restore and safe local database reset procedures.
- [ ] **Retention and privacy review** — define Raw/audit retention and deletion policies consistent with diagnostic, source-policy, and personal-data obligations.

### Epic acceptance criteria

- [ ] A developer can identify the failing transition and impacted records without inspecting application internals.
- [ ] Alerts are actionable and rate-limited, and expected no-change runs are distinguishable from missing or stale data.
- [ ] Recovery exercises demonstrate that immutable Raw inputs can rebuild Created layer records deterministically for a recorded parser/mapping version.

## Epic 5: Domain projection

### Features

- [ ] Define how each completed Created layer entity maps to domain entities and relationships, including the target concepts not present in the current code.
- [ ] Implement an idempotent local projection job with its own run status, errors, Created identifiers, and domain identifiers.
- [ ] Handle late-arriving references and corrections without coupling Created layer availability to successful domain projection.
- [ ] Add domain invariants and tests for meetings, participants, ownership/stable relationships, race results, and runner results as those slices are introduced.

### Epic acceptance criteria

- [ ] Re-running projection creates no duplicate domain objects and produces the same state from the same Created layer inputs and projection version.
- [ ] Projection failures remain visible and recoverable while Created layer data stays browsable through the website.

## Epic 6: Predictions (future discovery / TBD)

No prediction target, modelling approach, or delivery commitment has been chosen. The following are discovery questions and prerequisites, not decided features.

- [ ] Define the user decision to support and the prediction target, horizon, unit of prediction, and success criteria.
- [ ] Assess historical depth, coverage, correction behaviour, survivorship bias, missingness, and label quality across the Created layer.
- [ ] Define candidate features and their point-in-time availability; prevent target leakage and future-information leakage.
- [ ] Choose evaluation and backtesting methodology, temporal splits, calibration and comparison metrics appropriate to the target.
- [ ] Establish transparent baseline heuristics/models before considering more complex approaches.
- [ ] Define reproducibility and provenance for datasets, feature definitions, code, model versions, training runs and prediction runs.
- [ ] Decide prediction storage, lifecycle, refresh/correction behaviour, API contracts and React views only after the target and evaluation plan are agreed.
- [ ] Define responsible interpretation: uncertainty, limitations, non-guarantee language, monitoring for degradation, and any gambling-related safeguards.
- [ ] Decide whether predictions belong in the Created layer, a separate analytical boundary, or another store; do not assume the answer in advance.

### Discovery exit criteria

- [ ] A reviewed discovery note answers the target, data-readiness, leakage, baseline, evaluation, provenance, storage, API/UI and interpretation questions, with unresolved decisions explicitly recorded.
- [ ] A small time-aware baseline can be backtested reproducibly before any production prediction feature is committed.

## Shared definition of done

An epic feature or entity slice is complete only when all applicable items below are satisfied.

- [ ] Acceptance criteria and entity-specific stable identity are documented and reviewed.
- [ ] Source-policy/robots review and rate limits are recorded for every BHA location used.
- [ ] Success, no-change, partial, invalid-input, source-failure and processing-failure paths are tested as applicable.
- [ ] Audit, lineage, `FirstObserved`/`LastObserved`, idempotency and reprocessing behaviour are verified with automated tests.
- [ ] Raw fixtures contain no secrets and are legally/operationally appropriate to retain in the repository.
- [ ] API and UI behaviour is documented, accessible, observable, and covered by proportionate contract/integration tests.
- [ ] Schema/parser changes include drift handling and migration or replay guidance.
- [ ] Local configuration and run instructions are current; no secret is committed or exposed to the browser.
- [ ] Build, test, migration and smoke-test gates are actually run and their evidence is recorded before status is marked complete.

## Recommended delivery order

1. [ ] Confirm BHA collection policy and deliver the minimum Raw/audit/promotion foundation with the racecourse slice.
2. [ ] Add the Created layer racecourse read API and React browse/detail foundation, proving source-to-screen lineage early.
3. [ ] Deliver trainers, jockeys and owners; discover stables in parallel, then deliver stables when identity and source support are clear.
4. [ ] Deliver horses, including explicit handling for unresolved or changing observed relationships.
5. [ ] Deliver meetings, then races.
6. [ ] Deliver runners/entries, then race results and runner results.
7. [ ] Harden cross-entity observability, data-quality and recovery capabilities continuously, completing Epic 4 before relying on scheduled unattended runs.
8. [ ] Extend domain projection behind completed Created layer slices without blocking API/UI inspection.
9. [ ] Begin Predictions discovery only after sufficient historical result data and trustworthy point-in-time lineage exist.

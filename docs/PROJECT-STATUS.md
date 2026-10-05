# Project status

Last updated: 5 October 2026

Overall status: **IN PROGRESS**

## Present in the repository

- [x] Clean Architecture solution structure
- [x] Domain model for racecourses, races, horses, and runners
- [x] Application use case and repository abstraction
- [x] EF Core PostgreSQL context and entity mappings
- [x] Initial database migration and model snapshot
- [x] ASP.NET Core API composition root and initial endpoint
- [x] Local PostgreSQL container definition
- [x] Markdown architecture, database, workflow, and status documentation
- [x] Unit-test project and initial domain tests

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
| Dependency restore | **IN PROGRESS / NOT RUN** | No restore output recorded |
| Solution build | **IN PROGRESS / NOT RUN** | No build output recorded |
| Unit tests | **IN PROGRESS / NOT RUN** | No test output recorded |
| Migration apply | **IN PROGRESS / NOT RUN** | No PostgreSQL migration run recorded |
| API smoke test | **IN PROGRESS / NOT RUN** | No runtime check recorded |

The presence of source code, tests, or migrations does not imply that a gate passed. Update this table only with evidence from an actual run.

# Architecture

## Target runtime and data architecture

The application is local-first. Data is collected from the British Horseracing Authority (BHA) website, retained in successive data layers, and consumed locally.

```text
British Horseracing Authority website
                |
                | scrape
                v
       Local scraping jobs
                |
                | capture source payload
                v
     +-----------------------+
     | Raw data layer        |
     | + ingestion audit     |
     +-----------------------+
                |
                | validate, map and promote
                v
     +-----------------------+
     | Created data layer    |
     | + provenance          |
     | + FirstObserved       |
     | + LastObserved        |
     | + promotion audit     |
     +-----------------------+
           /           \
          / read         \ read and convert
         v               v
+------------------+   +-------------------------+
| Local read API   |   | Local domain-projection |
+------------------+   | job                     |
         |             +-------------------------+
         | serve data              |
         v                         | create/update
+----------------------+           v
| Locally hosted React |  +------------------+
| website              |  | Domain objects   |
| - browse data        |  +------------------+
| - future predictions |
+----------------------+
```

### Local scraping jobs

- Scrape the BHA website on a local schedule or through a local manual run.
- Capture the source response in the raw layer before it is transformed.
- Promote validated and mapped records from raw storage into the created layer.
- Preserve enough provenance to trace every created record back to its raw input and BHA source.

The first implemented job is the manually run `HorseRacing.Bha.RawCollector` .NET console
application. It is restricted to the reviewed BHA racecourse, fixture, racecard,
racehorse, jockey, trainer, owner, result and racing-status source families and writes
only to the PostgreSQL `raw` schema. It captures source responses; it does not parse or
promote racecourse, meeting, race, runner, horse, jockey, trainer, owner, result or
steward-report records. API sources require operator-supplied bearer tokens outside
source control.

### Raw data layer

- Stores the source data as it was collected, before application-specific interpretation.
- Provides the evidence needed to diagnose scraper or transformation behaviour and to reprocess data.
- Records an audit event whenever a source payload is written, including its source and collection/job context.

The implemented Raw foundation uses `raw.payloads` for exact response bytes and response
metadata, and `raw.collection_runs` for source-to-Raw outcomes and errors. Raw-to-Created
conversion remains a separate, not-yet-implemented executable process. That process will
read Raw rows, write Created/Curated rows and promotion audit records, and will not mutate
Raw evidence.

### Created data layer

- Stores the validated, consistently shaped records produced from raw BHA data.
- Is the read model used by the locally hosted website.
- Is the input to the local job that creates or updates domain objects.
- Retains a link to its raw input and source provenance.

Each identifiable object in this layer has observation metadata:

- `FirstObserved`: when the object was first identified in the created layer. This value is set once and retained.
- `LastObserved`: when the same object was most recently seen in a successful scrape and promotion. This value is refreshed on every observation.
- Source provenance: identifies where the data came from, including the BHA source location and the raw record or collection run that produced it.

The stable identity used to match repeat observations must be defined for each object type. Without that identity, `FirstObserved` and `LastObserved` cannot reliably distinguish a new object from an update to an existing one.

### Audit and lineage

Auditing spans both transitions: source-to-raw ingestion and raw-to-created promotion. The audit trail should record the operation, timestamp, local job/run identifier, source, outcome, and relevant raw/created identifiers. Failed attempts are recorded as well as successful ones, without treating a failed scrape as a successful observation.

Together, the audit trail, source provenance, and observation timestamps answer:

- Where did this record come from?
- When was it first identified?
- When was it last seen?
- Which local run ingested or promoted it?
- Did either transition fail?

### Local consumers

- The local API exposes read endpoints over the created layer for the website.
- A straightforward, locally hosted React website uses those endpoints to browse and inspect the collected data. It does not connect directly to persistence or scrape the BHA website.
- A separate local domain-projection job reads the created layer and converts its records into domain objects.
- These consumers are independent: the website does not need to wait for domain projection before showing created data.

### React website and future predictions

The React website is the presentation layer over the local data pipeline. Its initial scope is deliberately simple: allow a user to view, filter, and inspect created records through the local API.

Prediction views are a future capability. When prediction generation and storage are designed, their results will also be exposed through the local API for the React website to display. The current design reserves this user-interface capability without prescribing or claiming an implemented prediction model.

## Style

The solution uses Clean Architecture. Business rules are kept independent from transport, storage, and framework concerns.

```text
HorseRacing.Api
    |-- HorseRacing.Application
    `-- HorseRacing.Infrastructure
            |-- HorseRacing.Application
            `-- HorseRacing.Domain

HorseRacing.Application
    `-- HorseRacing.Domain
```

## Layer responsibilities

### Domain

- Owns racing entities and invariants.
- Contains no ASP.NET Core, Entity Framework Core, or PostgreSQL references.
- Models Racecourse, Meeting, Race, Horse, Stable, Trainer, Owner, Jockey, Runner, RaceResult and RunnerResult. Definitions, aggregate boundaries and remaining gaps are in the [domain dictionary](DOMAIN-MODEL.md).
- Race owns declarations and its current result. Connection references on runners preserve the race-time trainer, owner, jockey and stable identities. Meeting and the reference entities are separate aggregates.

### Application

- Describes use cases and ports required by those use cases.
- Includes the create-race command handler and `IRaceRepository` abstraction.
- Depends only on Domain.

### Infrastructure

- Implements Application ports.
- Owns the EF Core `HorseRacingDbContext`, PostgreSQL mappings, repository implementation, and migrations.
- Registers infrastructure services for the API composition root.

### API

- Is the application composition root.
- Hosts HTTP endpoints and maps transport requests to Application commands.
- Will provide the created-data read endpoints used by the local React website.
- Does not contain persistence rules.

### Web

- Is a conventional React and TypeScript frontend hosted locally.
- Reads application data through a typed repository boundary rather than accessing storage directly. It currently uses representative fixtures because the Created-layer read endpoint is not implemented; `api` and API-with-fallback modes are configuration-only changes.
- Provides the initial responsive race-day overview, meeting filters, search, schedule, and loading/empty/error states. Prediction views remain future work.

## Dependency rule

Source-code dependencies must point inwards. Domain must not reference Application, Infrastructure, or API. Application must not reference Infrastructure or API.

## Current scope

This remains a scaffold. The expanded domain and its PostgreSQL relationships are implemented; [the data dictionary](DATA-DICTIONARY.md) and [generated SQL](sql/DOMAIN-SCHEMA.sql) describe that implementation. The React website foundation is implemented against representative data behind a typed repository. The manual BHA raw collector, immutable Raw payload table, and source-to-Raw audit table are implemented for the reviewed racecourse, fixture/meeting, racecard/race, racehorse, jockey, trainer, owner, result and racing-status source families. Source parsing, Raw-to-Created processing, Created storage and promotion audit/lineage remain planned, as do additional entity/scheduled collectors, domain-projection, live website data, and prediction views. Result promotion, revision history, authentication, operational observability and deployment remain future work. [Project status](PROJECT-STATUS.md) records executed verification gates. Consult [source links](SOURCE-LINKS.md) for research provenance.

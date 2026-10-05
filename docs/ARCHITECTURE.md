# Architecture

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
- Currently models `Race`, `Racecourse`, `Horse`, and `Runner`.

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
- Does not contain persistence rules.

## Dependency rule

Source-code dependencies must point inwards. Domain must not reference Application, Infrastructure, or API. Application must not reference Infrastructure or API.

## Current scope

This is an initial scaffold, not a completed product. Authentication, data ingestion, race-result capture, observability, and production deployment remain future work. Build and test gates are recorded as **IN PROGRESS** until they are actually run.

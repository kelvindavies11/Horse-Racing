# Kaggle Raceform bulk import

`HorseRacing.KaggleDataSync` is a resumable alternative to the request-by-request BHA
history collector. It downloads version 118 of the public Kaggle Raceform archive,
extracts its SQLite database, stores lossless monthly JSON chunks in the existing Raw
schema, and promotes those chunks into the existing Curated domain objects.

The source archive is about 1.12 GB. It is cached under `.data/kaggle-raceform`, which is
excluded from Git. The download is streamed through a partial file, moved into place only
after success, and recorded locally with its byte count and SHA-256 fingerprint. The
temporary signed redirect URL is never persisted. PostgreSQL receives monthly payloads
rather than the single archive because a PostgreSQL `bytea` value cannot safely hold this
archive at its current size.

## Run the complete job

From the repository root:

```powershell
./scripts/run-kaggle-raceform-import.ps1 -From 2017-01-01 -To 2025-12-31
```

Or double-click `run-kaggle-raceform-import.bat` to use the default range (2015 through
today). The `.bat` file also accepts the PowerShell parameters:

```bat
run-kaggle-raceform-import.bat -From 2017-01-01 -To 2025-12-31
```

Useful switches:

- `-RawOnly` downloads and captures Raw data without promotion.
- `-RefreshRaw` creates fresh monthly Raw payloads even when successful payloads already
  exist for this configured dataset version.

Normal restarts reuse both the cached archive and successful monthly Raw payloads. Failed
or incomplete work is retried, and completed Raw and Curated audit records remain visible
in the application.

## Compatibility and lineage

The importer creates the same Racecourse, Meeting, Race, RunnerResult, Horse, Jockey,
Trainer, Owner, and derived Stable object types used by the web application. Records use
the `KaggleRaceform` source system so they cannot overwrite BHA records. The results read
path accepts both sources and suppresses duplicate race cards when an overlapping BHA
record is present. Weather enrichment can process the imported races through the existing
weather job after promotion.

The configured dataset version is deliberately pinned. Before changing the version,
review the dataset page and its licence/usage conditions, update `KaggleRaceform` in
`src/HorseRacing.KaggleDataSync/appsettings.json`, and run with `-RefreshRaw`.

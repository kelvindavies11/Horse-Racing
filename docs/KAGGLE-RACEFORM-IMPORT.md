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

## Source and local file locations

| Item | Location | Details |
| --- | --- | --- |
| Public dataset page | [Horse Racing Results UK/Ireland 2015-2025](https://www.kaggle.com/datasets/deltaromeo/horse-racing-results-ukireland-2015-2025) | Human-readable Kaggle page for the selected public dataset. Review its current licence and usage conditions before changing or redistributing the data. |
| Pinned download | [Kaggle API version 118 download](https://www.kaggle.com/api/v1/datasets/download/deltaromeo/horse-racing-results-ukireland-2015-2025?datasetVersionNumber=118) | Public archive endpoint used by the importer. Kaggle may redirect this URL to a temporary signed download URL; that redirect is not stored. |
| Cached archive | `.data/kaggle-raceform/raceform-v118.zip` | 1,120,726,580 bytes. SHA-256: `5f48f8f8640e0b429b0d8a21aad69e0fcd9a31bd5181ead37cf4b9bb919b3464`. Excluded from Git. |
| Download manifest | `.data/kaggle-raceform/raceform-v118.zip.manifest.json` | Records the dataset slug, pinned version, stable public endpoint, byte count, SHA-256 and download timestamp. Excluded from Git. |
| Extracted SQLite file | `.data/kaggle-raceform/raceform-v118.db` | Extracted from `form_2015-present/form_2015-present/raceform.db` in the archive. The current local file is 801,103,872 bytes and includes the importer's local date index. Excluded from Git. |
| Import project | `src/HorseRacing.KaggleDataSync` | Downloads, verifies and extracts the archive, creates monthly immutable Raw payloads, then promotes them to Curated records. |
| Import launchers | `run-kaggle-raceform-import.bat` and `scripts/run-kaggle-raceform-import.ps1` | Restartable entry points for archive download, Raw capture and Curated promotion. |
| Weather launcher | `scripts/run-kaggle-raceform-weather.ps1` | Restartable monthly Open-Meteo enrichment after Curated promotion. |

All local paths above are relative to the repository root. In the current checkout that
root is `C:\Users\kelvi\OneDrive\Documents\ChatGPT\Horse-Racing`. The observed version
118 source coverage is January 2015 through May 2026; there were no source rows for June
through October 2026 when it was imported on 11 October 2026.

Each monthly PostgreSQL Raw payload records this logical source URL pattern for lineage:

```text
https://www.kaggle.com/datasets/deltaromeo/horse-racing-results-ukireland-2015-2025/versions/118?rawMonth=YYYY-MM
```

The Raw collection job name is `kaggle-raceform-results`, the Curated promotion job name
is `kaggle-raceform-to-curated`, and promoted objects use source system
`KaggleRaceform`.

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
weather job after promotion. For the pinned archive coverage, use the restartable monthly
weather runner after the bulk job succeeds:

```powershell
./scripts/run-kaggle-raceform-weather.ps1 -From 2015-01-01 -To 2026-05-31
```

The weather runner skips completed months, retries transiently failed months, stores every
Open-Meteo response in Raw, and writes race-hour observations through the normal Curated
weather tables. It does not collect BHA race data; because it hosts `RaceDataSync`, the
repository's documented public-token refresh must still be completed before it starts.

The configured dataset version is deliberately pinned. Before changing the version,
review the dataset page and its licence/usage conditions, update `KaggleRaceform` in
`src/HorseRacing.KaggleDataSync/appsettings.json`, and run with `-RefreshRaw`.

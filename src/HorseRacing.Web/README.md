# Horse Racing Web

A small React and TypeScript presentation layer for browsing local horse-racing data. The dashboard currently uses representative fixtures because the Created-layer read API is not implemented yet. UI components consume a `RacingRepository`; they do not fetch or embed domain records directly.

## Start locally

```powershell
cd src/HorseRacing.Web
npm install
npm run dev
```

The development site is served at `http://127.0.0.1:5173`. Vite proxies `/api` to the existing ASP.NET Core host at `http://localhost:5080`.

Copy `.env.example` to `.env.local` when a non-default data mode is needed:

- `VITE_DATA_MODE=mock` uses the typed local fixture (the default).
- `VITE_DATA_MODE=api` requires the read endpoint and shows an error if it is unavailable.
- `VITE_DATA_MODE=auto` calls the read endpoint and falls back to the same typed fixture if the API is unavailable.
- `VITE_API_BASE_URL=/api` sets the public API base. This is not a secret and may also be an absolute local URL.

## Expected read contract

The repository expects `GET /api/v1/race-days/{yyyy-MM-dd}` to return:

```json
{
  "date": "2026-10-05",
  "generatedAtUtc": "2026-10-05T10:52:00Z",
  "origin": "api",
  "meetings": [
    {
      "id": "meeting-guid",
      "name": "Autumn Racing Meeting",
      "scheduledDate": "2026-10-05",
      "type": "Flat",
      "status": "Scheduled",
      "racecourse": {
        "id": "racecourse-guid",
        "name": "Ascot",
        "locality": "Berkshire"
      },
      "goingDescription": "Good to firm",
      "weatherDescription": "Bright · 16°C",
      "races": [
        {
          "id": "race-guid",
          "raceNumber": 1,
          "name": "The Opening Mile",
          "scheduledStartUtc": "2026-10-05T11:45:00Z",
          "code": "Flat",
          "surface": "Turf",
          "distanceMetres": 1609,
          "status": "Scheduled",
          "runnerCount": 11
        }
      ]
    }
  ]
}
```

Enum strings intentionally mirror the existing domain (`Flat`, `Hurdle`, `Steeplechase`, `NationalHuntFlat`; `Turf`, `AllWeather`). The server should derive `origin`; the client overwrites it to `api` after a successful response. A future contract should add provenance, observation timestamps and lineage links without changing the component-facing repository boundary.

## Quality gates

```powershell
npm run lint
npm run test
npm run build
```


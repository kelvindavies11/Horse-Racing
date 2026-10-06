# Horse Racing Web

A responsive React and TypeScript workspace for browsing the local Curated layer and inspecting Raw-to-Curated job evidence. The website reads the local API only: it contains no representative fixture mode and does not fall back to sample records when the API is unavailable.

## Start locally

Apply the existing migrations, start the API, then start the website:

```powershell
dotnet ef database update --project src/HorseRacing.Infrastructure --startup-project src/HorseRacing.Infrastructure
dotnet run --project src/HorseRacing.Api

cd src/HorseRacing.Web
npm ci
npm run dev
```

The development site is served at `http://127.0.0.1:5173`. Vite proxies `/api` to the ASP.NET Core host at `http://localhost:5080`.

Copy `.env.example` to `.env.local` only when the API base URL needs to change:

```text
VITE_API_BASE_URL=/api
```

This public URL is not a secret. Database credentials remain server-side.

## Views

- **Results** shows the most recent completed week by default, with search, winner/result facts, complete finishing order, course coordinates/postcode and Open-Meteo weather for the local race hour.
- **Explore** shows Curated record counts, type distribution, name/source-key search, type filtering, bounded pagination and a record drawer containing the found data, source location, observation timestamps and Raw/Curated lineage identifiers.
- **Patterns** infers links where reference-like Curated fields such as `horseId`, `fixtureId`, `courseName` or `trainerId` match another record's source key or display name. It shows type-pair frequency and lets the user inspect connected entity hubs. These are inspection signals, not persisted or asserted domain relationships.
- **Admin audit** shows aggregate and per-run information for Raw collection and Curated promotion jobs, including outcomes, timing, versions, HTTP/payload metadata, record counts and recorded errors. It is read-only.

## Read API

The typed repository consumes:

| Endpoint | Purpose |
| --- | --- |
| `GET /api/v1/curated/results?from=&to=` | Bounded historical race results with runners, course location and race-hour weather |
| `GET /api/v1/curated/overview` | Total records, type counts and observation range |
| `GET /api/v1/curated/entities?type=&search=&page=&pageSize=` | Stable, bounded Curated record list with found data and lineage |
| `GET /api/v1/curated/relationships?limit=` | Bounded inferred relationship graph and type-pair counts |
| `GET /api/v1/admin/audit?limit=` | Raw collection and Curated promotion run ledger |

The frontend does not receive Raw payload bytes. `pageSize` is constrained to 12–100 records, the relationship sample to 50–800 records and each audit list to 20–200 runs.

## Quality gates

```powershell
npm run lint
npm run test
npm run build
```

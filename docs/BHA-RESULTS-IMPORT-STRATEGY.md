# BHA results import strategy experiment

Reviewed: 9 October 2026.

## Outcome

Use a quota-aware, continuously progressing collector rather than short request bursts
followed by failed monthly jobs and supervisor restarts.

The selected design keeps the existing BHA endpoints and the existing immutable Raw
contract. It changes request scheduling, restart recovery and downstream pipelining:

- start BHA JSON API requests no more frequently than once every 9 seconds across all
  workers during the controlled trial;
- switch the active monthly worker to the conservative 10-second interval after its first
  audited HTTP 418 or 429 response;
- issue one HTTP attempt per audited Raw collection attempt;
- persist an HTTP 429 response before applying a 15, 30, then 60 minute cooldown;
- retry the same item after the cooldown and continue the batch when it succeeds;
- classify request timeouts separately and retry timeouts, network failures, HTTP 408 and
  HTTP 5xx responses after bounded 1, 2 and 4 minute delays;
- stop on the first HTTP 401 or 403 so the supervisor can refresh the public token without
  spending the remaining queue against an expired credential;
- retain successful-payload reuse for process restarts;
- persist fixture-expansion and race-result work in `raw.result_work_queue`;
- claim missing race results before more fixture-expansion work;
- cache result `404` responses for 24 hours and terminal failed work for one minute after
  the bounded in-process retries are exhausted;
- refresh a partial month's fixture index while reusing completed child payloads;
- run serialized Curated checkpoints and one-second Open-Meteo weather work alongside
  the independently gated BHA collector.

`HorseRacing.Bha.CuratedPromoter`, the Curated schema, the API, and the web application do
not need a data-contract change. The source URLs, job names, exact response bytes, hashes,
and Raw lineage remain the same.

## Production evidence

The comparison used read-only queries over the active collector's Raw audit history. No
experimental BHA API traffic was sent while the existing import was running.

For the measured 24-hour window:

| Measurement | Observed value |
| --- | ---: |
| Audited Raw attempts | 4,291 |
| Distinct source URLs | 4,262 |
| HTTP 200 | 4,260 |
| HTTP 404 | 7 |
| Audited HTTP 429 | 24 |
| Median interval between audited starts | 5.01 seconds |
| Gaps longer than one minute | 10 |
| Time in those long gaps | 16.04 hours |

Repeated bursts consistently reached about 500 audited attempts. A representative burst
made 501 attempts in 41.7 minutes and ended with two 429 responses. This strongly suggests
a quota boundary near 500 requests in a rolling interval. It is an inference from the Raw
audit, not a published BHA limit.

The old HTTP client also retried each 429 up to four times inside one audited collection
attempt. Those extra calls bypassed the database request gate and were not individually
visible in Raw, amplifying traffic precisely when the source was asking the collector to
slow down.

## Alternatives considered

### Increase parallelism

Rejected. The database gate already serialized normal request starts. More workers did
not improve sustainable throughput and made simultaneous throttle recovery more likely.

### Replace per-race results with a bulk results endpoint

Rejected for now. The official BHA public results client uses the same fixture-races and
per-race results endpoint hierarchy as the collector. Stored fixture-race payloads contain
race metadata but not runner results, so they cannot replace the per-race payload while
preserving the current Curated records.

### Scrape the public results HTML

Rejected after a browser-level experiment on 9 October 2026. The server returned an
84,538-byte Angular page shell, not materialized race-result rows. Loading that page in a
real browser generated 55 requests, including the same BHA `racecourses` and paginated
`fixtures` JSON endpoints used by the collector. Both JSON calls returned HTTP 418 during
the experiment and the rendered page displayed `Error Loading Results`.

The page controller confirms that it requests ten fixtures per API page. It does not
embed the results in HTML. Following fixtures would still use the fixture-races and
per-race results endpoints, so browser scraping would retain the API bottleneck while
adding HTML, JavaScript, CSS, image, font and analytics requests. It would also produce a
less stable parsing contract than preserving the source JSON bytes.

A coordinated live probe also requested `per_page=1000`. The BHA API rejected it with
`The per page field must not be greater than 250.` The collector's value of 250 is
therefore the server-enforced maximum. It already returns every fixture in the largest
observed 2026 month in one response.

### Quota-aware continuous collection

Selected. The original conservative interval was one request every 10 seconds, or 360
requests per hour. The controlled API-only trial starts at one request every 9 seconds, or
400 requests per hour, while retaining one shared gate and automatically returning the
active worker to 10 seconds after the first 418 or 429. Using the measured 4,262 distinct
URLs, an uninterrupted throttle-free pass is approximately 10 hours 39 minutes rather
than 11 hours 50 minutes at 10 seconds. Website-page collection remains at least 10
seconds apart in accordance with the public website's robots policy.

This is a replay estimate rather than a live BHA load test. A live comparison was avoided
because the existing backfill was still running and additional traffic could have affected
both the source quota and the production measurement.

## Prototype and verification

The implementation makes these source changes:

- `RaceDataSync` clamps its shared BHA JSON API request interval to at least 9 seconds and
  configures a 10-second throttle fallback interval.
- `BhaHistoricalResultsApiClient` returns the first response instead of performing hidden
  retries.
- `CollectRaceResultsHistoryHandler` records a 429, waits using bounded exponential
  cooldowns, retries the same URL through the normal Raw path, and continues the batch.
- the handler also records and retries transient transport/timeout/408/5xx failures, while
  authentication failures stop immediately;
- the sync summary reports recovered throttle and transient attempts separately from
  terminal failures.
- a durable queue records Pending, Running, Succeeded, Unavailable and Failed work with
  Raw run/payload lineage; five-minute stale claims are recovered transactionally with
  `FOR UPDATE SKIP LOCKED`;
- orchestration uses one BHA worker at the adaptive 9-to-10-second cadence and maintains a separate
  `weather-progress.csv`, allowing missing 2026 weather to be backfilled concurrently;
  weather completion is polled during Raw collection so the next weather month starts
  immediately instead of waiting for the active BHA month to finish.

Automated verification covers both layers:

- an application test returns 429 once, verifies the failed Raw attempt was persisted,
  retries successfully, and confirms the following race is still collected;
- application tests cover timeout classification, transient recovery, immediate auth
  failure stopping, and unresolved deferred failures remaining visible to the supervisor;
- an infrastructure test verifies that a 429 causes exactly one HTTP call in the client;
- all Application tests pass (20/20), including adaptive 418/429 fallback, partial-month
  child reuse and 404 caching;
- all Infrastructure tests pass (187/187);
- all Domain tests pass (18/18);
- all web application tests pass (9/9);
- the Release `HorseRacing.RaceDataSync` build succeeds with zero warnings and errors.

## Rollout

The additive queue migration was applied on 9 October 2026. The protected supervisor
then refreshed and validated the public BHA token and resumed May 2026 with one Raw
runner at 10 seconds. January weather began concurrently. Initial live verification
showed 578 succeeded result queue items, five pending result items, one running result
item, two cached unavailable results, and 283 newly populated 2026 weather rows.

The existing progress CSV and successful Raw payload reuse make this rollout resumable;
no already collected Raw payload needs to be discarded or rewritten.

## 2026 weather observation

The HTML experiment did not solve the missing 2026 race weather. The database contained
3,685 Curated 2026 races and zero matching `curated.race_weather` rows at the time of the
test. All 1,571 successful Open-Meteo Raw weather requests were for 2021 through 2023;
there had been no 2026 weather pass.

The BHA fixture JSON contains a short source weather description for 967 of 1,014 sampled
2026 fixture records, but this is not equivalent to the application's hourly weather
model, which stores temperature, humidity, precipitation, weather code, wind and Raw
Open-Meteo lineage for each race. The current archive supervisor deliberately runs
`RaceDataSync --mode raw` and periodic Curated promotion; weather enrichment is deferred.
The revised supervisor now runs one bounded weather month concurrently with BHA Raw work,
records it independently, and retries only unfinished weather months. This keeps weather
traffic on Open-Meteo out of the BHA request budget.

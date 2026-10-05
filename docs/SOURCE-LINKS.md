# Source links and reference register

Reviewed: 5 October 2026. These links were consulted for this domain review or identify the canonical project/technical references. This register does not claim to reconstruct links used before the repository was created. A research reference is not an approved scraping endpoint or evidence that every field is publicly available.

## Racing references consulted

| Reference | Used for | Interpretation |
| --- | --- | --- |
| [BHA race planning](https://www.britishhorseracing.com/about/planning/) | Fixtures and race programmes | Meeting/fixture and individual race are separate concepts; fixtures may move. |
| [BHA full-year fixtures](https://www.britishhorseracing.com/racing/fixtures/full-year/) | Fixture source discovery and raw page capture | Meeting/fixture source shell; conversion and stable Created identity remain future work. |
| [BHA fixtures API](https://api09.horseracing.software/bha/v1/fixtures?per_page=250) | Fixture/meeting source payload discovered from the current BHA full-year fixtures page scripts | Requires a bearer token supplied outside source control; no token is committed or inferred as a licence. |
| [BHA fixtures calendars](https://crate.horseracing.software/ics/fixtures?year=2026) | Public calendar fixture source for meeting-date evidence | Current collector defaults to reviewed 2026 and 2027 calendar URLs; update configuration and docs when year coverage changes. |
| [BHA 2027 fixture list XLSX](https://media.britishhorseracing.com/bha/Fixture_List/2027-Fixture-list.xlsx) and [PDF](https://media.britishhorseracing.com/bha/Fixture_List/2027_Fixture_List.pdf) | Official fixture-list download raw evidence | Binary Raw payloads only; parsing and Created promotion are separate future work. |
| [BHA upcoming fixtures](https://www.britishhorseracing.com/racing/fixtures/upcoming/) | Racecard/race source discovery shell | Public page; racecard API URLs require fixture/race IDs from upstream source data. |
| [BHA racecard page](https://www.britishhorseracing.com/racing/fixtures/upcoming/racecard/race/) | Racecard/race source shell | Public page; without query parameters it is a shell, not a complete racecard. |
| BHA racecard API patterns under `https://api09.horseracing.software/bha/v1/` | Configured race, runner-entry, balloted, result and fixture-race raw payloads | Requires bearer token and concrete year/fixture/race identifiers supplied outside source control; no token or discovered IDs are committed. |
| [BHA horses](https://www.britishhorseracing.com/racing/horses/racehorse-search-results/) | Racehorse source discovery shell and raw page capture | Public page; the search API requires an operator-supplied bearer token and configured search terms of at least three characters. |
| BHA racehorses API under `https://api09.horseracing.software/bha/v1/racehorses` | Configured racehorse search raw payloads discovered from the current BHA horses page script | Requires bearer token and configured query seeds supplied outside source control; no token or exhaustive horse list is committed. |
| [BHA jockeys](https://www.britishhorseracing.com/racing/participants/jockeys/) | Jockey championship/source discovery shell and raw page capture | Public page; the Jockey APIs require a bearer token supplied outside source control. |
| [BHA jockeys winners totals](https://www.britishhorseracing.com/racing/jockeys-winners-totals/) | Jockey milestone source discovery shell and raw page capture | Public page; the milestones API requires a bearer token supplied outside source control. |
| BHA jockey APIs under `https://api09.horseracing.software/bha/v1/` | Configured jockey championship, list/search/profile and milestone raw payloads | Requires bearer token and any pagination/profile IDs supplied outside source control; no token or exhaustive jockey list is committed. |
| [BHA trainers](https://www.britishhorseracing.com/racing/participants/trainers/) | Trainer championship/source discovery shell and raw page capture | Public page; the Trainer APIs require a bearer token supplied outside source control. |
| [BHA trainers map](https://www.britishhorseracing.com/racing/participants/trainers/trainers-map/) | Trainer map page shell raw capture | Public page only; an old commented map-feed token in the page script is deliberately not used or committed. |
| [BHA trainers non-runners](https://www.britishhorseracing.com/racing/participants/trainers/trainers-non-runners/) | Trainer non-runner source discovery shell and raw page capture | Public page; the non-runner API requires a bearer token supplied outside source control. |
| BHA trainer APIs under `https://api09.horseracing.software/bha/v1/` | Configured trainer championship, list/search/profile, performance and non-runner raw payloads | Requires bearer token and any pagination/profile IDs supplied outside source control; no token or exhaustive trainer list is committed. |
| [BHA owners championship](https://www.britishhorseracing.com/racing/participants/owners/) | Owner championship source discovery shell and raw page capture | Public page; the owner championship API requires a bearer token supplied outside source control. |
| [BHA all owners](https://www.britishhorseracing.com/racing/participants/owners/all-owners/) | Owner championship list shell and raw page capture | Public page; pagination is represented by configured API source URLs. |
| BHA owner championship API under `https://api09.horseracing.software/bha/v1/championships/owners` | Configured owner championship raw payloads | Requires bearer token and any pagination supplied outside source control; no token or exhaustive owner list is committed. |
| [BHA results](https://www.britishhorseracing.com/racing/results/) | Result-fixture source discovery shell and raw page capture | Public page; detailed JSON results require a bearer token supplied outside source control. |
| [BHA stewards reports](https://www.britishhorseracing.com/racing/stewards-reports/) | Stewards report source discovery shell and raw page capture | Public page; detailed JSON reports require a bearer token supplied outside source control. |
| [BHA racing updates](https://www.britishhorseracing.com/racing/racing-updates/) | Racing status/update page capture | Public page; retained as raw evidence for abandoned/update status discovery. |
| [BHA result fixtures API](https://api09.horseracing.software/bha/v1/fixtures/?resultsAvailable=1&fields=courseId,courseName,fixtureDate,fixtureType,fixtureSession,abandonedReasonCode,highlightTitle) | Result-available fixture raw payload discovered from the current BHA results page scripts | Requires a bearer token supplied outside source control; no token is committed or inferred as a licence. |
| [BHA stewards reports API](https://api09.horseracing.software/bha/v1/stewards-room/reports) | Stewards report raw payload discovered from the current BHA stewards page scripts | Requires a bearer token supplied outside source control; no token is committed or inferred as a licence. |
| [BHA racecourses](https://www.britishhorseracing.com/racing/racecourses/) | Venue reference data | Racecourse is a distinct entity; track details are additional scope. |
| [BHA racecourses API](https://api09.horseracing.software/bha/v1/racecourses/) | Racecourse source payload discovered from the current BHA racecourses page scripts | Requires a bearer token supplied outside source control; no token is committed or inferred as a licence. |
| [BHA robots.txt](https://www.britishhorseracing.com/robots.txt) | Racecourses-page collection policy, reviewed 5 October 2026 | Public path is not disallowed; `crawl-delay: 10` is enforced as the minimum interval. Re-review before adding a source. |
| [BHA website terms](https://www.britishhorseracing.com/terms-conditions/) | Usage restrictions, reviewed 5 October 2026 | Personal-use extracts are allowed; commercial web scraping is prohibited. The local collector is non-commercial only and is not evidence of a broader licence. |
| [BHA guide to handicapping](https://www.britishhorseracing.com/regulation/guide-to-handicapping/) | Horse/result annotations | Informs runner connections, weight, class, going, ratings, placing, margins and starting price. Some are deliberately recorded as remaining gaps. |
| [BHA owners' toolkit](https://www.britishhorseracing.com/regulation/ownership/owners-toolkit/) | Ownership forms, entries/declarations, connections | Ownership may represent groups. Entry and declaration are different stages. No fees or deadlines are hard-coded from this page. |
| [BHA declaration tracking announcement](https://www.britishhorseracing.com/press_releases/transparent-declaration-tracking-available-on-bha-website/) | Provisional versus published declarations | Historical announcement (10 March 2023), used for the distinction, not current procedural deadlines. |
| [BHA FAQs](https://www.britishhorseracing.com/about/faqs/) | Results, margins and stewards | Supports treating corrections and publication history as a necessary future capability. |
| [BHA racing statistics](https://www.britishhorseracing.com/regulation/reports-and-statistics/racing-statistics/) | Horse training returns and actual off-time data | Supports future temporal training assignments and separate scheduled/actual times. |
| [BHA Racing Digital](https://www.britishhorseracing.com/about/racing-digital/) | Administration context | Entries, declarations and staff management are distinct activities. Not a selected integration. |
| [Weatherbys](https://www.weatherbys.co.uk/) and [group services](https://www.weatherbys.co.uk/about-us/the-weatherbys-group) | Racing/breeding service boundaries | Context for pedigree and registration scope; no data-feed licence or integration claimed. |

The entity design, nullability and aggregate boundaries are project decisions informed by these sources, not a BHA-prescribed schema. Re-check the current rules and source contract before implementing rule-specific deadlines, eligibility or automated extraction.

## Repository and technical references

| Reference | Use |
| --- | --- |
| [Canonical repository](https://github.com/kelvindavies11/Horse-Racing/) | Project source of truth |
| [EF Core package guidance](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/10.0.9) | Keep Microsoft EF packages aligned; the original mixed transitive Relational versions caused a build warning. |
| [EF Core Design 10.0.12](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Design/10.0.12) | Selected design-time patch |
| [Npgsql EF provider 10.0.3](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3) | EF 10 dependency compatibility |

## Maintenance rule

When an external source informs a feature, add its exact URL, purpose, review date and material limitations here. Preserve source URLs on future ingestion records separately: this document is the research register, not the runtime lineage store. Update [domain](DOMAIN-MODEL.md), [data](DATA-DICTIONARY.md) and [database](DATABASE.md) documentation together when a relationship changes.

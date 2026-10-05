# Source links and reference register

Reviewed: 5 October 2026. These links were consulted for this domain review or identify the canonical project/technical references. This register does not claim to reconstruct links used before the repository was created. A research reference is not an approved scraping endpoint or evidence that every field is publicly available.

## Racing references consulted

| Reference | Used for | Interpretation |
| --- | --- | --- |
| [BHA race planning](https://www.britishhorseracing.com/about/planning/) | Fixtures and race programmes | Meeting/fixture and individual race are separate concepts; fixtures may move. |
| [BHA full-year fixtures](https://www.britishhorseracing.com/racing/fixtures/full-year/) | Fixture source discovery | Reference page; no extractor or stable external-ID contract verified yet. |
| [BHA racecourses](https://www.britishhorseracing.com/racing/racecourses/) | Venue reference data | Racecourse is a distinct entity; track details are additional scope. |
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

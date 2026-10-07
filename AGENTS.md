# Repository agent instructions

## BHA bearer-token workflow

Before running `HorseRacing.RaceDataSync` or an enabled BHA JSON source, refresh the
public web-client bearer token from the official BHA results page by following
[`docs/BHA-RAW-COLLECTOR.md`](docs/BHA-RAW-COLLECTOR.md#refresh-the-public-results-token).
Do this before asking the operator to provide a token.

Never print, paste into chat, commit, or write the token to a repository file. Store it
only in `BhaCollection__RacingStatusApiBearerToken` in the process or user environment,
and validate it only against the allow-listed `https://api09.horseracing.software/bha/v1/`
API with the official BHA origin and results-page referrer. Record only the HTTP status,
token length, or a one-way fingerprint in diagnostics.

If the public client stops exposing a bearer token, the official hosts change, or access
requires an account, login, CAPTCHA, or newly created credential, stop and ask the user
rather than attempting to bypass that control.

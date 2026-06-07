# ADR 005: Security Posture — Loopback Bind, No Auth, and Crawler-Start Hardening

## Status

Accepted

## Context

Photo Organizer is a personal, single-user application intended to run on a local machine and serve a browser on the same host. This shapes the threat model materially:

- **Multi-user auth is a non-goal**: there is only one user; adding auth would add complexity with no benefit (SPEC.md §12).
- **LAN access is explicitly out of scope** until auth and rate limiting are in place (SPEC.md §10).
- The only state-changing operation exposed over HTTP is `POST /api/crawler/start`. All other endpoints are read-only.

Even in a loopback-only deployment, cross-site request forgery is a real threat: a malicious web page open in the same browser could make a credentialed same-origin-exempt request to `localhost:6192` and trigger a crawler run against an attacker-supplied path. The `POST /api/crawler/start` endpoint also constructs a CLI invocation, making argument injection a concern.

## Decision

**The server binds loopback-only by default; `POST /api/crawler/start` applies layered defense-in-depth.**

### Bind address

- **Development**: `localhost:6192` via `src/PhotoOrganizer.Server/Properties/launchSettings.json`.
- **Published builds**: Kestrel defaults to `localhost:5000`.
- Binding to `0.0.0.0` or any non-loopback address is prohibited until authentication and rate limiting are implemented (see `SECURITY.md`).

### CSRF guard — `X-Requested-With` header

`POST /api/crawler/start` requires the `X-Requested-With` header. This header is **not** a CORS-safelisted header, so any cross-origin request that includes it triggers a CORS preflight. The server's CORS policy (configured in `src/PhotoOrganizer.Server/Program.cs`) only allows the known frontend origins; the preflight is blocked for any other origin. The server returns **403** if the header is absent. This approach was chosen over framework anti-forgery tokens because:
- It requires no session state or cookie
- It is simpler to implement and test
- It is sufficient for a loopback-only personal app; token-based CSRF protection is reserved for multi-user deployments

### Allowlist validation — `StartCrawlValidation`

`Mode` and `Step` values are validated against `StartCrawlValidation` (`src/PhotoOrganizer.Application/Crawler/StartCrawlValidation.cs`) before the crawler is launched:
- Valid modes: `full`, `incremental`, `targeted`
- Valid steps: `metadata`, `duplicates`
- Returns **400** with a descriptive message if either field is invalid

`StartCrawlValidation` lives in the Application layer (not Server) so the Server can reference it without taking a dependency on the Crawler project.

### Argument injection prevention — `ProcessStartInfo.ArgumentList`

The crawler is launched via `ProcessStartInfo.ArgumentList` (one token per entry), **never** via `ProcessStartInfo.Arguments` (a single concatenated string). This ensures that user-supplied field values — including mode and step strings — cannot be re-tokenized by the shell into additional flags or arguments even if an invalid value somehow bypasses the allowlist.

## Consequences

**Positive:**
- Simple, auditable security model: one mutating endpoint, three independent controls
- No session state or cookies required for CSRF protection
- Argument injection is structurally impossible regardless of input content
- Threat surface is minimal — loopback bind prevents LAN/internet exposure entirely

**Accepted tradeoffs:**
- Binding to non-loopback addresses for legitimate remote access (e.g. same-LAN tablet) requires adding authentication and rate limiting first — this is a deliberate forcing function, not a gap
- The `X-Requested-With` CSRF guard relies on browsers respecting CORS preflight rules; it would not protect against a same-origin attacker (not a concern for a loopback app)
- Auth and multi-user support are declared non-goals for this phase; revisiting them would require significant new infrastructure

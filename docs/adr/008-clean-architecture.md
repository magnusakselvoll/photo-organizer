# ADR 008: Clean Architecture Layering and Repository Pattern

## Status

Accepted

## Context

The backend needs a structure that keeps business logic testable and independent of I/O concerns (filesystem, sidecar format, ASP.NET). Several approaches were considered:

| Approach | Testability | Change isolation | Complexity |
|----------|------------|-----------------|------------|
| Monolithic ASP.NET service layer | Poor — domain logic tightly coupled to HTTP/DI | Any infrastructure change touches business logic | Low |
| Transaction-script style | Moderate — logic can be extracted, but coupling is implicit | Difficult to swap implementations | Low–moderate |
| Clean Architecture (Domain → Application → Infrastructure → Server) | High — domain is pure .NET; infrastructure is fully swappable | Each layer has a well-defined seam | Moderate |

The project targets a personal NAS deployment where storage technology (filesystem/SMB paths, sidecar format, crawler stack) may evolve, but the core domain concepts (photo, folder, duplicate group) are stable. Isolating domain logic from infrastructure is a high-value investment.

The architecture is modeled on the [photo-booth-take-two](https://github.com/magnusakselvoll/photo-booth-take-two) reference implementation.

## Decision

**The backend follows Clean Architecture with a strict dependency direction: Domain → Application → Infrastructure → Server.**

### Layer responsibilities

| Project | Responsibility |
|---------|---------------|
| `PhotoOrganizer.Domain` | Entities (`Photo`, `SourceFolder`), value objects, repository interfaces, domain helpers (`DisplayableImageFormats`, `SupportedPhotoExtensions`) — add here first |
| `PhotoOrganizer.Application` | Use cases, DTOs, service interfaces (`IPhotoService`), validation (`StartCrawlValidation`) — no I/O |
| `PhotoOrganizer.Infrastructure` | File system access, sidecar parsing, indexing (`RandomizedSidecarIndexer`), image transcoding (`MagickImageTranscoder`), crawler service |
| `PhotoOrganizer.Server` | ASP.NET Core host, API endpoints, middleware, DI wiring, static file serving |
| `PhotoOrganizer.Web` | React + TypeScript frontend (Vite build, deployed as static files under `wwwroot/`) |

### Repository pattern

All data access goes through domain-defined interfaces:
- `IPhotoRepository` — list, find, serve photos
- `IFolderRepository` — discover and read source folders
- `ISidecarReader` / `ISidecarStore` — read/write sidecar metadata
- `IImageTranscoder` — transcode images for browser delivery

Infrastructure implements these interfaces; the Application and Domain layers never reference concrete infrastructure types. This makes unit testing straightforward: tests inject fakes/stubs rather than hitting the filesystem.

### Pluggable providers

Camera-specific logic, storage backends, and detection strategies are implemented behind interfaces so they can be swapped or extended without touching the domain. The crawler's `IBatchProcessingStep` / `IProcessingStep` follow the same pattern (see ADR 009).

## Consequences

**Positive:**
- Domain and Application layers have zero I/O dependencies; unit tests are fast and require no filesystem setup
- Infrastructure implementations can be replaced (e.g. swapping `MagickImageTranscoder` for a different transcoding backend) without touching business logic
- The layering makes it clear where new code belongs: domain concepts go in `Domain`, use cases in `Application`, I/O implementations in `Infrastructure`
- Consistent with the reference implementation; developers familiar with either codebase can navigate both

**Accepted tradeoffs:**
- More projects and more indirection than a simple monolith; justified by the expected lifetime and extensibility requirements of the project
- Interface boundaries require explicit DI wiring in `Program.cs`; adding a new feature requires touching multiple projects

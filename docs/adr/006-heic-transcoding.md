# ADR 006: On-the-Fly HEIC/HEIF Transcoding via Magick.NET (No Caching)

## Status

Accepted

## Context

Modern iPhones and many cameras capture images in HEIC/HEIF format. Only Safari natively supports HEIC in an `<img>` element; Chrome, Firefox, and Edge do not. To serve HEIC photos to all browsers, the server must convert them to JPEG.

ADR 001 noted that Tier 3 perceptual hashing would require a native image-decoding dependency and deferred that decision. HEIC browser support forces the issue earlier: the `/api/photos/{id}/image` endpoint must serve a displayable format for every photo in the library.

Four strategies were considered:

| Strategy | Complexity | Serving latency | Disk usage | Dependencies |
|----------|-----------|-----------------|------------|--------------|
| Pre-generate JPEG derivatives at crawl time | High — crawler must write JPEGs, server must discover them | Low (file read) | High — doubles storage for HEIC photos | No new dependency |
| On-demand transcoding with disk cache | Moderate — cache invalidation, storage quota | Low after first request | Moderate | Native image lib |
| On-demand transcoding with memory cache | Moderate — eviction policy, memory ceiling | Low after first request | None (memory) | Native image lib |
| On-demand transcoding, no cache | Low — stateless endpoint | Per-request CPU hit | None | Native image lib |

For a personal single-user app with a small concurrent request rate, per-request transcoding is acceptable. Caching adds complexity (invalidation, storage limits, first-load discrepancy) that is not worth the benefit at this scale. The "no caching" approach is explicitly a v1 tradeoff; a disk-based or memory-based cache can be added later without changing the public API.

Magick.NET (`Magick.NET-Q8-AnyCPU`) was chosen because:
- It wraps ImageMagick, which includes libheif support
- It is a well-maintained NuGet package with reliable cross-platform binaries
- The `IImageTranscoder` abstraction makes it straightforward to swap in another implementation later

## Decision

**HEIC/HEIF photos are transcoded to JPEG on the fly at `/api/photos/{id}/image`, using `Magick.NET-Q8-AnyCPU`. No transcoded result is cached.**

Implementation:
- `IImageTranscoder` (`src/PhotoOrganizer.Domain/Interfaces/IImageTranscoder.cs`) — abstraction in Domain; injected into the image endpoint
- `MagickImageTranscoder` (`src/PhotoOrganizer.Infrastructure/Imaging/MagickImageTranscoder.cs`) — concrete implementation; wraps `MagickImage` in `Task.Run` to avoid blocking the request thread
- `DisplayableImageFormats.IsTranscodable(filePath)` gates which files are transcoded (currently `.heic`, `.heif`)

RAW formats are not transcoded — they are excluded from grid/slideshow listings (see ADR 007) but remain downloadable as `application/octet-stream` via the version panel's image endpoint.

Caching is deferred to a future pass. When added, it should be implemented in the image endpoint or a caching decorator around `IImageTranscoder`, without changing `MagickImageTranscoder`.

## Consequences

**Positive:**
- All HEIC photos display correctly in every major browser
- Stateless endpoint — no cache warm-up, no invalidation, no storage budget to manage
- `IImageTranscoder` abstraction allows swapping the implementation without touching the API layer

**Accepted tradeoffs:**
- Every HEIC request transcodes from scratch; repeated views of the same photo incur full CPU cost each time
- `Magick.NET-Q8-AnyCPU` ships a substantial native payload (libheif, ImageMagick) — adds ~50 MB to published builds
- Transcoding is synchronous at the ImageMagick layer (`Task.Run` provides thread-pool isolation but does not make it truly async I/O)

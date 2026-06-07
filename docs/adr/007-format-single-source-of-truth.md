# ADR 007: Format Capability Single-Source-of-Truth and Display Policy

## Status

Accepted

## Context

The photo organizer handles a heterogeneous mix of file formats: JPEG, PNG, WEBP, AVIF, BMP (browser-native), HEIC/HEIF (server-transcoded), and RAW formats (CR2, CR3, ORF, ARW, NEF, RW2) plus TIFF. Multiple subsystems need to know which formats fall into which capability bucket:

- The **indexer** (`RandomizedSidecarIndexer`) must know which files to index at all
- The **crawler** (`FileDiscoverer`) must know which files to discover
- The **photo service** (`PhotoService.ApplyFilters`) must know which photos to include in grid and slideshow listings
- The **transcoder** (`MagickImageTranscoder`) must know which files it can transcode
- The **duplicate detector** (`DuplicatesStep`) must know which formats are display-preferred

Before the refactors in commits `835fa37`, `afebbde`, and `9cf61db`, these extension sets were duplicated as independent `HashSet<string>` literals scattered across the above components. Adding or removing a format required a multi-file search; omitting one component caused silent errors (e.g. a file indexed but not served, or served but not discoverable).

## Decision

**Two Domain helpers are the single sources of truth for format capabilities. No other code should define its own extension lists.**

### `DisplayableImageFormats` (`src/PhotoOrganizer.Domain/DisplayableImageFormats.cs`)

Defines which formats the server can serve to a browser:
- `BrowserDisplayableExtensions` — rendered natively by browsers (`.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.avif`, `.bmp`)
- `TranscodableExtensions` — transcoded to JPEG by the server before delivery (`.heic`, `.heif`)
- `AllDisplayableExtensions` — the union of both; used as the baseline for `SupportedPhotoExtensions`
- `IsDisplayable(filePath)` — used by `PhotoService.ApplyFilters` to gate grid/slideshow listings

### `SupportedPhotoExtensions` (`src/PhotoOrganizer.Domain/SupportedPhotoExtensions.cs`)

Defines which files are recognised as photos at all (indexer + crawler):
- Constructed as `DisplayableImageFormats.AllDisplayableExtensions ∪ {RAW/TIFF extensions}`
- Guarantees that every displayable/transcodable format is also discoverable — you can never have a photo served to the grid that the crawler does not know about

**The invariant `displayable ⊆ discoverable` is pinned by a unit test** in `tests/PhotoOrganizer.Application.Tests/SupportedPhotoExtensionsTests.cs` so the two lists cannot silently diverge.

### Display policy

RAW formats (CR2, CR3, ORF, ARW, NEF, RW2) and bare TIFF are:
- **Discoverable** — the crawler indexes them and they appear in the photo model
- **Not displayable** — excluded from grid and slideshow listings by `PhotoService.ApplyFilters`
- **Downloadable** — accessible via the version panel's `/api/photos/{id}/image` endpoint as `application/octet-stream`

This policy means a photographer who shoots RAW+JPEG sees their JPEG in the grid with the RAW available for download in the version panel; a RAW-only photographer can still download files but will not see them in browse or slideshow.

## Consequences

**Positive:**
- Adding a new format (e.g. AVIF transcoding) requires editing exactly one place
- The subset invariant test prevents the common error of adding a displayable format without making it discoverable
- `DuplicatesStep` can rank formats by display preference by delegating to `IsDisplayable` rather than maintaining its own ranking table

**Accepted tradeoffs:**
- RAW-only users get a degraded grid experience (no thumbnails); this is a known limitation pending Tier 3 transcoding support (see ADR 001)
- Bare TIFF is excluded from display even though some TIFFs are browser-renderable — conservative choice to avoid browser compatibility edge cases

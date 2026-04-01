# ADR 001: Crawler Technology Stack

## Status

Accepted

## Context

The photo organizer crawler is an independent CLI process responsible for:

- Recursive filesystem traversal across source folders
- EXIF metadata extraction from JPEG, RAW (CR2, CR3, ORF, ARW, NEF), and HEIC files
- SHA-256 file hashing for change detection
- SQLite read/write for crawl state tracking
- JSON sidecar file serialization/deserialization (`_folder.json`, `<name>.meta.json`)
- CLI argument parsing (`init`, `run` commands with flags)
- Cross-platform execution (macOS + Windows)

The crawler is spec'd as a stack-agnostic independent process — it communicates with the server exclusively via sidecar files and does not share a runtime with the ASP.NET Core server. Three candidates were evaluated.

### Duplicate Detection Strategy

Duplicate detection (identifying edits of the same photo across formats and folders) is a core crawler responsibility. The approach is tiered by cost:

| Tier | Technique | What it catches |
|------|-----------|-----------------|
| 1 | Filename normalization (strip extension + edit suffixes, lowercase) | Same base name across different extensions/suffixes |
| 2 | EXIF matching (capture timestamp + camera model + serial) | RAW+JPEG pairs shot simultaneously by the same camera |
| 3 | Perceptual hashing (dHash/pHash on image content) | Visual duplicates regardless of name or format |

Tiers 1 and 2 are sufficient for v1. Tier 3 (perceptual hashing) requires decoding RAW image data, which has significant implications for stack choice.

### Candidate Evaluation

#### .NET Console App

| Requirement | Library | Assessment |
|-------------|---------|------------|
| CLI parsing | System.CommandLine | Production-grade |
| EXIF extraction | MetadataExtractor (drewnoakes) | Best-in-class across all stacks; reads CR2, CR3, ORF, ARW, NEF, HEIC metadata natively |
| SHA-256 hashing | System.Security.Cryptography (stdlib) | Built-in |
| SQLite | Microsoft.Data.Sqlite | First-party, reliable |
| JSON | System.Text.Json (stdlib) | Built-in |
| Filesystem traversal | System.IO (stdlib) | Built-in |
| Distribution | Single-file publish (`--self-contained`) | Comparable to Go |
| RAW image decoding (Tier 3) | Magick.NET (requires native LibRaw) | Works but adds native dependency; no pure-managed option |
| Perceptual hashing (Tier 3) | CoenM/ImageHash (via ImageSharp) | Immature; no RAW support without Magick.NET |

**Toolchain overlap**: The server is already .NET 10. One SDK, one CI pipeline, one language. Sidecar schema types can be shared via `PhotoOrganizer.Domain` to prevent drift.

#### Python

| Requirement | Library | Assessment |
|-------------|---------|------------|
| CLI parsing | argparse / click (stdlib/mature) | Good |
| EXIF extraction | exifread / pyexiftool | Adequate; pyexiftool wraps ExifTool for full format coverage |
| SHA-256 hashing | hashlib (stdlib) | Built-in |
| SQLite | sqlite3 (stdlib) | Built-in |
| JSON | json (stdlib) | Built-in |
| Filesystem traversal | os / pathlib (stdlib) | Built-in |
| Distribution | pyinstaller or script | Fragile; adds runtime dependency |
| RAW image decoding (Tier 3) | rawpy (LibRaw wrapper) | Excellent; covers CR2, CR3, ORF, ARW, NEF, RW2 |
| Perceptual hashing (Tier 3) | imagehash + Pillow | Best-in-class; mature, well-maintained |
| HEIC support (Tier 3) | pillow-heif | Good |

**Strongest for Tier 3**, but adds a second language/runtime to the project. Distribution (packaging Python for cross-platform use) is significantly more complex than a .NET single-file publish.

#### Go

| Requirement | Library | Assessment |
|-------------|---------|------------|
| CLI parsing | cobra / flag | Good |
| EXIF extraction | dsoprea/go-exif | More limited than MetadataExtractor or ExifTool |
| SHA-256 hashing | crypto/sha256 (stdlib) | Built-in |
| SQLite | mattn/go-sqlite3 (CGo) | Requires CGo; cross-compilation becomes complex |
| Distribution | Single binary | Excellent |
| RAW image decoding (Tier 3) | None native | Must shell out to ExifTool or use CGo to LibRaw |
| Perceptual hashing (Tier 3) | corona10/goimagehash | Reasonable, but no RAW support |

Go has the weakest ecosystem for this specific task — no native RAW or HEIC image decoding, and the CGo dependency for SQLite complicates cross-compilation.

## Decision

**Core crawler: .NET 10 Console App.**

**Python sub-tool strategy for image-heavy processing steps.**

For v1, the .NET stack covers all requirements cleanly (Tiers 1 and 2 of duplicate detection). When Tier 3 perceptual hashing is needed in a future phase, individual processing steps (e.g. `visual-duplicates`) will be implemented as standalone Python CLI scripts invoked by the .NET crawler via `Process.Start()`. These sub-tools:

- Are self-contained Python scripts with their own `requirements.txt` / virtualenv under `tools/`
- Read and write the same sidecar files and SQLite database as the main crawler
- Require no special IPC — the interface is the filesystem and exit codes
- Can be developed and tested independently

This approach defers the Python runtime dependency until it is actually needed, while keeping a clear migration path.

Key packages for the .NET crawler:

| Package | Purpose |
|---------|---------|
| `System.CommandLine` | CLI argument parsing |
| `MetadataExtractor` | EXIF/IPTC/XMP extraction from all formats |
| `Microsoft.Data.Sqlite` | SQLite state tracking |
| `System.Text.Json` | Sidecar JSON serialization |

## Consequences

**Positive:**
- Single primary toolchain (one SDK, one CI pipeline, one language for 95% of the codebase)
- Sidecar schema types can be shared with `PhotoOrganizer.Domain`
- MetadataExtractor is the strongest EXIF library across all evaluated stacks
- Single-file publish produces a clean, portable artifact

**Accepted tradeoffs:**
- Tier 3 perceptual hashing deferred to a future phase; v1 duplicate detection is limited to filename normalization and EXIF timestamp matching
- When Tier 3 is implemented, Python becomes a runtime dependency for that processing step
- Python sub-tools add operational complexity (virtualenv management, Python version pinning)

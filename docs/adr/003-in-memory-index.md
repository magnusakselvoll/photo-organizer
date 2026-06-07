# ADR 003: Progressive Randomized In-Memory Index (and the Warm-Cache Reversal)

## Status

Accepted

## Context

The server needs to enumerate the full photo library for browse-grid and slideshow requests. Photos live on a Synology NAS over SMB. A synchronous filesystem walk at startup was measured at ~56 seconds for a medium-sized library — unacceptable for server cold-start time.

Three indexing strategies were considered:

| Strategy | Cold-start | Browse availability | Freshness |
|----------|-----------|---------------------|-----------|
| Blocking walk at startup | Long (56 s measured) | Only after full walk | Always current |
| Query filesystem per request | No startup delay | Immediate | Always current, but per-request cost high on SMB |
| Progressive background index | Near-zero startup | Immediately (grows as indexing runs) | Rebuilt from sidecars each startup |

A fourth variant — a **persistent warm cache** — was also evaluated. After a full index build, the cache would serialize the index to disk so subsequent restarts could load it instantly instead of re-walking the filesystem.

### The warm-cache reversal

The warm cache was implemented in commit `2cfb5cd` (`RandomizedSidecarIndexer` + `PhotoIndexCache`). In practice it introduced two problems:

1. **Staleness**: the cache did not know about photos added or deleted between restarts, producing phantom or missing entries until the next full recrawl.
2. **Footgun**: `PhotoIndex.Clear()` / `InvalidateCacheAsync` was called in several places to force a fresh read, but callers could not reliably trigger a full re-index; races produced a permanently empty or partial index after manual invalidation.

Both issues were resolved by removing the warm cache entirely (commit `d5d57f5`) and then removing `InvalidateCacheAsync`/`PhotoIndex.Clear()` (commit `09aadff`): the index is always rebuilt from sidecars at startup. The SMB walk penalty is acceptable because the progressive design means the server is usable within seconds even while indexing is ongoing.

## Decision

**The server builds a thread-safe in-memory index progressively at startup via a `BackgroundService`, always starting from scratch.**

Key properties of `RandomizedSidecarIndexer` (`src/PhotoOrganizer.Infrastructure/Indexing/RandomizedSidecarIndexer.cs`):

- **Lazy / progressive**: photo entries appear in `PhotoIndex` as each sidecar is read; API requests return whatever is indexed so far without waiting.
- **Randomized**: directories are enqueued and dequeued in random order (Fisher-Yates shuffle + random insertion into the pending list). This ensures a representative cross-section of the library is indexed first rather than one folder dominating the early index.
- **Parallel workers**: `PhotoOrganizerSettings.Indexing.MaxParallelism` parallel workers drain a shared pending queue, balancing SMB round-trip latency with CPU usage.
- **Completion signal**: `PhotoIndex.MarkComplete()` / `PhotoIndex.IsComplete` lets the browse page stop polling `GET /api/index/status` once the full index is available.
- **No caching**: there is no on-disk cache; restarting the server always rebuilds from sidecars. **Do not add `PhotoIndex.Clear()`, `InvalidateCacheAsync`, or a warm-cache path** — these patterns were tried and removed because their staleness and race conditions outweighed the startup-time benefit.

`PhotoIndex` (`src/PhotoOrganizer.Infrastructure/Indexing/PhotoIndex.cs`) is backed by `ConcurrentDictionary<Guid, Photo>` and `ConcurrentDictionary<string, SourceFolder>`. Readers take a point-in-time snapshot (`SnapshotPhotos()`) that does not block indexer workers.

## Consequences

**Positive:**
- Server is usable within seconds of startup regardless of library size
- Each startup surfaces a randomized spread of photos, so the grid feels populated quickly even for multi-thousand-photo libraries
- Eliminating the warm cache removes an entire class of staleness bugs and the `Clear()`/`InvalidateCacheAsync` footgun
- Simple mental model: "the index is whatever sidecars currently exist on disk"

**Accepted tradeoffs:**
- Every server restart re-reads all sidecars from the NAS; on large libraries this takes minutes for full indexing (but browse is available throughout)
- There is no way to add a photo to the index without a crawler run or a server restart (the index is read-only at runtime)
- The live-update polling (`BrowsePage` polls `GET /api/index/status` every 4 s) is only relevant during the initial indexing window, not for photo additions after the index is complete

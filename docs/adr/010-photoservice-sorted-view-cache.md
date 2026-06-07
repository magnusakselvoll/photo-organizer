# ADR 010: PhotoService Sorted-View Cache

## Status

Accepted

## Context

Every `GET /api/photos` request caused `PhotoService.GetPhotosAsync` to:

1. Take a full snapshot of the in-memory index (`[.. _photos.Values]`).
2. Apply a displayability filter (per-photo extension check) over all N photos.
3. Sort the entire set descending by `CapturedAt ?? FileModifiedAt` — O(N log N).
4. Walk the sorted list to skip past the cursor and take 50 items.

On a 10k–100k photo library this per-request rebuild produced multi-second latency for every browse-page load and every infinite-scroll page fetch, causing the browse page to show "Loading…" for several seconds even when the index was fully built (issue #68).

The underlying cause is that the expensive step (steps 1–3) is filter-independent: it produces the same sorted list regardless of which narrowing filters the caller applies. Only steps 4+ (folder/type/filename/date/dedup) vary per request.

### Relationship to ADR 003 (warm-cache reversal)

ADR 003 reversed a **persistent warm cache** — an on-disk serialisation of the entire `PhotoIndex` that survived server restarts. It was removed because:
- It became stale between restarts without a reliable way to detect staleness.
- `PhotoIndex.Clear()` / `InvalidateCacheAsync` introduced a race-prone invalidation footgun.

The cache introduced here is categorically different:
- **In-memory only** — it does not survive restarts; no on-disk serialisation.
- **Automatically invalidated** — keyed on `PhotoIndex.Version`, a monotonic counter that `PhotoIndex.AddPhoto` increments atomically. When a photo is added the counter increments; the stale cache entry is ignored on the next request and rebuilt from the live index.
- **A derived view, not a source of truth** — the cache holds a filtered+sorted projection of `PhotoIndex`, not a copy of it. `PhotoIndex` remains the sole authority; the cache is discarded and rebuilt whenever the version changes.
- **No `Clear()` / `InvalidateCacheAsync`** — invalidation is implicit via the version counter.

## Decision

The singleton `PhotoService` maintains a `CachedView` field containing the version number and the sorted, displayable-filtered list of photos. On each `GetPhotosAsync` call:

1. Read `repository.Version` **before** snapshotting (conservative: never label a snapshot with a version it doesn't reflect; may rebuild unnecessarily if the version increments mid-call, but never serves stale data as fresh).
2. If `_cache?.Version == version`, skip the snapshot and sort — return the cached list directly.
3. Otherwise take a fresh snapshot, filter for displayability, sort, store as the new `CachedView`, and return it.

Per-request narrowing (folder, type, filename, date bounds, dedup) is applied as order-preserving `Where` passes over the cached sorted list. `Volatile.Read/Write` ensures the reference is exchanged atomically without a lock; concurrent cache rebuilds during the indexing window are benign (last writer wins, all candidates produce equivalent results for the same version).

Key files:
- `src/PhotoOrganizer.Infrastructure/Indexing/PhotoIndex.cs` — `Version` property + `AddPhoto` increment.
- `src/PhotoOrganizer.Domain/Interfaces/IPhotoRepository.cs` — `long Version { get; }` added to interface.
- `src/PhotoOrganizer.Infrastructure/Indexing/IndexPhotoRepository.cs` — delegates `Version` to `PhotoIndex`.
- `src/PhotoOrganizer.Infrastructure/Services/PhotoService.cs` — `CachedView` record, `GetSortedDisplayableAsync`, `ApplyNarrowing`.

## Consequences

**Positive:**
- First-page browse latency drops from seconds to milliseconds on a warm index; only O(N) narrowing work remains per request.
- Cache invalidates automatically during the initial indexing window as `AddPhoto` bumps the version; once the background indexer finishes the version is stable and every request is a cache hit.
- No `Clear()`, no `InvalidateCacheAsync`, no on-disk state — the footguns that prompted the ADR 003 reversal are not reintroduced.

**Accepted tradeoffs:**
- A photo added to the index is not visible in browse results until the first request that reads the new version (sub-millisecond in practice for the steady-state case; bounded by the next `AddPhoto` call's version bump during initial indexing).
- `GetPhotoByIdAsync` (rare detail-view path) still takes a full snapshot to populate sibling versions. This is not on the hot path and is left unchanged.

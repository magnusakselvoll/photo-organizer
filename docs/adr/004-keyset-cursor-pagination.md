# ADR 004: Keyset Cursor Pagination for the Browse Grid

## Status

Accepted

## Context

The browse grid needs to load photos in pages as the user scrolls. The in-memory index is append-only during a startup index build: new photos are prepended (newest-first sort) as the background indexer runs. Two standard pagination strategies were evaluated:

| Strategy | Stability under concurrent prepends | Implementation complexity |
|----------|-------------------------------------|---------------------------|
| Offset (`SKIP n TAKE m`) | Poor — offset shifts when items are prepended; the same photo can appear on two consecutive pages or a photo can be skipped entirely | Simple |
| Keyset cursor | Stable — cursor points to a specific item; the next page starts immediately after it regardless of prepends | Moderate |

Offset pagination was kept for the **slideshow** endpoint (`/api/photos?page=N&pageSize=M`), which is a single-pass, single-user flow where prepend instability is acceptable and the simpler API is preferable.

## Decision

**`GET /api/photos` supports two pagination modes; the browse grid uses keyset cursor pagination.**

### Keyset path (`cursor` + `limit` params)

- Sort order: `CapturedAt ?? FileModifiedAt` descending, `Id` descending as a tiebreaker (the `Id` tiebreaker guarantees cursor determinism when two photos share the same effective timestamp).
- Cursor format: `base64url("{effectiveTimestampUtcTicks}_{idN}")` — human-readable in logs, URL-safe, no padding.
- Implementation: `PhotoService.EncodeCursor` / `DecodeCursor` / `EffectiveTicks` in `src/PhotoOrganizer.Infrastructure/Services/PhotoService.cs`.
- `PhotoPageDto.NextCursor` is `null` on the last page (fewer results than `limit`), signalling to the frontend that infinite scroll should stop.

### Offset path (`page` + `pageSize` params — legacy)

- Used by the slideshow endpoint and retained for future callers.
- `PhotoPageDto.NextCursor` is `null` on all offset responses.

### Frontend integration

The `useInfinitePhotos` hook (`src/PhotoOrganizer.Web/src/hooks/useInfinitePhotos.ts`) drives the grid: it appends pages using the `NextCursor` value from each response and calls `mergeNewest()` to prepend new arrivals detected by the index-status poll without resetting the scroll position.

## Consequences

**Positive:**
- Infinite scroll is stable: no photo appears twice or disappears when the index grows during a session
- The cursor is stateless — no server-side session required; the cursor can be bookmarked or passed between clients
- The `Id` tiebreaker guarantees deterministic pages even for libraries where many photos share the same capture-time resolution (e.g. burst shots)

**Accepted tradeoffs:**
- Keyset pagination cannot jump to an arbitrary page number (e.g. "go to page 50") — only forward-sequential traversal is supported
- Cursor encoding/decoding adds a thin abstraction layer; malformed cursors are silently treated as "start of list" rather than an error
- Filters compose with the cursor but the cursor is not filter-aware: changing filters while mid-scroll invalidates the current cursor position (the frontend resets the cursor on filter change)

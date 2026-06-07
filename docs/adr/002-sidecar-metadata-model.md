# ADR 002: Sidecar JSON Files as the Metadata Source of Truth

## Status

Accepted

## Context

The photo organizer needs to store per-photo metadata (capture time, duplicate group, preferred flag, crawl step history, tags) and per-folder metadata (label, type, enabled flag). Several storage models were considered:

| Approach | Portability | Failure modes |
|----------|-------------|---------------|
| Central relational DB (SQLite/Postgres) | Poor — single file tightly coupled to the installation | DB corruption, path breakage on NAS remap; all metadata lost if DB is deleted |
| Central SQLite alongside the server | Moderate — one file to back up | Harder to move or split photo libraries; crawler and server would share a write-sensitive file |
| Sidecar JSON files co-located with photos | Excellent — travels with the photo library | Individual file corruption is isolated; no single point of failure |
| Embedded index (e.g., one `.catalog.json` per root) | Moderate | Still decoupled from individual files; large files hard to merge |

The photos live on a Synology NAS accessed over SMB. NAS remaps, partial moves, and folder reorganisations are expected operational events. Metadata must survive any of these without manual recovery.

Additionally, the project has two independent processes: the **crawler** (runs on demand, writes metadata) and the **server** (always-on, reads metadata). Sharing a write-sensitive database between them would require connection multiplexing and careful serialization; a sidecar-per-file model requires no coordination at all.

## Decision

**All photo and folder metadata lives in co-located JSON sidecar files.**

- **Folder-level**: `_folder.json` in each source folder root (`schemas/folder.schema.json`)
- **Photo-level**: `<name>.<ext>.meta.json` beside each photo file — the full filename including extension is used so `IMG_1234.orf` and `IMG_1234.jpg` each get a distinct sidecar (`schemas/photo-meta.schema.json`)

The **crawler's SQLite DB** (`crawl_log`) holds only operational state — hashes, mod-times, step-run records — used for incremental crawl efficiency. It is never the source of truth for photo metadata, and the server accesses it read-only. Deleting it forces a full recrawl but loses no metadata.

The **crawler↔server integration contract** is: filesystem + exit codes. No IPC, no shared library, no network protocol.

**Schema evolution rule**: new optional fields may be added freely; existing fields must not be removed or renamed; readers must tolerate unknown fields (`JsonSerializer` with `PropertyNameCaseInsensitive = true` and no `JsonExtensionData` rejection). Version compatibility tests live in `tests/PhotoOrganizer.Application.Tests/SidecarsTests.cs`.

**Atomic writes**: sidecars are written via temp-file-then-rename (`File.Move(..., overwrite: true)`) so an interrupted write never produces a partial or zero-byte file. See also `src/PhotoOrganizer.Crawler/Sidecars/JsonSidecarStore.cs`.

## Consequences

**Positive:**
- Photo metadata is portable — moving the library to a different NAS or path requires no migration
- Corruption is isolated to a single file; the rest of the library is unaffected
- The crawler and server require no shared database or connection pool
- Metadata is human-readable and inspectable with any text editor
- A photo can be deleted, and its sidecar naturally disappears with it

**Accepted tradeoffs:**
- Querying across the full library (filtering, sorting) requires an in-memory index built at server startup (see ADR 003); no ad-hoc SQL is possible
- Writing a sidecar during an ongoing crawl requires file locking to prevent torn writes
- Schema changes require a migration strategy for existing sidecar files in the field

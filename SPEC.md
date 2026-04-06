# Photo Organizer — Technical Specification

## 1. Goals

- Aggregate photos from multiple source folders (local Windows drives + Synology NAS)
- Support both originals and edited versions; prefer edits for display
- Detect and collapse duplicates (primarily by file name)
- Serve a React UI for browsing and slideshow display
- Store all metadata in sidecar files co-located with photos — portable and NAS-friendly
- Be extensible toward auto-tagging, GPS location, and face recognition

## 2. Architecture

Follows Clean Architecture, inspired by [photo-booth-take-two](https://github.com/magnusakselvoll/photo-booth-take-two).

```
Domain → Application → Infrastructure → Server
                                 ↑
                            PhotoOrganizer.Web (React)

Crawler (independent process, any stack)
  └── communicates via sidecar files + SQLite DB
```

### Layers

| Project | Responsibility |
|---------|---------------|
| `PhotoOrganizer.Domain` | Entities, value objects, repository interfaces, domain exceptions |
| `PhotoOrganizer.Application` | Use cases, DTOs, service interfaces, event contracts |
| `PhotoOrganizer.Infrastructure` | File system access, sidecar read/write, indexing, duplicate detection |
| `PhotoOrganizer.Server` | ASP.NET Core host, API endpoints, middleware, static file serving |
| `PhotoOrganizer.Web` | React + TypeScript frontend, Vite build |
| `Crawler` | Independent CLI process — discovers photos, runs processing pipeline, writes sidecars |

### Key Interfaces (Domain)

- `IPhotoRepository` — list, find, serve photos
- `IFolderRepository` — discover and read source folders (via `_folder.json` sidecars)
- `ISidecardStore` — read/write sidecar metadata
- `IDuplicateDetector` — identify duplicate photos across folders

## 3. Source Folders

Folders are set up via the crawler's `init` command (see §6.2) and discovered by the server by scanning `ScanRoots` for `_folder.json` sidecar files. There is no central folder registry.

Each source folder is described by its `_folder.json` sidecar:

| Property | Description |
|----------|-------------|
| `label` | Human-readable name |
| `type` | `originals` or `edits` |
| `enabled` | Whether to include in indexing |

## 4. Photo Model

```
Photo
  Id          : Guid
  FilePath    : string          # Absolute path
  FileName    : string          # Without extension
  CapturedAt  : DateTimeOffset? # From EXIF or file creation time
  FolderType  : originals | edits
  DuplicateGroupId : Guid?      # Links photos sharing the same logical image
  IsPreferred : bool            # True for the display-preferred version
  Tags        : string[]        # Future: auto or manual tags
```

## 5. Sidecar File Format

Metadata lives in files co-located with the photos. This keeps everything portable and avoids a central database as a single point of failure.

Formal JSON Schema definitions for both sidecar formats are in `schemas/`. The examples below are illustrative; the schemas are the canonical reference.

Sidecar files must be written in a backwards-compatible way: new optional fields may be added freely, but existing fields must not be removed or renamed. Readers must tolerate unknown fields without failing.

### Folder-level sidecar: `_folder.json`

Placed in the root of each source folder.

```json
{
  "version": 1,
  "label": "Holiday 2023",
  "type": "mixed",
  "enabled": true
}
```

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `version` | Yes | integer | Schema version, starts at 1 |
| `label` | Yes | string | Human-readable folder name |
| `enabled` | Yes | boolean | Whether to include in indexing |
| `type` | No | `originals` \| `edits` \| `mixed` | Content type; defaults to `mixed` |

### File-level sidecar: `<photoname>.meta.json`

One per photo file, same directory.

```json
{
  "version": 1,
  "capturedAt": "2023-07-14T18:30:00+02:00",
  "duplicateGroupId": "550e8400-e29b-41d4-a716-446655440000",
  "isPreferred": true,
  "tags": ["holiday", "beach"],
  "crawlSteps": {
    "metadata": { "version": 1, "completedAt": "2025-03-15T10:00:00Z" },
    "duplicates": { "version": 1, "completedAt": "2025-03-15T10:00:05Z" }
  }
}
```

The `crawlSteps` map records which processing step versions have run on this file and when they completed, enabling selective recrawling. The `version` field is required; all other fields are optional.

Sidecar files are created lazily on first write. Absence means default/unknown values.

## 6. Crawler

### 6.1 Overview

The crawler is an independent CLI process (stack-agnostic — could be Python, .NET, or any language). It:

- Discovers photos in source folders and runs them through a processing pipeline
- Writes metadata results to per-photo sidecar files
- Tracks operational state (file hashes, step versions) in a local SQLite database
- Runs periodically for incremental updates and can be triggered manually for full or targeted recrawls
- Is the sole mechanism for adding new source folders

### 6.2 Init Mode (Folder Setup)

```
crawler init <folder-path> [--label "..."] [--type originals|edits|mixed] [--enabled true|false]
```

- Prompts interactively for any parameters not supplied as CLI arguments
- Writes `_folder.json` to the folder root
- Immediately runs a full crawl of that folder
- The server discovers the folder automatically by scanning `ScanRoots` for `_folder.json` files

### 6.3 Processing Pipeline

A crawl executes an ordered list of **processing steps** over discovered photos.

Each step declares:
- `name` — unique identifier (e.g. `"metadata"`, `"duplicates"`, `"faces"`)
- `version` — integer; incrementing triggers a targeted recrawl of that step
- `dependsOn` — optional list of step names that must have run first

Steps are executed in dependency order. After each step completes on a file, the step name and version are written to the file's sidecar under `crawlSteps`.

**Built-in steps:**

| Step | Version | Depends on | Description |
|------|---------|------------|-------------|
| `metadata` | 1 | — | Extract EXIF data (capturedAt, dimensions, GPS), write to sidecar |
| `duplicates` | 1 | `metadata` | Group photos by normalised filename, assign `duplicateGroupId`, mark preferred version |

**Duplicate detection algorithm** (within the `duplicates` step):
1. Index all photos across all enabled folders
2. Normalise file name: strip extension, strip known edit suffixes (`_edit`, `_retouched`, `-hdr`), lowercase
3. Group photos sharing the same normalised name → one `duplicateGroupId`
4. Within a group, prefer `edits` folder type over `originals`
5. Store `duplicateGroupId` and `isPreferred` in each photo's sidecar

### 6.4 Crawl Modes

| Mode | Trigger | What it does |
|------|---------|--------------|
| **Init** | `crawler init <path>` | Write `_folder.json`, then full-crawl that folder |
| **Full** | `crawler run --mode full` | Scan all folders, run all steps on all files |
| **Incremental** | `crawler run` (default) / scheduled | Scan for new/changed/deleted files; run all steps on changed files |
| **Targeted** | `crawler run --mode targeted --step <name>` | Run a specific step (and its dependents) on all files where the step hasn't run or has an older version |

### 6.5 Change Detection (Tiered)

For each file encountered during a crawl:
1. Compare file's last-modified timestamp against the value stored in the crawler DB
2. If mod-time is unchanged → skip (no reprocessing needed)
3. If mod-time changed → compute SHA-256 hash and compare against stored hash
4. If hash differs → file has changed → re-run all steps
5. If hash matches → spurious mod-time change (e.g. backup restore) → update stored mod-time only, skip processing

### 6.6 Crawler Database (SQLite)

The crawler maintains a local SQLite database for operational state. This is separate from the sidecar files (which are the source-of-truth metadata) and from the server's domain.

**Tables**: `schema_version`, `crawled_files`, `step_runs`, `crawl_log`.
Canonical DDL with column definitions: `schemas/crawler.sql`.
Migration policy: `schemas/README.md` section "Versioning Policy".

### 6.7 Deleted File Handling

- If a previously-indexed file is no longer on disk, mark it as deleted in the DB (`deleted = 1`)
- The corresponding sidecar is left in place (it may still be valid if the file was moved)
- Orphaned sidecar cleanup is opt-in via configuration

### 6.8 Configuration

Crawler config (standalone file, format to be determined by implementation):

```json
{
  "Crawler": {
    "DatabasePath": "./crawler.db",
    "ScheduleIntervalMinutes": 60,
    "OrphanedSidecarCleanup": false
  }
}
```

## 7. API Endpoints

Base path: `/api`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/folders` | List discovered source folders |
| GET | `/photos` | List photos (supports filtering, pagination) |
| GET | `/photos/{id}` | Get photo metadata |
| GET | `/photos/{id}/image` | Serve the photo file |
| GET | `/slideshow/next` | Next photo for slideshow (respects duplicate preference) |
| GET | `/config` | Runtime configuration |
| POST | `/crawler/start` | Trigger a crawl (`{ "mode": "full\|incremental\|targeted", "step": "..." }`) |
| GET | `/crawler/status` | Current crawl state (idle/running, progress, last run info) |

### Query Parameters for `/photos`

| Param | Values | Description |
|-------|--------|-------------|
| `folder` | folder id | Filter by source folder |
| `type` | `originals`, `edits`, `all` | Filter by folder type (default: `all`) |
| `deduplicated` | `true`, `false` | Show only preferred version per group (default: `true`) |
| `page` | int | Pagination page |
| `pageSize` | int | Items per page |

## 8. Slideshow

- Cycles through photos indefinitely
- Applies Ken Burns pan/zoom animation per photo
- Cross-fade transition between photos (≈500 ms)
- Only shows the preferred (deduplicated) version of each photo
- Configurable transition interval

## 9. Frontend

- React + TypeScript, built with Vite
- pnpm for package management
- Routes: `/` browse grid, `/slideshow` full-screen slideshow, `/photo/:id` detail view
- API client in `src/api/client.ts`; shared types in `src/api/types.ts`
- Development proxy: frontend at `:6173`, backend at `:6192`

## 10. Configuration

Stored in `appsettings.json` (gitignored for personal paths). Example:

```json
{
  "PhotoOrganizer": {
    "ScanRoots": [
      "D:\\Photos",
      "\\\\NAS\\Photos"
    ],
    "Slideshow": {
      "IntervalSeconds": 8,
      "TransitionMs": 500
    }
  }
}
```

`ScanRoots` are paths the server scans recursively for `_folder.json` files to discover managed source folders.

## 11. Non-Goals (for now)

- Cloud sync or remote storage
- Authentication / multi-user
- Video support
- Printing
- Social sharing

## 12. Future Extension Points

The architecture is intentionally open to:

- **Auto-tagging** — implement an `autotag` crawl step; run `crawler run --mode targeted --step autotag` to tag all photos
- **GPS / location** — EXIF extraction (in `metadata` step), reverse geocoding as a separate `location` step
- **Face recognition** — implement a `faces` step with `dependsOn: ["metadata"]`; targeted recrawl adds faces to existing photos without reprocessing everything
- **Additional crawl steps** — any future enrichment follows the same pattern: new step + `crawler run --mode targeted --step <name>`
- **Mobile app** — the REST API is the contract; any client can consume it

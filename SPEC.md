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
```

### Layers

| Project | Responsibility |
|---------|---------------|
| `PhotoOrganizer.Domain` | Entities, value objects, repository interfaces, domain exceptions |
| `PhotoOrganizer.Application` | Use cases, DTOs, service interfaces, event contracts |
| `PhotoOrganizer.Infrastructure` | File system access, sidecar read/write, indexing, duplicate detection |
| `PhotoOrganizer.Server` | ASP.NET Core host, API endpoints, middleware, static file serving |
| `PhotoOrganizer.Web` | React + TypeScript frontend, Vite build |

### Key Interfaces (Domain)

- `IPhotoRepository` — list, find, serve photos
- `IFolderRepository` — enumerate and configure source folders
- `ISidecardStore` — read/write sidecar metadata
- `IDuplicateDetector` — identify duplicate photos across folders

## 3. Source Folders

Each source folder is configured with:

| Property | Description |
|----------|-------------|
| `path` | Absolute path on disk or UNC path to NAS |
| `type` | `originals` or `edits` |
| `label` | Human-readable name |
| `enabled` | Whether to include in indexing |

Folder configuration is stored in a folder-level sidecar file (see §5).

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

### Folder-level sidecar: `_folder.json`

Placed in the root of each source folder.

```json
{
  "label": "Holiday 2023",
  "type": "edits",
  "enabled": true
}
```

### File-level sidecar: `<photoname>.meta.json`

One per photo file, same directory.

```json
{
  "capturedAt": "2023-07-14T18:30:00+02:00",
  "duplicateGroupId": "550e8400-e29b-41d4-a716-446655440000",
  "tags": ["holiday", "beach"]
}
```

Sidecar files are created lazily on first write. Absence means default/unknown values.

## 6. Duplicate Detection

Primary signal: file name (without extension and without known edit suffixes like `_edit`, `_retouched`, `-hdr`).

Algorithm:
1. Index all photos across all configured folders
2. Normalise file name: strip extension, strip known edit suffixes, lowercase
3. Group photos sharing the same normalised name → one `duplicateGroupId`
4. Within a group, prefer `edits` folder type over `originals`
5. Store `duplicateGroupId` in each photo's sidecar

The preferred photo in each group is the one shown in slideshows and default browsing.

## 7. API Endpoints

Base path: `/api`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/folders` | List configured source folders |
| GET | `/photos` | List photos (supports filtering, pagination) |
| GET | `/photos/{id}` | Get photo metadata |
| GET | `/photos/{id}/image` | Serve the photo file |
| GET | `/slideshow/next` | Next photo for slideshow (respects duplicate preference) |
| GET | `/config` | Runtime configuration |

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
- Development proxy: frontend at `:5173`, backend at `:5192`

## 10. Configuration

Stored in `appsettings.json` (gitignored for personal paths). Example:

```json
{
  "PhotoOrganizer": {
    "SourceFolders": [
      { "path": "D:\\Photos\\Originals", "type": "originals", "label": "Camera Roll", "enabled": true },
      { "path": "\\\\NAS\\Photos\\Edits", "type": "edits", "label": "Lightroom Exports", "enabled": true }
    ],
    "Slideshow": {
      "IntervalSeconds": 8,
      "TransitionMs": 500
    }
  }
}
```

## 11. Non-Goals (for now)

- Cloud sync or remote storage
- Authentication / multi-user
- Video support
- Printing
- Social sharing

## 12. Future Extension Points

The architecture is intentionally open to:

- **Auto-tagging** — plug in a tag provider behind `ITaggingService`
- **GPS / location** — EXIF extraction, reverse geocoding
- **Face recognition** — person clustering behind `IFaceRecognitionService`
- **Mobile app** — the REST API is the contract; any client can consume it

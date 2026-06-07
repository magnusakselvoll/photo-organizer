# Claude Working Instructions — Photo Organizer

## Project Context

Personal photo management app. See [README.md](README.md) for the user-facing overview and [SPEC.md](SPEC.md) for detailed technical requirements.

Reference implementation to draw patterns from: https://github.com/magnusakselvoll/photo-booth-take-two

## Architecture Conventions

- Architecture layers and project responsibilities: see SPEC.md section 2
- **Namespace prefix**: `PhotoOrganizer.*`
- **Backend**: .NET 10, ASP.NET Core, C#; **Frontend**: React + TypeScript, Vite, pnpm
- **Ports (dev)**: Backend `:6192`, Frontend `:6173`
- **Image transcoding**: HEIC/HEIF files are transcoded to JPEG on the fly in the `/api/photos/{id}/image` endpoint using `Magick.NET-Q8-AnyCPU` (`MagickImageTranscoder` in `src/PhotoOrganizer.Infrastructure/Imaging/`). The `IImageTranscoder` interface lives in `src/PhotoOrganizer.Domain/Interfaces/`. No caching — each request transcodes fresh (see ADR 006).
- **Displayability**: `DisplayableImageFormats` (`src/PhotoOrganizer.Domain/DisplayableImageFormats.cs`) is the single source of truth for which formats are servable. `PhotoService.ApplyFilters` filters every grid and slideshow listing to displayable photos only (browser-native + transcodable). Non-displayable files (RAW, bare TIFF) are never served in listings but remain downloadable via the version panel's `/api/photos/{id}/image` endpoint. `MagickImageTranscoder` and `DuplicatesStep` both delegate to this helper (see ADR 007).
- **Discoverability**: `SupportedPhotoExtensions` (`src/PhotoOrganizer.Domain/SupportedPhotoExtensions.cs`) is the single source of truth for which extensions the crawler (`FileDiscoverer`) and indexer (`RandomizedSidecarIndexer`) recognise as photos. Constructed as `DisplayableImageFormats.AllDisplayableExtensions ∪ {RAW/TIFF}`; `displayable ⊆ discoverable` is pinned by a unit test in `tests/PhotoOrganizer.Application.Tests/SupportedPhotoExtensionsTests.cs` (see ADR 007).
- **Browse grid pagination**: `/api/photos` supports two modes. (1) **Keyset cursor** (`cursor` + `limit`, used by the grid): stable, non-overlapping pages ordered newest-first; cursor encodes `(effectiveTimestamp UTC ticks, id)` as base64url — `PhotoService.EncodeCursor`/`DecodeCursor`. (2) **Offset** (`page` + `pageSize`): legacy path, kept for slideshow. `PhotoPageDto.NextCursor` is `null` on offset responses (see ADR 004).
- **Browse grid virtualizer**: `@tanstack/react-virtual` (`useVirtualizer`) windows rows. Only rows within the visible viewport ± 5-row overscan are in the DOM. Column count is derived from container width via `ResizeObserver` using `columnsForWidth()` in `src/PhotoOrganizer.Web/src/hooks/useInfinitePhotos.ts`. The `react-hooks/incompatible-library` lint warning on `useVirtualizer` is expected — it's a React Compiler note; we don't use the compiler. While scrolling, `PhotoGrid` shows a floating month/year pill (`.scrub-date`) that fades out on idle — computed from `PhotoDto.effectiveDate` (the sort key: `CapturedAt ?? FileModifiedAt`, exposed by the backend).
- **Live index updates**: `BrowsePage` polls `GET /api/index/status` every 4 s; when the photo count grows it calls `getPhotos` with no cursor to fetch the newest arrivals and prepends them via `useInfinitePhotos.mergeNewest()`. Polling stops when `complete: true` (see ADR 003 for the progressive indexer and the warm-cache reversal).
- **Browse filters**: `/api/photos` accepts `folder`, `type`, `deduplicated`, `fileName` (case-insensitive substring on the filename including extension), `dateFrom`/`dateTo` (inclusive day-granularity bounds on effective date = `CapturedAt ?? FileModifiedAt`, compared in UTC). All filters are optional and applied in-memory by `PhotoService.ApplyNarrowing` over the pre-sorted cached list before pagination, so they compose safely with the keyset cursor. The frontend (`BrowsePage.tsx`) reads/writes filter state via `useSearchParams` (react-router-dom v7) so filters are URL-encoded, deep-linkable, and survive refresh. Filename is debounced 300 ms before hitting the URL/API. `InfinitePhotosFilters.filterKey` must include every filter field or re-fetch won't trigger on change.
- **PhotoService sorted-view cache**: `PhotoService` (singleton) caches the full displayable+sorted list keyed by `PhotoIndex.Version` — a monotonic counter bumped on every `AddPhoto`. Narrowing filters run as O(N) passes over the cached list per request. The cache is in-memory only, auto-invalidated by version change, and carries none of the staleness/footgun risks of the reversed warm cache (see ADR 010).
- **Deferred (issue 39 follow-ups)**: tags filter (waiting for the crawler to write tags into `.meta.json` sidecars — the field is already read and mapped end-to-end, only the filter UI is missing).
- **Crawler**: .NET 10 Console App (see ADR 001); key packages: `System.CommandLine`, `MetadataExtractor`, `Microsoft.Data.Sqlite`; pipeline/step framework with tiered change detection (see ADR 009).
  - **Python sub-tool strategy**: Image-heavy steps as standalone Python CLIs under `tools/`, invoked via `Process.Start()` — share sidecar files and SQLite DB, no special IPC (see ADR 001).
  - **Folder discovery**: `ScanRoots` are searched recursively for `_folder.json` files; each such folder becomes an independent crawl unit (`CrawlTargetResolver`). Photos belong to the nearest ancestor unit; photos not under any unit are skipped. Mirrors `FileSystemFolderRepository` on the server side.
  - **Process launch**: crawler is started via `ProcessStartInfo.ArgumentList` (not `Arguments`) so user-supplied fields cannot re-tokenize into extra CLI arguments (see ADR 005).
- **Security — `POST /api/crawler/start`**: requires the `X-Requested-With` header (CSRF guard); `Mode`/`Step` validated against `StartCrawlValidation` (`src/PhotoOrganizer.Application/Crawler/StartCrawlValidation.cs`) — valid modes: `full`, `incremental`, `targeted`; valid steps: `metadata`, `duplicates`. Returns 403 if header absent, 400 if invalid (see ADR 005).
- **Security — bind address**: server binds loopback-only by default (dev: `localhost:6192` via `launchSettings.json`; published: Kestrel default `localhost:5000`). Do not bind to `0.0.0.0` without auth + rate limiting (see ADR 005).

## Patterns to Follow

- **Repository pattern** for all data access — domain defines interfaces, infrastructure implements them (see ADR 008)
- **Pluggable providers** — camera/storage/detection strategies behind interfaces, easy to swap (see ADR 008)
- **Sidecar files** (`_folder.json`, `<name>.<ext>.meta.json`) for all metadata — no database required; photo sidecars use the full filename including extension so RAW+JPEG pairs in the same folder each get a distinct sidecar (see ADR 002)
- **Lazy loading** — index photo metadata progressively in a background service rather than blocking at startup (see ADR 003)
- **Thread-safe file access** — use semaphore locks when reading/writing shared state
- **Centralized package versions** — `Directory.Packages.props`; no version numbers inside individual `.csproj` files

## Key Files to Know

| File | Role |
|------|------|
| `SPEC.md` | Source of truth for requirements, data models, API design |
| `global.json` | Pins the .NET 10 SDK version |
| `Directory.Build.props` | Global MSBuild properties for all projects |
| `Directory.Packages.props` | Centralized NuGet package versions |
| `PhotoOrganizer.slnx` | Solution file (XML format) |
| `src/PhotoOrganizer.Domain/` | Entities and interfaces — add here first |
| `src/PhotoOrganizer.Infrastructure/` | File system, sidecar parsing, indexing |
| `src/PhotoOrganizer.Server/Program.cs` | App bootstrap and middleware registration |
| `src/PhotoOrganizer.Web/src/api/` | TypeScript API client and shared types |
| `appsettings.json` | Local config (gitignored — contains personal paths) |

## GitHub Flow

Always use GitHub Flow when working on issues:

1. **Create a feature branch** before making any file edits — no exceptions:
   - First fetch and checkout latest main: `git fetch origin && git checkout main && git pull`
   - Branch name format: `<issue-number>-<short-description>` (e.g. `42-duplicate-detection`)
   - Create and checkout the branch: `git checkout -b 42-duplicate-detection`
   - **Do not read or edit any files until the branch is created.** This prevents accidentally committing to main (direct pushes to main are blocked).
   - **Only use worktrees** when explicitly asked (e.g. "use a worktree", "work on several issues in parallel")

2. **Commit** changes with descriptive messages:
   - Write commit messages as plain double-quoted strings — no heredocs, no `$()` substitution
   - Each `-m` value must be a single line — newlines inside a `-m` string cause errors
   - For multi-line messages use separate `-m` flags: `git commit -m "title" -m "body line"`

3. **Push** the branch and **create a PR**:
   - **Ask before creating the PR** — the user may have feedback based on console output or code
   - Reference the issue in the PR body with `Closes #<issue-number>` to auto-close on merge
   - Pass `--title` as a plain string; pass `--body` via a `$(cat <<'EOF' ... EOF)` heredoc — this prevents zsh from expanding backticks or `$()` inside the body as shell commands
   - Always pass `--head <branch-name> --base main` to `gh pr create`

4. **Merge** after review (squash merge preferred for clean history)

5. **Clean up** after the user confirms a PR is merged:
   - `git fetch origin && git checkout main && git pull`
   - `git branch -d <branch-name>`

### Worktree usage (only when explicitly requested)

When the user asks to use a worktree or work on multiple issues in parallel:
- Create a worktree: `git worktree add .claude/worktrees/42-duplicate-detection -b 42-duplicate-detection`
- All file reads/edits/writes must use the full worktree path
- Run all git commands in the worktree using `-C`: `git -C .claude/worktrees/<branch-name> <command>`
- Do NOT use `cd .claude/worktrees/<branch-name> && git ...`
- Cleanup: `git -C <repo-root> worktree remove .claude/worktrees/<branch-name>` then `git -C <repo-root> branch -d <branch-name>`

### Picking the next issue

When asked to "pick the next issue" or "work on the next issue":

1. Fetch open milestones: `gh api 'repos/magnusakselvoll/photo-organizer/milestones?state=open&per_page=20'`
2. Sort milestones by priority if set; otherwise sort by the leading number in the title (e.g. `Phase 0` < `Phase 1`)
3. From the lowest-priority milestone that still has open issues, fetch its open issues: `gh issue list --repo magnusakselvoll/photo-organizer --milestone "<title>" --state open --json number,title,labels`
4. Pick the open issue with the lowest number
5. Confirm the choice with the user before starting work

## Build Commands

```bash
dotnet build                                           # Build all projects
dotnet test                                            # Run all tests
dotnet test --filter "TestCategory!=Integration"       # Run only unit tests (CI dotnet job)
dotnet test --filter "TestCategory=Integration"        # Run only integration tests (CI integration job)
dotnet run --project src/PhotoOrganizer.Server         # Run the backend server

# Fixture generator (run once to regenerate committed test fixtures)
dotnet run --project tools/PhotoOrganizer.FixtureGenerator

# Frontend (run inside src/PhotoOrganizer.Web)
pnpm install                                           # Install dependencies
pnpm run build                                         # Build to wwwroot (required before running server)
pnpm run dev                                           # Dev server with hot reload (port 6173)
pnpm run lint                                          # Lint frontend code
pnpm run test                                          # Run frontend tests
```

## Test Classification

CI runs two separate .NET jobs — `dotnet` (unit tests) and `integration` (integration tests). Every new test class **must** be correctly classified:

- **Unit tests** (no attribute): Pure in-process tests using fakes/stubs. These run in the `dotnet` CI job.
- **Integration tests** (`[TestCategory("Integration")]`): Tests that run the full pipeline against real file system state (e.g., the end-to-end test in `tests/PhotoOrganizer.EndToEnd.Tests`). These run in the `integration` CI job.

Integration tests **must be CI-safe**: self-contained (copy committed fixtures from `tests/fixtures/photos/` to a temp dir, clean up on teardown) with no dependency on local machine state like personal photo paths.

When writing a new test class, explicitly decide which category it belongs to.

## Test Fixtures

Committed JPEG fixtures live in `tests/fixtures/photos/`:
- `originals/` — 12 photos with EXIF `DateTimeOriginal`; 3 have matching edits
- `edits/` — 3 edited copies of originals (named `IMG_xxxx_edit.jpg`)

Each subfolder has a `_folder.json` with the correct `type` so the crawler's duplicate detection logic correctly prefers `edits/` over `originals/`. Sidecars (`.meta.json`) are **not** committed; the end-to-end test creates them in a temp working copy.

HEIC fixture lives in `tests/fixtures/heic/` (separate so it doesn't disturb the photo count assertions in the main end-to-end tests). The single `IMG_heic.heic` was generated once with `sips -s format heic tests/fixtures/photos/originals/IMG_1001.jpg --out tests/fixtures/heic/IMG_heic.heic`. The `HeicTranscodingTests` integration test crawls this fixture and verifies the server returns a valid JPEG for it.

To regenerate JPEG fixtures: `dotnet run --project tools/PhotoOrganizer.FixtureGenerator`. The generator (`tools/PhotoOrganizer.FixtureGenerator/`) uses SixLabors.ImageSharp 3.x (Apache licensed) to write 64×64 JPEGs with EXIF data.

## Architecture Decision Records (ADRs)

ADRs live in `docs/adr/NNN-kebab-title.md`. The format mirrors `docs/adr/001-crawler-stack.md`: `## Status`, `## Context`, `## Decision`, `## Consequences`. Next available number after this issue: **011**.

**During issue planning**, assess whether the work introduces or reverses an architectural decision — a cross-cutting pattern, a tech/library choice, a security-posture change, a data-contract change, or a consciously-accepted tradeoff (including deliberate deferrals and reversals). If so, propose an ADR as part of the plan.

**Always ask the user before creating or materially changing an ADR.** Never write an ADR without explicit confirmation.

**Single source of truth**: rationale, alternatives considered, and accepted tradeoffs belong in the ADR. CLAUDE.md and SPEC.md reference the ADR (`see ADR NNN`) rather than re-explaining it.

## Documentation Updates

When closing issues via PR, update as needed: **SPEC.md** (requirements/behavior), **README.md** (setup/config), **CLAUDE.md** (implementation details/build/known issues), **docs/adr/** (if the change introduces or reverses an architectural decision — always ask first).

## Coding Style

Keep things simple — no speculative abstractions; validate only at system boundaries; prefer editing existing files over creating new ones; do not auto-commit without user confirmation.

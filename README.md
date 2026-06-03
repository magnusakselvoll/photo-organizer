# Photo Organizer

Personal photo management application for browsing, organizing, and displaying photo collections as slideshows.

## Overview

Photos live across multiple folders on a Windows PC and a Synology NAS. This application brings them together with a clean browsing UI and an "eternal slideshow" mode — designed to run unattended on a display.

## Key Features

- Browse photos across multiple configured source folders
- Slideshow mode with smooth transitions and Ken Burns effects; fully keyboard-navigable
- Originals and edited versions tracked separately; edited versions preferred for display
- Duplicate detection based on file names across folders
- Metadata stored as sidecar files alongside photos — no database lock-in
- Extensible for future features: auto-tagging, location, face recognition

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend API | .NET 10, ASP.NET Core (C#) |
| Frontend | React, TypeScript, Vite |
| Metadata | Sidecar files per folder/file |

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/) and [pnpm](https://pnpm.io/installation)
- Windows or macOS

### First-time setup

**1. Configure the server**

```sh
cp src/PhotoOrganizer.Server/appsettings.example.json src/PhotoOrganizer.Server/appsettings.json
```

Edit `src/PhotoOrganizer.Server/appsettings.json` and set `ScanRoots` to your photo library paths. You can point each entry at the top of a library tree — any subfolder with a `_folder.json` will be discovered automatically.

**2. Configure the crawler**

```sh
cp crawler-config.example.json crawler-config.json
```

Edit `crawler-config.json` and set `ScanRoots` to the same paths. Each entry is searched recursively; individual subfolders are opted in via `crawler init`.

**3. Build the frontend**

```sh
cd src/PhotoOrganizer.Web
pnpm install
pnpm run build
cd ../..
```

### Running locally

**Start the backend server** (port 6192):

```sh
dotnet run --project src/PhotoOrganizer.Server
```

**Seed and run the crawler** (writes sidecar metadata alongside your photos):

```sh
# Initialise a subfolder (creates _folder.json and crawls all configured roots so
# cross-folder duplicate detection works from the start)
dotnet run --project src/PhotoOrganizer.Crawler -- init /path/to/photos/originals --label "Originals"
dotnet run --project src/PhotoOrganizer.Crawler -- init /path/to/photos/edits --label "Edits" --type edits

# Subsequent runs: incremental crawl across all initialised folders under ScanRoots
dotnet run --project src/PhotoOrganizer.Crawler -- run

# Full re-crawl
dotnet run --project src/PhotoOrganizer.Crawler -- run --mode full

# If you are upgrading from an older version that used the old sidecar naming
# scheme (<name>.meta.json instead of <name>.<ext>.meta.json), delete and
# regenerate all sidecars with:
dotnet run --project src/PhotoOrganizer.Crawler -- run --delete-existing-meta
```

Open [http://localhost:6192](http://localhost:6192) in your browser.

### Slideshow keyboard shortcuts

| Key | Action |
|-----|--------|
| `←` / `→` | Previous / Next photo |
| `Space` | Play / Pause |
| `+` / `-` | Increase / Decrease display time (10s steps below 60s, 60s steps above) |
| `I` | Show photo info |
| `?` | Show this shortcut reference |
| `Esc` | Exit slideshow |

The display time defaults to **30 seconds** and can be adjusted between 10 seconds and 10 minutes. The Ken Burns effect duration follows the display time automatically. To change the default, set `PhotoOrganizer:Slideshow:IntervalSeconds` in `appsettings.json`.

### Frontend dev mode

For hot-reload during frontend development, start both the backend and the Vite dev server:

```sh
# Terminal 1 — backend (port 6192)
dotnet run --project src/PhotoOrganizer.Server

# Terminal 2 — frontend dev server (port 6173, proxied to backend)
cd src/PhotoOrganizer.Web
pnpm run dev
```

### Build and test

```sh
dotnet build                                           # Build all projects
dotnet test --filter "TestCategory!=Integration"       # Unit tests (CI)
dotnet test --filter "TestCategory=Integration"        # Integration tests (requires file system)
dotnet test                                            # All tests

cd src/PhotoOrganizer.Web
pnpm run lint                                          # Lint frontend
pnpm run test                                          # Frontend tests
```

## Security / Networking

The server binds **loopback-only** by default — `localhost:6192` in dev (via `launchSettings.json`); Kestrel's default `localhost:5000` in published builds. It is intentionally not accessible from other machines.

**Do not bind to `0.0.0.0` or a LAN interface** without first adding authentication (e.g. a shared secret) and rate limiting. Before any such change, read the security notes in SPEC.md §8.

## Project Structure

```
src/
  PhotoOrganizer.Domain/        # Core entities and interfaces
  PhotoOrganizer.Application/   # Business logic, DTOs, services
  PhotoOrganizer.Infrastructure/# File system, sidecar parsing, indexing
  PhotoOrganizer.Server/        # ASP.NET Core API host
  PhotoOrganizer.Web/           # React frontend
tests/
  ...
```

## See Also

- [SPEC.md](SPEC.md) — Technical specification and architecture detail

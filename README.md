# Photo Organizer

Personal photo management application for browsing, organizing, and displaying photo collections as slideshows.

## Overview

Photos live across multiple folders on a Windows PC and a Synology NAS. This application brings them together with a clean browsing UI and an "eternal slideshow" mode — designed to run unattended on a display.

## Key Features

- Browse photos across multiple configured source folders
- Slideshow mode with smooth transitions
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

> Setup and run instructions will be added as the project takes shape.

### Prerequisites

- .NET 10 SDK
- Node.js 20+ and pnpm
- Windows or macOS

### Running Locally

```sh
# To be documented
```

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

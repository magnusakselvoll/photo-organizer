# Claude Working Instructions — Photo Organizer

## Project Context

Personal photo management app. See [README.md](README.md) for the user-facing overview and [SPEC.md](SPEC.md) for detailed technical requirements.

Reference implementation to draw patterns from: https://github.com/magnusakselvoll/photo-booth-take-two

## Architecture Conventions

- **Clean Architecture**: Domain → Application → Infrastructure → Server. Never let outer layers bleed into inner ones.
- **Namespace prefix**: `PhotoOrganizer.*`
- **Backend**: .NET 10, ASP.NET Core, C#
- **Frontend**: React + TypeScript, Vite, pnpm
- **Ports (dev)**: Backend `:5192`, Frontend `:5173`

## Patterns to Follow

- **Repository pattern** for all data access — domain defines interfaces, infrastructure implements them
- **Pluggable providers** — camera/storage/detection strategies behind interfaces, easy to swap
- **Sidecar files** (`_folder.json`, `<name>.meta.json`) for all metadata — no database required
- **Lazy loading** — index and cache photo metadata on demand rather than at startup
- **Thread-safe file access** — use semaphore locks when reading/writing shared state
- **Centralized package versions** — `Directory.Packages.props`; no version numbers inside individual `.csproj` files

## Key Files to Know

| File | Role |
|------|------|
| `SPEC.md` | Source of truth for requirements, data models, API design |
| `src/PhotoOrganizer.Domain/` | Entities and interfaces — add here first |
| `src/PhotoOrganizer.Infrastructure/` | File system, sidecar parsing, indexing |
| `src/PhotoOrganizer.Server/Program.cs` | App bootstrap and middleware registration |
| `src/PhotoOrganizer.Web/src/api/` | TypeScript API client and shared types |
| `appsettings.json` | Local config (gitignored — contains personal paths) |

## GitHub Flow

- All work is tracked as **GitHub Issues** — no other task tracker
- Branch from `main` for each piece of work; name branches after the issue (e.g. `42-duplicate-detection`)
- Open a **Pull Request** against `main` when ready for review; use the PR template (`.github/pull_request_template.md`)
- PRs must reference the closing issue (`Closes #N`) and pass the checklist before merging
- Merge to `main` via the PR; do not push directly to `main`

## Development Workflow

- Run backend: `dotnet run --project src/PhotoOrganizer.Server`
- Run frontend: `pnpm run dev` inside `src/PhotoOrganizer.Web`
- Tests: `dotnet test`

## Coding Style

- Keep things simple — no speculative abstractions
- Validate only at system boundaries (user input, external files)
- Prefer editing existing files over creating new ones
- Do not auto-commit; always confirm with the user before committing or pushing

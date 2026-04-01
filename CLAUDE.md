# Claude Working Instructions — Photo Organizer

## Project Context

Personal photo management app. See [README.md](README.md) for the user-facing overview and [SPEC.md](SPEC.md) for detailed technical requirements.

Reference implementation to draw patterns from: https://github.com/magnusakselvoll/photo-booth-take-two

## Architecture Conventions

- **Clean Architecture**: Domain → Application → Infrastructure → Server. Never let outer layers bleed into inner ones.
- **Namespace prefix**: `PhotoOrganizer.*`
- **Backend**: .NET 10, ASP.NET Core, C#
- **Frontend**: React + TypeScript, Vite, pnpm
- **Ports (dev)**: Backend `:6192`, Frontend `:6173`

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
   - Pass `--title` and `--body` as plain strings to `gh pr create` — no heredocs, no backticks
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

1. Fetch open milestones: `gh api repos/magnusakselvoll/photo-organizer/milestones?state=open&per_page=20`
2. Sort milestones by priority if set; otherwise sort by the leading number in the title (e.g. `Phase 0` < `Phase 1`)
3. From the lowest-priority milestone that still has open issues, fetch its open issues: `gh issue list --repo magnusakselvoll/photo-organizer --milestone "<title>" --state open --json number,title,labels`
4. Pick the open issue with the lowest number
5. Confirm the choice with the user before starting work

## Build Commands

```bash
dotnet build                                           # Build all projects
dotnet test                                            # Run all tests
dotnet test --filter "TestCategory!=Integration"       # Run only non-integration tests (CI)
dotnet run --project src/PhotoOrganizer.Server         # Run the backend server

# Frontend (run inside src/PhotoOrganizer.Web)
pnpm install                                           # Install dependencies
pnpm run build                                         # Build to wwwroot (required before running server)
pnpm run dev                                           # Dev server with hot reload (port 6173)
pnpm run lint                                          # Lint frontend code
pnpm run test                                          # Run frontend tests
```

## Test Classification

CI runs `dotnet test --filter "TestCategory!=Integration"`, so every new test class **must** be correctly classified:

- **Unit tests** (no attribute): Pure in-process tests using fakes/stubs. These run in CI.
- **Integration tests** (`[TestCategory("Integration")]`): Tests requiring external resources (real file system with specific paths, external services). These are skipped in CI.

When writing a new test class, explicitly decide which category it belongs to.

## Documentation Updates

When closing issues via PR, consider updating:
- **SPEC.md** — Functional requirements, use cases, expected behavior
- **README.md** — Setup instructions, configuration, user-facing changes
- **CLAUDE.md** — Technical implementation details, architecture, build commands, known issues

## Coding Style

- Keep things simple — no speculative abstractions
- Validate only at system boundaries (user input, external files)
- Prefer editing existing files over creating new ones
- Do not auto-commit; always confirm with the user before committing or pushing

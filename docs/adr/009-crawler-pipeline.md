# ADR 009: Crawler Pipeline/Step Framework and Tiered Change Detection

## Status

Accepted

## Context

The crawler needs to run multiple distinct processing operations on each photo file: at minimum metadata extraction and duplicate detection, and in future phases GPS enrichment, auto-tagging, and perceptual hashing (see ADR 001). These operations differ in:

- **Cost**: metadata extraction is fast (EXIF read); perceptual hashing will be slow (image decode + hash computation)
- **Frequency**: metadata rarely changes after initial crawl; duplicate detection may need re-running when new photos arrive
- **Dependencies**: duplicate detection must run after metadata (it uses the captured timestamp from the metadata step)

A monolithic single-pass crawler would re-run all operations on every file every crawl, which becomes prohibitively expensive as the library grows and more processing steps are added.

Additionally, the crawler runs on a NAS where backup tools (Time Machine, rsync) routinely update file modification times without changing content. A naive mtime-based change check would reprocess every file after each backup restore.

## Decision

**The crawler uses a named, versioned step pipeline with per-file step-completion records, and detects changes using a tiered mtime-then-SHA-256 strategy.**

### Pipeline framework

Processing steps implement `IProcessingStep` (`src/PhotoOrganizer.Crawler/Pipeline/IProcessingStep.cs`) or `IBatchProcessingStep`:

```csharp
public interface IProcessingStep
{
    string Name    { get; }   // e.g. "metadata", "duplicates"
    int Version    { get; }   // increment to force re-run for all files
    IReadOnlyList<string> DependsOn { get; }
    Task ExecuteAsync(ProcessingContext context);
}
```

`PipelineRunner` (`src/PhotoOrganizer.Crawler/Pipeline/PipelineRunner.cs`) iterates registered steps in dependency order. For each step, it checks `sidecar.CrawlSteps[step.Name].Version`:
- If `existingVersion >= step.Version` → skip (already processed at this version)
- Otherwise → execute the step, record `{version, completedAt}` in the sidecar

Steps are registered in `StepRegistry` and `CrawlOrchestrator` runs the pipeline per file.

**Version bump semantics**: incrementing `IProcessingStep.Version` marks all existing per-file records as stale, causing the step to re-run for every file on the next crawl. This is the correct way to force a global re-process (e.g. when the duplicate-detection algorithm changes); it does not require deleting sidecars or a full recrawl flag.

**`crawlSteps` in the sidecar** (`schemas/photo-meta.schema.json`, `SPEC.md §5`):
```json
"crawlSteps": {
  "metadata":   { "version": 1, "completedAt": "2025-03-15T10:00:00Z" },
  "duplicates": { "version": 1, "completedAt": "2025-03-15T10:00:05Z" }
}
```

### Tiered change detection

`ChangeDetector` (`src/PhotoOrganizer.Crawler/ChangeDetection/ChangeDetector.cs`) evaluates each file in two tiers:

| Tier | Check | Result |
|------|-------|--------|
| 1 | `|storedMtime − fileMtime| < 1 s` → unchanged | Skip all steps (fast path) |
| 2 | Compute SHA-256; compare with stored hash | Matching hash → `ModTimeOnly` (update mtime only, skip re-processing); differing hash → `Changed` (run all pending steps) |

`ModTimeOnly` handles the common NAS/backup-restore case where mtime is bumped but content is identical. It updates the stored mtime so tier-1 hits on the next run, avoiding a hash computation every crawl.

### Current steps

| Step | Class | Version | Depends on |
|------|-------|---------|-----------|
| `metadata` | `MetadataStep` | 1 | — |
| `duplicates` | `DuplicatesStep` | 1 | `metadata` |

Future steps (faces, GPS, auto-tag) will follow the same pattern; see ADR 001 for the Python sub-tool strategy for image-heavy steps.

## Consequences

**Positive:**
- Incremental crawls are cheap: only new or changed files run the full pipeline; unchanged files are skipped in one mtime comparison
- Version bumps provide a clean, targeted way to force re-processing of a specific step without touching other steps or deleting sidecars
- New processing steps can be added without modifying `CrawlOrchestrator` — they self-register via `StepRegistry`
- The `crawlSteps` record in the sidecar makes it possible to audit exactly which steps ran and when for any given photo

**Accepted tradeoffs:**
- Step version numbers must be incremented manually by the developer; forgetting to bump the version means a changed algorithm will not re-run on existing files
- The dependency order is currently enforced by `DependsOn` at registration time; circular dependencies would cause undefined behavior (not currently validated at startup)
- SHA-256 is computed on the full file; for very large RAW files (50+ MB) this adds noticeable I/O cost on the first changed-file detection

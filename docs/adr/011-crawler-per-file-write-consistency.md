# ADR 011: Crawler Per-File Write Consistency — Sidecar Authoritative, DB Reconcilable Cache

## Status

Accepted

## Context

For each new or changed photo file the crawler processes three writes across two independent
persistence systems:

1. `crawled_files` upsert (SQLite) — records `file_hash` and `modified_at` for change detection.
2. `step_runs` upsert per pipeline step (SQLite) — records which processing steps completed and at
   which version.
3. `.meta.json` sidecar write (filesystem, atomic rename) — records the step completion state used
   by `PipelineRunner` to decide which steps to skip on the next crawl.

Before this change each write was its own autocommit transaction on its own SQLite connection, and
they occurred in the order listed above. Two crash scenarios caused silent data loss:

- **Crash between (1) and (3)**: `crawled_files.modified_at` is advanced, so the next incremental
  crawl's `ChangeDetector` skips the file (mod-time within 1 s → `Unchanged`); the sidecar is
  never written, so all pipeline steps are silently re-skipped — permanent step loss with no
  observable error.
- **Crash between step (2) and the final sidecar write**: individual `step_runs` rows are committed
  eagerly per step, but the sidecar that `PipelineRunner` reads for skip decisions is only written
  once at the very end; the DB and sidecar diverge.

The key insight that informed the fix: `step_runs` is **write-only** — no code path reads it back
to make any decision; all skip logic keys exclusively off the sidecar's `CrawlSteps` dictionary.
The sidecar is therefore already the de facto authority; the DB just needs to be brought into a
consistent, recoverable relationship with it.

## Decision

Treat the `.meta.json` sidecar as the authoritative record of step completion. Treat `crawled_files`
and `step_runs` as a reconcilable cache that must never be advanced past what the sidecar reflects.

**Implementation:** wrap each file's `UpsertAsync` + all `RecordStepRunAsync` calls in a single
`SqliteTransaction` (`CrawlFileTransaction`) that is committed **only after** the sidecar has been
durably written to disk.

`CrawlerDatabase.BeginFileTransaction()` opens a connection, applies the WAL/foreign-key PRAGMAs
(which must precede `BEGIN`), and wraps the result in a `CrawlFileTransaction` — a lightweight
`IDisposable` holding the connection and transaction. Disposing without calling `Commit()` rolls
back automatically.

`CrawlOrchestrator.ProcessFileWithPipelineAsync()` is the transaction scope:

```
using var tx = _db.BeginFileTransaction();
var dbRecord = await _fileRepo.UpsertAsync(file.FilePath, hash, modifiedAt, tx);
await _pipeline.RunAsync(file.FilePath, dbRecord, tx);   // writes sidecar last
tx.Commit();                                              // DB durable only after sidecar
```

`PipelineRunner.RunAsync` writes the sidecar as its final step (unchanged), then returns — the
commit in the orchestrator follows immediately after. `RecordStepRunAsync` calls inside the pipeline
pass the same transaction and accumulate their writes within it; none of them are visible outside
the transaction until commit.

### Crash analysis

| Crash moment | Sidecar state | DB state | Next crawl behaviour |
|---|---|---|---|
| Before sidecar write | Untouched (old) | Tx rolled back → old | Reprocesses correctly |
| After sidecar write, before commit | New (durable) | Tx rolled back → old hash/modtime | `Changed` detected → reprocesses → step-skip skips already-done steps (idempotent) |
| After commit | New (durable) | New (committed) | `Unchanged` → skipped correctly |

All crash states are consistent and recoverable — there is no window for silent step loss.

### Out of scope

`RunTargetedAsync` (batch-step-only upsert) and `ModTimeOnly` (timestamp-only update) are left as
autocommit; they do not touch the sidecar in the same coupled way. The failed-step retry gap (a
failed step still advances `file_hash`, so it is not retried on the next incremental crawl) is
pre-existing behaviour and is left for a future issue.

## Consequences

**Positive:**
- Crash during per-file processing can no longer silently lose step results.
- All three writes (`crawled_files`, `step_runs`, sidecar) are committed atomically from the
  observer's perspective, with the sidecar as the commit point.
- The fix is additive: `UpsertAsync` and `RecordStepRunAsync` accept an optional
  `CrawlFileTransaction? tx = null` parameter; existing callers without a transaction continue to
  work in autocommit mode.

**Accepted tradeoffs:**
- A write lock on the SQLite WAL file is held for the duration of one file's pipeline execution
  (metadata extraction, etc.). The crawler is single-writer by design; there is no concurrent writer
  to contend with, so the lock is never contested.
- Batch processing steps (duplicate detection via Python sub-tools) run outside this transaction
  scope — they write sidecars independently and do not need the same crash guarantee.
- `step_runs` remains advisory/audit-only. It accurately reflects the committed state after this
  change, but nothing reads it for operational decisions.

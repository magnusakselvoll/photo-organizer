# Data Contract Schemas

Formal definitions for all data formats shared between the photo-organizer server and the crawler. Both sides must conform to these schemas.

## Files

| File | Format | Describes |
|------|--------|-----------|
| `folder.schema.json` | JSON Schema (draft 2020-12) | `_folder.json` sidecar — one per source folder |
| `photo-meta.schema.json` | JSON Schema (draft 2020-12) | `<photoname>.meta.json` sidecar — one per photo |
| `crawler.sql` | SQLite DDL | Crawler's operational database |

## Ownership

| Data format | Written by | Read by |
|-------------|------------|---------|
| `_folder.json` | Crawler (`init` command) | Server (folder discovery), Crawler |
| `<photoname>.meta.json` | Crawler (processing steps) | Server (photo metadata), Crawler |
| Crawler SQLite DB | Crawler | Crawler only |

The sidecar files are the **source of truth** for photo metadata. The SQLite database is operational state internal to the crawler — the server never reads it.

## Versioning Policy

All schema documents carry a sequential `version` integer field, starting at `1`.

### Rules for evolving schemas

1. **Additive changes** (new optional fields with defaults) are non-breaking. Increment `version` in the JSON Schema file. Both sides must tolerate unknown properties gracefully — **readers must never reject a document solely because it contains an unfamiliar field**.

2. **Breaking changes** (removing fields, changing types, renaming, making previously optional fields required) require a coordinated release of both server and crawler. Increment the relevant crawl step version to trigger reprocessing of affected files.

3. **SQLite migrations** are applied by the crawler on startup, using the `schema_version` table to determine which have run. Each migration is a numbered SQL file (`schemas/migrations/NNN-description.sql`). The initial `crawler.sql` serves as migration 000.

4. The `$id` URIs in JSON Schema files are stable identifiers for documentation and tooling — they are not fetched at runtime.

### Backwards and forwards compatibility

- **Backwards compatibility** (old files, new code): New code must accept documents at older `version` values, supplying defaults for any missing fields.
- **Forwards compatibility** (new files, old code): Old code must not crash on documents with a higher `version` or unexpected fields. Use permissive deserialization (`ignoreUnknownProperties` / `JsonIgnoreCondition`).

## Validation

To validate a sidecar file against its schema (requires a JSON Schema validator such as [`ajv-cli`](https://github.com/ajv-validator/ajv-cli)):

```sh
ajv validate -s schemas/folder.schema.json -d path/to/_folder.json
ajv validate -s schemas/photo-meta.schema.json -d path/to/photo.meta.json
```

To verify the SQLite DDL creates without errors:

```sh
sqlite3 :memory: < schemas/crawler.sql && echo "OK"
```

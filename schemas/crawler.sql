-- Crawler operational database schema
-- Version: 1
--
-- This database tracks crawl state for the photo-organizer crawler.
-- The sidecar files (_folder.json, <photoname>.meta.json) remain the source
-- of truth for photo metadata. The server never reads this database.
--
-- Migrations: future schema changes are applied as numbered migration scripts
-- (schemas/migrations/NNN-description.sql). This file serves as migration 000.

PRAGMA journal_mode = WAL;
PRAGMA foreign_keys = ON;

-- Tracks the current schema version for migration management.
CREATE TABLE IF NOT EXISTS schema_version (
    version     INTEGER NOT NULL,
    applied_at  TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

INSERT INTO schema_version (version) VALUES (1);

-- One row per file discovered during crawling.
CREATE TABLE IF NOT EXISTS crawled_files (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    file_path       TEXT    NOT NULL UNIQUE,         -- absolute path
    file_hash       TEXT,                            -- SHA-256 hex digest; NULL until first hash computed
    modified_at     TEXT,                            -- ISO 8601, file's last-modified timestamp
    first_seen_at   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    last_crawled_at TEXT,                            -- ISO 8601, last successful crawl pass
    deleted         INTEGER NOT NULL DEFAULT 0       -- 1 if file was not found on last scan
);

-- One row per (file, step) pair, recording the most recent run of each processing step.
CREATE TABLE IF NOT EXISTS step_runs (
    file_id       INTEGER NOT NULL REFERENCES crawled_files(id) ON DELETE CASCADE,
    step_name     TEXT    NOT NULL,
    step_version  INTEGER NOT NULL,
    completed_at  TEXT    NOT NULL,                  -- ISO 8601
    status        TEXT    NOT NULL DEFAULT 'completed', -- completed | failed
    error_message TEXT,                              -- NULL unless status = 'failed'
    PRIMARY KEY (file_id, step_name)
);

-- One row per crawl run.
CREATE TABLE IF NOT EXISTS crawl_log (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    started_at      TEXT    NOT NULL,                -- ISO 8601
    completed_at    TEXT,                            -- NULL while in progress
    mode            TEXT    NOT NULL,                -- full | incremental | targeted
    target_step     TEXT,                            -- step name if mode = 'targeted'; NULL otherwise
    status          TEXT    NOT NULL DEFAULT 'running', -- running | completed | failed
    files_scanned   INTEGER NOT NULL DEFAULT 0,
    files_processed INTEGER NOT NULL DEFAULT 0,
    files_errored   INTEGER NOT NULL DEFAULT 0,
    error_message   TEXT                             -- top-level crawl error, if any
);

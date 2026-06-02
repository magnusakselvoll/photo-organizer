using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoOrganizer.Application;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;

namespace PhotoOrganizer.Infrastructure.Indexing;

/// <summary>
/// Background service that progressively indexes photos from sidecars in randomized order.
/// Uses multiple parallel workers; each picks a random pending directory, shuffles its
/// contents, and processes files/subdirs — giving a different random spread of the library
/// at every startup.
///
/// Architecture note: sidecars (<photo>.meta.json, _folder.json) remain the sole source of
/// truth. This indexer reads them; it never writes metadata or touches the crawler DB.
/// </summary>
public sealed class RandomizedSidecarIndexer : BackgroundService
{
    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic",
        ".cr2", ".cr3", ".orf", ".arw", ".nef", ".rw2",
        ".tiff", ".tif"
    };

    private readonly PhotoOrganizerSettings _settings;
    private readonly ISidecarReader _sidecarReader;
    private readonly PhotoIndex _index;
    private readonly PhotoIndexCache _cache;
    private readonly ILogger<RandomizedSidecarIndexer> _logger;

    // Pending work: each entry is a directory to process plus the effective crawl-unit
    // context that governs it (null = not yet under any _folder.json unit).
    private readonly List<PendingDir> _pending = [];
    private readonly object _pendingLock = new();

    public RandomizedSidecarIndexer(
        IOptions<PhotoOrganizerSettings> settings,
        ISidecarReader sidecarReader,
        PhotoIndex index,
        PhotoIndexCache cache,
        ILogger<RandomizedSidecarIndexer> logger)
    {
        _settings = settings.Value;
        _sidecarReader = sidecarReader;
        _index = index;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Try loading from cache first — instant warm start.
        if (await _cache.TryLoadAsync(_index, stoppingToken))
        {
            _logger.LogInformation("Photo index loaded from cache ({Count} photos, {Folders} folders)",
                _index.Count, _index.SnapshotFolders().Count);
            _index.MarkComplete();
            return;
        }

        await RunIndexBuildAsync(stoppingToken);
    }

    /// <summary>Clears the index and cache, then re-runs a fresh build. Called by InvalidateCacheAsync.</summary>
    public async Task RestartAsync()
    {
        _index.Clear();
        await _cache.DeleteAsync();
        // The hosted service lifetime owns re-triggering; callers should invalidate and let the
        // next server restart pick it up, or inject the service and call ExecuteAsync again.
    }

    private async Task RunIndexBuildAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting randomized photo index build from {RootCount} scan roots",
            _settings.ScanRoots.Length);

        lock (_pendingLock) { _pending.Clear(); }

        // pendingCount = (items queued) + (items being processed).
        // Guaranteed invariant: never decrements below 0; hits 0 only when all work is done.
        var pendingCount = 0;
        var completionTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingSignal = new SemaphoreSlim(0, int.MaxValue);

        void Enqueue(PendingDir dir)
        {
            // Increment BEFORE adding to queue — ensures pendingCount never hits 0 prematurely.
            Interlocked.Increment(ref pendingCount);
            lock (_pendingLock)
                _pending.Insert(Random.Shared.Next(_pending.Count + 1), dir);
            pendingSignal.Release();
        }

        void MarkDone()
        {
            if (Interlocked.Decrement(ref pendingCount) == 0)
                completionTcs.TrySetResult();
        }

        // Seed from scan roots.
        foreach (var root in _settings.ScanRoots)
            Enqueue(new PendingDir(root, null));

        // Edge case: no scan roots configured.
        if (_settings.ScanRoots.Length == 0)
            completionTcs.TrySetResult();

        // Start parallel workers.
        using var workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var parallelism = Math.Max(1, _settings.Indexing.MaxParallelism);

        var workers = Enumerable.Range(0, parallelism)
            .Select(_ => Task.Run(async () =>
            {
                while (!workerCts.Token.IsCancellationRequested)
                {
                    // Block until there's work to do (or we're cancelled).
                    try { await pendingSignal.WaitAsync(workerCts.Token); }
                    catch (OperationCanceledException) { return; }

                    // Pop a random item.
                    PendingDir? item = null;
                    lock (_pendingLock)
                    {
                        if (_pending.Count > 0)
                        {
                            var idx = Random.Shared.Next(_pending.Count);
                            item = _pending[idx];
                            _pending.RemoveAt(idx);
                        }
                    }

                    if (item is null)
                    {
                        // Spurious signal (can happen during restart/clear) — put the credit back.
                        pendingSignal.Release();
                        continue;
                    }

                    try { await ProcessDirectoryAsync(item, cancellationToken, Enqueue); }
                    finally { MarkDone(); }
                }
            }, workerCts.Token))
            .ToArray();

        try
        {
            await completionTcs.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            workerCts.Cancel();
            await Task.WhenAll(workers.Select(t => t.ContinueWith(_ => { }, TaskContinuationOptions.None)));
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            _index.MarkComplete();
            _logger.LogInformation("Photo index build complete: {PhotoCount} photos, {FolderCount} folders",
                _index.Count, _index.SnapshotFolders().Count);
            await _cache.SaveAsync(_index, cancellationToken);
        }
    }

    private async Task ProcessDirectoryAsync(PendingDir item, CancellationToken cancellationToken,
        Action<PendingDir> enqueue)
    {
        var dirPath = item.Path;
        if (!Directory.Exists(dirPath))
            return;

        // Determine the effective crawl unit for this directory.
        // A _folder.json here starts/overrides the inherited unit.
        var effectiveUnit = item.ActiveUnit;
        var folderSidecarPath = Path.Combine(dirPath, "_folder.json");
        if (File.Exists(folderSidecarPath))
        {
            try
            {
                var sidecar = await _sidecarReader.ReadFolderSidecarAsync(dirPath);
                if (sidecar is not null)
                {
                    var folder = new SourceFolder
                    {
                        Path = dirPath,
                        Label = sidecar.Label,
                        Type = FolderTypeExtensions.Parse(sidecar.Type),
                        Enabled = sidecar.Enabled
                    };
                    _index.AddFolder(folder);
                    effectiveUnit = folder;

                    if (!folder.Enabled)
                        return; // Prune the entire subtree for disabled units.
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read _folder.json in {Dir}; skipping as crawl unit", dirPath);
            }
        }

        // Enumerate one level (no recursion — subdirs go back into the pending queue).
        string[] subdirs;
        string[] files;
        try
        {
            var opts = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                RecurseSubdirectories = false
            };
            subdirs = Directory.GetDirectories(dirPath, "*", opts);
            files = Directory.GetFiles(dirPath, "*", opts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skipping inaccessible directory {Dir}", dirPath);
            return;
        }

        // Shuffle subdirs and enqueue them at random positions.
        Shuffle(subdirs);
        foreach (var sub in subdirs)
            enqueue(new PendingDir(sub, effectiveUnit));

        // Only index photo files when inside an enabled crawl unit.
        if (effectiveUnit is null || !effectiveUnit.Enabled)
            return;

        // Shuffle files for randomized discovery within the folder.
        Shuffle(files);

        foreach (var filePath in files)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var ext = Path.GetExtension(filePath);
            if (!PhotoExtensions.Contains(ext))
                continue;

            try
            {
                var photo = await BuildPhotoAsync(filePath, effectiveUnit.Type);
                _index.AddPhoto(photo);

                // Periodically write the in-progress cache so a mid-build restart is cheap.
                var interval = _settings.Indexing.CacheWriteIntervalPhotos;
                if (interval > 0 && _index.Count % interval == 0)
                    await _cache.SaveAsync(_index, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to index photo {FilePath}", filePath);
            }
        }
    }

    private async Task<Photo> BuildPhotoAsync(string filePath, FolderType folderType)
    {
        var sidecar = await _sidecarReader.ReadPhotoMetaAsync(filePath);
        var fileModifiedAt = GetFileModifiedAt(filePath);

        return new Photo
        {
            Id = PhotoId.FromFilePath(filePath),
            FilePath = filePath,
            FileName = Path.GetFileNameWithoutExtension(filePath),
            CapturedAt = sidecar?.CapturedAt,
            FileModifiedAt = fileModifiedAt,
            FolderType = folderType,
            DuplicateGroupId = sidecar?.DuplicateGroupId,
            IsPreferred = sidecar?.IsPreferred ?? false,
            Tags = (IReadOnlyList<string>?)sidecar?.Tags ?? []
        };
    }

    private static DateTimeOffset? GetFileModifiedAt(string filePath)
    {
        try { return new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero); }
        catch { return null; }
    }

    private static void Shuffle<T>(T[] array)
    {
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    private sealed record PendingDir(string Path, SourceFolder? ActiveUnit);
}

using PhotoOrganizer.Crawler.ChangeDetection;
using PhotoOrganizer.Crawler.Data;
using PhotoOrganizer.Crawler.Discovery;
using PhotoOrganizer.Crawler.Pipeline;
using PhotoOrganizer.Crawler.Sidecars;
using Serilog;

namespace PhotoOrganizer.Crawler;

public sealed class CrawlOrchestrator
{
    private readonly ICrawledFileRepository _fileRepo;
    private readonly ICrawlLogRepository _logRepo;
    private readonly ISidecarStore _sidecarStore;
    private readonly ICrawlTargetResolver _resolver;
    private readonly ChangeDetector _changeDetector;
    private readonly PipelineRunner _pipeline;
    private readonly IReadOnlyList<IBatchProcessingStep> _batchSteps;
    private readonly CrawlerDatabase _db;

    public CrawlOrchestrator(
        ICrawledFileRepository fileRepo,
        ICrawlLogRepository logRepo,
        ISidecarStore sidecarStore,
        ICrawlTargetResolver resolver,
        ChangeDetector changeDetector,
        PipelineRunner pipeline,
        CrawlerDatabase db,
        IReadOnlyList<IBatchProcessingStep>? batchSteps = null)
    {
        _fileRepo = fileRepo;
        _logRepo = logRepo;
        _sidecarStore = sidecarStore;
        _resolver = resolver;
        _changeDetector = changeDetector;
        _pipeline = pipeline;
        _db = db;
        _batchSteps = batchSteps ?? [];
    }

    public async Task RunAsync(IReadOnlyList<string> folderPaths, bool fullMode)
    {
        var mode = fullMode ? "full" : "incremental";
        var crawlId = await _logRepo.StartCrawlAsync(mode);
        var filesScanned = 0;
        var filesProcessed = 0;
        var filesErrored = 0;

        try
        {
            var allDiscoveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var targets = await _resolver.ResolveAsync(folderPaths);
            foreach (var target in targets)
            {
                if (!target.Sidecar.Enabled)
                {
                    Log.Information("Folder {FolderPath} is disabled, skipping", target.FolderPath);
                    continue;
                }

                Log.Information("Crawling folder {FolderPath} ({Label})", target.FolderPath, target.Sidecar.Label);
                filesScanned += target.Files.Count;

                foreach (var file in target.Files)
                {
                    allDiscoveredPaths.Add(file.FilePath);

                    try
                    {
                        if (fullMode)
                        {
                            await ProcessFileWithPipelineAsync(file, null);
                            filesProcessed++;
                        }
                        else
                        {
                            var existing = await _fileRepo.GetByPathAsync(file.FilePath);
                            var change = await _changeDetector.DetectChangeAsync(file, existing);

                            switch (change.Kind)
                            {
                                case ChangeKind.Unchanged:
                                    Log.Debug("Skipping unchanged file {FilePath}", file.FilePath);
                                    break;

                                case ChangeKind.ModTimeOnly:
                                    Log.Debug("Mod-time change only for {FilePath}, updating timestamp", file.FilePath);
                                    if (existing is not null)
                                        await _fileRepo.UpdateModifiedAtAsync(existing.Id, file.LastModified);
                                    break;

                                case ChangeKind.New:
                                case ChangeKind.Changed:
                                    await ProcessFileWithPipelineAsync(file, change.ComputedHash);
                                    filesProcessed++;
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing file {FilePath}", file.FilePath);
                        filesErrored++;
                    }
                }
            }

            // Run batch steps (e.g. duplicate detection) across all discovered files
            if (_batchSteps.Count > 0)
            {
                var allPaths = allDiscoveredPaths.ToList();
                var batchContext = new BatchProcessingContext
                {
                    FilePaths = allPaths,
                    SidecarStore = _sidecarStore
                };
                foreach (var batchStep in _batchSteps)
                {
                    Log.Information("Running batch step {StepName}", batchStep.Name);
                    await batchStep.ExecuteAsync(batchContext);
                }
            }

            // Detect deletions
            var activeFiles = await _fileRepo.GetActiveFilesAsync();
            var deletedIds = activeFiles
                .Where(f => !allDiscoveredPaths.Contains(f.FilePath))
                .Select(f => f.Id)
                .ToList();

            if (deletedIds.Count > 0)
            {
                Log.Information("Marking {Count} deleted files", deletedIds.Count);
                await _fileRepo.MarkDeletedAsync(deletedIds);
            }

            await _logRepo.CompleteCrawlAsync(crawlId, "completed", filesScanned, filesProcessed, filesErrored);
            Log.Information("Crawl completed: {Scanned} scanned, {Processed} processed, {Errored} errored",
                filesScanned, filesProcessed, filesErrored);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Crawl failed");
            await _logRepo.CompleteCrawlAsync(crawlId, "failed", filesScanned, filesProcessed, filesErrored, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Upserts the file record, runs the processing pipeline (which writes the sidecar at the
    /// very end of <see cref="PipelineRunner.RunAsync"/>), then commits the DB transaction — so
    /// the DB is only advanced once the sidecar is durably on disk.  A crash before the sidecar
    /// write rolls the transaction back automatically on dispose, leaving both stores in their
    /// prior consistent state.
    /// </summary>
    private async Task ProcessFileWithPipelineAsync(DiscoveredFile file, string? computedHash)
    {
        using var tx = _db.BeginFileTransaction();
        var dbRecord = await _fileRepo.UpsertAsync(file.FilePath, computedHash, file.LastModified, tx);
        await _pipeline.RunAsync(file.FilePath, dbRecord, tx);
        tx.Commit();
    }

    public async Task RunTargetedAsync(IReadOnlyList<string> folderPaths, string stepName)
    {
        var crawlId = await _logRepo.StartCrawlAsync("targeted", stepName);
        var filesScanned = 0;

        try
        {
            var batchStep = _batchSteps.FirstOrDefault(s =>
                string.Equals(s.Name, stepName, StringComparison.OrdinalIgnoreCase));

            if (batchStep is null)
            {
                Log.Error("Unknown targeted step: {StepName}", stepName);
                await _logRepo.CompleteCrawlAsync(crawlId, "failed", 0, 0, 0, $"Unknown step: {stepName}");
                return;
            }

            var allDiscoveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var targets = await _resolver.ResolveAsync(folderPaths);
            foreach (var target in targets)
            {
                if (!target.Sidecar.Enabled)
                    continue;

                filesScanned += target.Files.Count;

                foreach (var file in target.Files)
                {
                    allDiscoveredPaths.Add(file.FilePath);
                    await _fileRepo.UpsertAsync(file.FilePath, null, file.LastModified);
                }
            }

            var batchContext = new BatchProcessingContext
            {
                FilePaths = allDiscoveredPaths.ToList(),
                SidecarStore = _sidecarStore
            };

            Log.Information("Running targeted batch step {StepName} on {Count} files", stepName, allDiscoveredPaths.Count);
            await batchStep.ExecuteAsync(batchContext);

            await _logRepo.CompleteCrawlAsync(crawlId, "completed", filesScanned, 0, 0);
            Log.Information("Targeted crawl ({StepName}) completed: {Scanned} files scanned", stepName, filesScanned);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Targeted crawl failed");
            await _logRepo.CompleteCrawlAsync(crawlId, "failed", filesScanned, 0, 0, ex.Message);
            throw;
        }
    }
}

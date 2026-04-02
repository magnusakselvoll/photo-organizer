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
    private readonly IFileDiscoverer _discoverer;
    private readonly ChangeDetector _changeDetector;
    private readonly PipelineRunner _pipeline;
    private readonly IReadOnlyList<IBatchProcessingStep> _batchSteps;

    public CrawlOrchestrator(
        ICrawledFileRepository fileRepo,
        ICrawlLogRepository logRepo,
        ISidecarStore sidecarStore,
        IFileDiscoverer discoverer,
        ChangeDetector changeDetector,
        PipelineRunner pipeline,
        IReadOnlyList<IBatchProcessingStep>? batchSteps = null)
    {
        _fileRepo = fileRepo;
        _logRepo = logRepo;
        _sidecarStore = sidecarStore;
        _discoverer = discoverer;
        _changeDetector = changeDetector;
        _pipeline = pipeline;
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

            foreach (var folderPath in folderPaths)
            {
                var folderSidecar = await _sidecarStore.ReadFolderSidecarAsync(folderPath);
                if (folderSidecar is null)
                {
                    Log.Warning("No _folder.json found in {FolderPath}, skipping", folderPath);
                    continue;
                }
                if (!folderSidecar.Enabled)
                {
                    Log.Information("Folder {FolderPath} is disabled, skipping", folderPath);
                    continue;
                }

                Log.Information("Crawling folder {FolderPath} ({Label})", folderPath, folderSidecar.Label);
                var discovered = _discoverer.Discover(folderPath);
                filesScanned += discovered.Count;

                foreach (var file in discovered)
                {
                    allDiscoveredPaths.Add(file.FilePath);

                    try
                    {
                        if (fullMode)
                        {
                            var dbRecord = await _fileRepo.UpsertAsync(file.FilePath, null, file.LastModified);
                            await _pipeline.RunAsync(file.FilePath, dbRecord);
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
                                    var dbRecord = await _fileRepo.UpsertAsync(file.FilePath, change.ComputedHash, file.LastModified);
                                    await _pipeline.RunAsync(file.FilePath, dbRecord);
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

            foreach (var folderPath in folderPaths)
            {
                var folderSidecar = await _sidecarStore.ReadFolderSidecarAsync(folderPath);
                if (folderSidecar is null || !folderSidecar.Enabled)
                    continue;

                var discovered = _discoverer.Discover(folderPath);
                filesScanned += discovered.Count;

                foreach (var file in discovered)
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

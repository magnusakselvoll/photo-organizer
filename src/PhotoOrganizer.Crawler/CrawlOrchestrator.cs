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

    public CrawlOrchestrator(
        ICrawledFileRepository fileRepo,
        ICrawlLogRepository logRepo,
        ISidecarStore sidecarStore,
        IFileDiscoverer discoverer,
        ChangeDetector changeDetector,
        PipelineRunner pipeline)
    {
        _fileRepo = fileRepo;
        _logRepo = logRepo;
        _sidecarStore = sidecarStore;
        _discoverer = discoverer;
        _changeDetector = changeDetector;
        _pipeline = pipeline;
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
}

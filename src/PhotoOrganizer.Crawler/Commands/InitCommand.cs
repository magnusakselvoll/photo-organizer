using PhotoOrganizer.Domain.Models;
using Serilog;

namespace PhotoOrganizer.Crawler.Commands;

public static class InitCommand
{
    public static async Task<int> RunAsync(
        string folderPath, string label, string type, bool enabled,
        bool addToConfig, bool deleteExistingMeta, string? configPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Log.Error("Folder does not exist: {FolderPath}", folderPath);
            return 1;
        }

        var absolutePath = Path.GetFullPath(folderPath);

        var sidecarStore = new PhotoOrganizer.Crawler.Sidecars.JsonSidecarStore();
        var sidecar = new FolderSidecar
        {
            Version = 1,
            Label = label,
            Type = type,
            Enabled = enabled
        };

        await sidecarStore.WriteFolderSidecarAsync(absolutePath, sidecar);
        Log.Information("Created _folder.json in {FolderPath}", absolutePath);

        if (addToConfig)
        {
            var config = await ConfigLoader.LoadAsync(configPath);
            if (!config.ScanRoots.Contains(absolutePath, StringComparer.OrdinalIgnoreCase))
            {
                config.ScanRoots.Add(absolutePath);
                await ConfigLoader.SaveAsync(configPath, config);
                Log.Information("Added {FolderPath} to {ConfigPath}",
                    absolutePath, ConfigLoader.ResolvePath(configPath));
            }
            else
            {
                Log.Information("{FolderPath} is already in {ConfigPath}",
                    absolutePath, ConfigLoader.ResolvePath(configPath));
            }
        }

        // Crawl across all configured roots so cross-folder duplicate detection works.
        var runConfig = await ConfigLoader.LoadAsync(configPath);
        var allRoots = runConfig.ScanRoots
            .Where(Directory.Exists)
            .ToList();

        // Ensure the just-initialized folder is included even if --no-add-to-config was given.
        if (!allRoots.Contains(absolutePath, StringComparer.OrdinalIgnoreCase))
            allRoots.Add(absolutePath);

        if (deleteExistingMeta)
        {
            Log.Information("--delete-existing-meta: deleting all .meta.json files before crawl");
            MetaSidecarCleaner.DeleteAll(allRoots);
        }

        using var services = CrawlerServices.Build(runConfig);

        // Run incrementally so already-indexed folders are not fully re-processed, but all
        // discovered files participate in the batch duplicate-detection step.
        // --delete-existing-meta wipes all sidecars, so a full crawl is required.
        var fullMode = deleteExistingMeta;
        if (deleteExistingMeta)
            Log.Information("--delete-existing-meta forces a full crawl");

        await services.Orchestrator.RunAsync(allRoots, fullMode: fullMode);
        return 0;
    }
}

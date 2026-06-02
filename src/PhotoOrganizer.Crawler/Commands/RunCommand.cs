using Serilog;

namespace PhotoOrganizer.Crawler.Commands;

public static class RunCommand
{
    public static async Task<int> RunAsync(string mode, string? step, bool deleteExistingMeta, string? configPath)
    {
        if (string.Equals(mode, "targeted", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(step))
        {
            Log.Error("--step is required when --mode is targeted");
            return 1;
        }

        var config = await ConfigLoader.LoadAsync(configPath);

        if (config.ScanRoots.Count == 0)
        {
            Log.Error("No ScanRoots configured. Add scan roots to your config file.");
            return 1;
        }

        var enabledRoots = config.ScanRoots
            .Where(Directory.Exists)
            .ToList();

        if (enabledRoots.Count == 0)
        {
            Log.Error("None of the configured ScanRoots exist on disk.");
            return 1;
        }

        if (deleteExistingMeta)
        {
            Log.Information("--delete-existing-meta: deleting all .meta.json files before crawl");
            MetaSidecarCleaner.DeleteAll(enabledRoots);
        }

        using var services = CrawlerServices.Build(config);

        if (string.Equals(mode, "targeted", StringComparison.OrdinalIgnoreCase))
        {
            await services.Orchestrator.RunTargetedAsync(enabledRoots, step!);
        }
        else
        {
            // --delete-existing-meta wipes all sidecars, so a full crawl is required to regenerate them.
            var fullMode = deleteExistingMeta || string.Equals(mode, "full", StringComparison.OrdinalIgnoreCase);
            if (deleteExistingMeta && !string.Equals(mode, "full", StringComparison.OrdinalIgnoreCase))
                Log.Information("--delete-existing-meta forces a full crawl");

            await services.Orchestrator.RunAsync(enabledRoots, fullMode);
        }

        return 0;
    }
}

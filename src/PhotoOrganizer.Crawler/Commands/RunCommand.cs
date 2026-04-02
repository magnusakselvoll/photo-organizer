using Serilog;

namespace PhotoOrganizer.Crawler.Commands;

public static class RunCommand
{
    public static async Task<int> RunAsync(string mode, string? step, string? configPath)
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

        using var services = CrawlerServices.Build(config);

        if (string.Equals(mode, "targeted", StringComparison.OrdinalIgnoreCase))
        {
            await services.Orchestrator.RunTargetedAsync(enabledRoots, step!);
        }
        else
        {
            var fullMode = string.Equals(mode, "full", StringComparison.OrdinalIgnoreCase);
            await services.Orchestrator.RunAsync(enabledRoots, fullMode);
        }

        return 0;
    }
}

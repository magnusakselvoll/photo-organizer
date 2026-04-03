using PhotoOrganizer.Domain.Models;
using Serilog;

namespace PhotoOrganizer.Crawler.Commands;

public static class InitCommand
{
    public static async Task<int> RunAsync(
        string folderPath, string label, string type, bool enabled,
        bool addToConfig, string? configPath)
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

        var runConfig = await ConfigLoader.LoadAsync(configPath);
        using var services = CrawlerServices.Build(runConfig);
        await services.Orchestrator.RunAsync([absolutePath], fullMode: true);
        return 0;
    }
}

using PhotoOrganizer.Domain.Models;
using Serilog;

namespace PhotoOrganizer.Crawler.Commands;

public static class InitCommand
{
    public static async Task<int> RunAsync(string folderPath, string label, string type, bool enabled, string? configPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Log.Error("Folder does not exist: {FolderPath}", folderPath);
            return 1;
        }

        var sidecarStore = new PhotoOrganizer.Crawler.Sidecars.JsonSidecarStore();
        var sidecar = new FolderSidecar
        {
            Version = 1,
            Label = label,
            Type = type,
            Enabled = enabled
        };

        await sidecarStore.WriteFolderSidecarAsync(folderPath, sidecar);
        Log.Information("Created _folder.json in {FolderPath}", folderPath);

        var config = await ConfigLoader.LoadAsync(configPath);
        using var services = CrawlerServices.Build(config);
        await services.Orchestrator.RunAsync([folderPath], fullMode: true);
        return 0;
    }
}

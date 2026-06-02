using PhotoOrganizer.Crawler.Discovery;
using Serilog;

namespace PhotoOrganizer.Crawler;

/// <summary>
/// Deletes all photo <c>.meta.json</c> sidecar files under the given root folders.
/// Folder configuration files (<c>_folder.json</c>) are not touched.
/// Uses <see cref="ResilientFileWalker"/> so inaccessible sub-directories are skipped
/// rather than causing the whole operation to abort.
/// </summary>
public static class MetaSidecarCleaner
{
    public static void DeleteAll(IEnumerable<string> roots)
    {
        var totalDeleted = 0;
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                Log.Warning("MetaSidecarCleaner: root does not exist, skipping: {Root}", root);
                continue;
            }

            Log.Information("MetaSidecarCleaner: scanning {Root}", root);
            var deleted = 0;
            foreach (var file in ResilientFileWalker.EnumerateFiles(root, "*.meta.json"))
            {
                // _folder.json happens not to match "*.meta.json", but guard anyway.
                if (Path.GetFileName(file).Equals("_folder.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Warning("MetaSidecarCleaner: could not delete {File}: {Message}", file, ex.Message);
                }
            }

            Log.Information("MetaSidecarCleaner: deleted {Count} .meta.json files under {Root}", deleted, root);
            totalDeleted += deleted;
        }

        if (totalDeleted > 0)
            Log.Information("MetaSidecarCleaner: deleted {Total} .meta.json files in total", totalDeleted);
    }
}

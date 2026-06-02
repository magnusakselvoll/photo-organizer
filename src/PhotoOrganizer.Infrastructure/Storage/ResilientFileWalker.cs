using Microsoft.Extensions.Logging;

namespace PhotoOrganizer.Infrastructure.Storage;

/// <summary>
/// Provides resilient recursive file enumeration that skips inaccessible or reparse-point
/// directories (e.g. network recycle bins, broken symlinks) instead of crashing.
/// </summary>
public static class ResilientFileWalker
{
    /// <summary>
    /// Enumerates all files matching <paramref name="searchPattern"/> under <paramref name="root"/>,
    /// recursing into subdirectories. Directories that are reparse points (symlinks, junctions) are
    /// skipped to avoid loops. Directories that throw <see cref="IOException"/> or
    /// <see cref="UnauthorizedAccessException"/> are also skipped with a logged warning.
    /// Results within each directory are returned in ordinal sort order.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(string root, string searchPattern, ILogger logger)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            string[] files;
            string[] subDirs;
            try
            {
                files = Directory.GetFiles(current, searchPattern);
                subDirs = Directory.GetDirectories(current)
                    .Where(d => (new DirectoryInfo(d).Attributes & FileAttributes.ReparsePoint) == 0)
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Skipping inaccessible directory {Directory}: {Message}", current, ex.Message);
                continue;
            }

            Array.Sort(files, StringComparer.Ordinal);
            Array.Sort(subDirs, StringComparer.Ordinal);

            foreach (var file in files)
                yield return file;

            // Push in reverse so subdirectories are popped in sorted order
            for (var i = subDirs.Length - 1; i >= 0; i--)
                stack.Push(subDirs[i]);
        }
    }
}

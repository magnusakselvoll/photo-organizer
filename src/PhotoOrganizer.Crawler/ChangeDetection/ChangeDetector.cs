using PhotoOrganizer.Crawler.Data;
using PhotoOrganizer.Crawler.Discovery;

namespace PhotoOrganizer.Crawler.ChangeDetection;

public sealed class ChangeDetector
{
    private readonly FileHasher _hasher;

    public ChangeDetector(FileHasher hasher) => _hasher = hasher;

    public async Task<ChangeResult> DetectChangeAsync(DiscoveredFile file, CrawledFileRecord? existing)
    {
        if (existing is null)
            return new ChangeResult(ChangeKind.New);

        if (existing.ModifiedAt.HasValue &&
            Math.Abs((existing.ModifiedAt.Value - file.LastModified).TotalSeconds) < 1)
            return new ChangeResult(ChangeKind.Unchanged);

        var hash = await _hasher.ComputeHashAsync(file.FilePath);

        if (existing.FileHash is not null &&
            string.Equals(existing.FileHash, hash, StringComparison.OrdinalIgnoreCase))
            return new ChangeResult(ChangeKind.ModTimeOnly, hash);

        return new ChangeResult(ChangeKind.Changed, hash);
    }
}

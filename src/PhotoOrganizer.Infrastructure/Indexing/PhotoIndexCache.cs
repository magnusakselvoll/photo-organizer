using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhotoOrganizer.Application;
using PhotoOrganizer.Domain;

namespace PhotoOrganizer.Infrastructure.Indexing;

/// <summary>
/// Persists the in-memory PhotoIndex to a server-owned JSON file in the system temp directory.
/// On the next server start, if the cache file is younger than the TTL and was built from the
/// same ScanRoots, it is loaded immediately (instant warm start — no SMB walk needed).
///
/// The cache is a rebuildable derived artifact. Deleting it is always safe; a full index
/// rebuild from sidecars will produce an identical result.
/// </summary>
public sealed class PhotoIndexCache
{
    private const int CacheFormatVersion = 1;

    private readonly PhotoOrganizerSettings _settings;
    private readonly ILogger<PhotoIndexCache> _logger;

    public PhotoIndexCache(IOptions<PhotoOrganizerSettings> settings, ILogger<PhotoIndexCache> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Tries to load a valid, non-expired cache into <paramref name="index"/>.
    /// Returns true and populates the index on success; returns false if the cache is
    /// absent, expired, or was built from different ScanRoots.
    /// </summary>
    public async Task<bool> TryLoadAsync(PhotoIndex index, CancellationToken cancellationToken)
    {
        var path = GetCachePath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        try
        {
            var ttl = TimeSpan.FromHours(_settings.Indexing.CacheTtlHours);
            var age = DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(path);
            if (age > ttl)
            {
                _logger.LogInformation("Photo index cache expired (age {Age:g} > TTL {TTL:g}), rebuilding", age, ttl);
                return false;
            }

            await using var stream = File.OpenRead(path);
            var envelope = await JsonSerializer.DeserializeAsync<CacheEnvelope>(stream,
                JsonOptions, cancellationToken);

            if (envelope is null || envelope.Version != CacheFormatVersion)
                return false;

            if (envelope.ScanRootsHash != ComputeScanRootsHash(_settings.ScanRoots))
            {
                _logger.LogInformation("Photo index cache is for different ScanRoots, rebuilding");
                return false;
            }

            foreach (var f in envelope.Folders)
                index.AddFolder(f);
            foreach (var p in envelope.Photos)
                index.AddPhoto(p);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load photo index cache from {Path}, rebuilding", path);
            return false;
        }
    }

    /// <summary>Saves a snapshot of the current index to the cache file.</summary>
    public async Task SaveAsync(PhotoIndex index, CancellationToken cancellationToken)
    {
        var path = GetCachePath();
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);

            var envelope = new CacheEnvelope
            {
                Version = CacheFormatVersion,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ScanRootsHash = ComputeScanRootsHash(_settings.ScanRoots),
                Photos = index.SnapshotPhotos().ToList(),
                Folders = index.SnapshotFolders().ToList()
            };

            var tmp = path + ".tmp";
            await using (var stream = File.Create(tmp))
                await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, cancellationToken);

            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not save photo index cache to {Path}", path);
        }
    }

    /// <summary>Deletes the cache file (called before a forced rebuild).</summary>
    public Task DeleteAsync()
    {
        var path = GetCachePath();
        if (!string.IsNullOrEmpty(path))
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
        return Task.CompletedTask;
    }

    private string GetCachePath()
    {
        var dir = string.IsNullOrEmpty(_settings.Indexing.CacheDirectory)
            ? Path.Combine(Path.GetTempPath(), "PhotoOrganizer")
            : _settings.Indexing.CacheDirectory;
        return string.IsNullOrEmpty(dir) ? string.Empty : Path.Combine(dir, "photo-index.json");
    }

    private static string ComputeScanRootsHash(string[] roots)
    {
        var joined = string.Join("|", roots.OrderBy(r => r, StringComparer.OrdinalIgnoreCase));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexStringLower(hash)[..16];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    // Cache envelope — internal type, not part of the public API or sidecar contract.
    private sealed class CacheEnvelope
    {
        public int Version { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string ScanRootsHash { get; set; } = "";
        public List<Photo> Photos { get; set; } = [];
        public List<SourceFolder> Folders { get; set; } = [];
    }
}

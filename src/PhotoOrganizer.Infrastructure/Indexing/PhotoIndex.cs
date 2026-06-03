using System.Collections.Concurrent;
using PhotoOrganizer.Domain;

namespace PhotoOrganizer.Infrastructure.Indexing;

/// <summary>
/// Thread-safe in-memory index built progressively by the background indexer.
/// Readers always get a non-blocking snapshot; writes from concurrent indexer
/// workers are safe via ConcurrentDictionary.
/// </summary>
public sealed class PhotoIndex
{
    private readonly ConcurrentDictionary<Guid, Photo> _photos = new();
    private readonly ConcurrentDictionary<string, SourceFolder> _folders =
        new(StringComparer.OrdinalIgnoreCase);

    private volatile bool _isComplete;

    /// <summary>True once the background indexer has finished walking all directories.</summary>
    public bool IsComplete => _isComplete;

    /// <summary>Number of photos currently indexed (updates continuously as indexing runs).</summary>
    public int Count => _photos.Count;

    /// <summary>Adds or replaces a photo in the index. Idempotent by photo Id.</summary>
    public void AddPhoto(Photo photo) => _photos[photo.Id] = photo;

    /// <summary>Adds or replaces a folder in the index. Idempotent by path.</summary>
    public void AddFolder(SourceFolder folder) => _folders[folder.Path] = folder;

    /// <summary>Returns a point-in-time snapshot of all indexed photos.</summary>
    public IReadOnlyList<Photo> SnapshotPhotos() => [.. _photos.Values];

    /// <summary>Returns a point-in-time snapshot of all indexed folders.</summary>
    public IReadOnlyList<SourceFolder> SnapshotFolders() => [.. _folders.Values];

    /// <summary>Looks up a single photo by Id without allocating a snapshot.</summary>
    public Photo? GetById(Guid id) => _photos.GetValueOrDefault(id);

    /// <summary>Marks the index as fully built and ready.</summary>
    public void MarkComplete() => _isComplete = true;
}

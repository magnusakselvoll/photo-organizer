using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;

namespace PhotoOrganizer.Infrastructure.Indexing;

/// <summary>
/// IFolderRepository implementation that reads from the in-memory PhotoIndex.
/// Never blocks — returns whatever folders have been discovered so far.
/// </summary>
public sealed class IndexFolderRepository(PhotoIndex index) : IFolderRepository
{
    public Task<IReadOnlyList<SourceFolder>> GetAllFoldersAsync() =>
        Task.FromResult(index.SnapshotFolders());

    public Task<SourceFolder?> GetFolderByPathAsync(string path)
    {
        var folders = index.SnapshotFolders();
        var match = folders.FirstOrDefault(f =>
            string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match);
    }
}

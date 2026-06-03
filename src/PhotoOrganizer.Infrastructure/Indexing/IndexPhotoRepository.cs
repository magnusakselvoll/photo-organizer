using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;

namespace PhotoOrganizer.Infrastructure.Indexing;

/// <summary>
/// IPhotoRepository implementation that reads from the in-memory PhotoIndex.
/// Never blocks — returns whatever is indexed right now (index grows in background).
/// </summary>
public sealed class IndexPhotoRepository(PhotoIndex index) : IPhotoRepository
{
    public Task<IReadOnlyList<Photo>> GetAllPhotosAsync() =>
        Task.FromResult(index.SnapshotPhotos());

    public Task<Photo?> GetByIdAsync(Guid id) =>
        Task.FromResult(index.GetById(id));
}

namespace PhotoOrganizer.Domain.Interfaces;

public interface IPhotoRepository
{
    Task<IReadOnlyList<Photo>> GetAllPhotosAsync();
    Task<Photo?> GetByIdAsync(Guid id);
    Task InvalidateCacheAsync();
}

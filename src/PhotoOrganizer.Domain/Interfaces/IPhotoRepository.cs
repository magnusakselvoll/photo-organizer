namespace PhotoOrganizer.Domain.Interfaces;

public interface IPhotoRepository
{
    /// <summary>
    /// Monotonic version counter for the photo collection. Changes whenever a photo is added
    /// or updated; stable once the index is fully built. Used by <c>PhotoService</c> to
    /// invalidate its sorted-view cache without polling (see ADR 010).
    /// </summary>
    long Version { get; }

    Task<IReadOnlyList<Photo>> GetAllPhotosAsync();
    Task<Photo?> GetByIdAsync(Guid id);
}

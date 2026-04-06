namespace PhotoOrganizer.Application.Photos;

public interface IPhotoService
{
    Task<PhotoPageDto> GetPhotosAsync(PhotoFilter filter);
    Task<PhotoDto?> GetPhotoByIdAsync(Guid id);
}

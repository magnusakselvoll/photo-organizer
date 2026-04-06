using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;

namespace PhotoOrganizer.Infrastructure.Services;

public sealed class PhotoService(IPhotoRepository repository) : IPhotoService
{
    public async Task<PhotoPageDto> GetPhotosAsync(PhotoFilter filter)
    {
        var all = await repository.GetAllPhotosAsync();
        var filtered = ApplyFilters(all, filter);

        var totalCount = filtered.Count;
        var items = filtered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(ToDto)
            .ToList();

        return new PhotoPageDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<PhotoDto?> GetPhotoByIdAsync(Guid id)
    {
        var photo = await repository.GetByIdAsync(id);
        return photo is null ? null : ToDto(photo);
    }

    private static List<Photo> ApplyFilters(IReadOnlyList<Photo> photos, PhotoFilter filter)
    {
        IEnumerable<Photo> result = photos;

        if (filter.Folder is not null)
            result = result.Where(p => p.FilePath.StartsWith(filter.Folder, StringComparison.OrdinalIgnoreCase));

        if (filter.Type is not null && !filter.Type.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var folderType = FolderTypeExtensions.Parse(filter.Type);
            result = result.Where(p => p.FolderType == folderType);
        }

        if (filter.Deduplicated)
            result = Deduplicate(result);

        return result.ToList();
    }

    private static IEnumerable<Photo> Deduplicate(IEnumerable<Photo> photos)
    {
        var seen = new HashSet<Guid>();
        foreach (var photo in photos)
        {
            if (photo.DuplicateGroupId is null)
            {
                yield return photo;
            }
            else if (photo.IsPreferred && seen.Add(photo.DuplicateGroupId.Value))
            {
                yield return photo;
            }
        }
    }

    private static PhotoDto ToDto(Photo photo) => new()
    {
        Id = photo.Id,
        FilePath = photo.FilePath,
        FileName = photo.FileName,
        CapturedAt = photo.CapturedAt,
        FolderType = photo.FolderType.ToString(),
        DuplicateGroupId = photo.DuplicateGroupId,
        IsPreferred = photo.IsPreferred,
        Tags = photo.Tags
    };
}

using System.Security.Cryptography;
using System.Text;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;

namespace PhotoOrganizer.Infrastructure.Storage;

public sealed class FileSystemPhotoRepository : IPhotoRepository
{
    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic",
        ".cr2", ".cr3", ".orf", ".arw", ".nef", ".rw2",
        ".tiff", ".tif"
    };

    private readonly IFolderRepository _folderRepository;
    private readonly ISidecarReader _sidecarReader;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<Photo>? _cache;
    private Dictionary<Guid, Photo>? _cacheById;

    public FileSystemPhotoRepository(IFolderRepository folderRepository, ISidecarReader sidecarReader)
    {
        _folderRepository = folderRepository;
        _sidecarReader = sidecarReader;
    }

    public async Task<IReadOnlyList<Photo>> GetAllPhotosAsync()
    {
        if (_cache is not null)
            return _cache;

        await _lock.WaitAsync();
        try
        {
            if (_cache is not null)
                return _cache;

            var photos = await LoadPhotosAsync();
            _cache = photos;
            _cacheById = photos.ToDictionary(p => p.Id);
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Photo?> GetByIdAsync(Guid id)
    {
        await GetAllPhotosAsync();
        return _cacheById!.GetValueOrDefault(id);
    }

    public async Task InvalidateCacheAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _cache = null;
            _cacheById = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<Photo>> LoadPhotosAsync()
    {
        var folders = await _folderRepository.GetAllFoldersAsync();
        var photos = new List<Photo>();

        foreach (var folder in folders.Where(f => f.Enabled))
        {
            if (!Directory.Exists(folder.Path))
                continue;

            foreach (var filePath in Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(filePath);
                if (!PhotoExtensions.Contains(ext))
                    continue;

                var photo = await BuildPhotoAsync(filePath, folder.Type);
                photos.Add(photo);
            }
        }

        return photos;
    }

    private async Task<Photo> BuildPhotoAsync(string filePath, FolderType folderType)
    {
        var sidecar = await _sidecarReader.ReadPhotoMetaAsync(filePath);

        return new Photo
        {
            Id = DeterministicGuid(filePath),
            FilePath = filePath,
            FileName = Path.GetFileNameWithoutExtension(filePath),
            CapturedAt = sidecar?.CapturedAt,
            FolderType = folderType,
            DuplicateGroupId = sidecar?.DuplicateGroupId,
            IsPreferred = sidecar?.IsPreferred ?? false,
            Tags = (IReadOnlyList<string>?)sidecar?.Tags ?? []
        };
    }

    private static Guid DeterministicGuid(string filePath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(filePath));
        return new Guid(hash[..16]);
    }
}

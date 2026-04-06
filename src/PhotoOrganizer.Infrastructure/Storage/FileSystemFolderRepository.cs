using Microsoft.Extensions.Options;
using PhotoOrganizer.Application;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;

namespace PhotoOrganizer.Infrastructure.Storage;

public sealed class FileSystemFolderRepository : IFolderRepository
{
    private readonly PhotoOrganizerSettings _settings;
    private readonly ISidecarReader _sidecarReader;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<SourceFolder>? _cache;

    public FileSystemFolderRepository(IOptions<PhotoOrganizerSettings> settings, ISidecarReader sidecarReader)
    {
        _settings = settings.Value;
        _sidecarReader = sidecarReader;
    }

    public async Task<IReadOnlyList<SourceFolder>> GetAllFoldersAsync()
    {
        if (_cache is not null)
            return _cache;

        await _lock.WaitAsync();
        try
        {
            if (_cache is not null)
                return _cache;

            _cache = await LoadFoldersAsync();
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<SourceFolder?> GetFolderByPathAsync(string path)
    {
        var folders = await GetAllFoldersAsync();
        return folders.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<SourceFolder>> LoadFoldersAsync()
    {
        var folders = new List<SourceFolder>();

        foreach (var scanRoot in _settings.ScanRoots)
        {
            if (!Directory.Exists(scanRoot))
                continue;

            foreach (var sidecarFile in Directory.EnumerateFiles(scanRoot, "_folder.json", SearchOption.AllDirectories))
            {
                var folderPath = Path.GetDirectoryName(sidecarFile);
                if (folderPath is null)
                    continue;

                var sidecar = await _sidecarReader.ReadFolderSidecarAsync(folderPath);
                if (sidecar is null)
                    continue;

                folders.Add(new SourceFolder
                {
                    Path = folderPath,
                    Label = sidecar.Label,
                    Type = FolderTypeExtensions.Parse(sidecar.Type),
                    Enabled = sidecar.Enabled
                });
            }
        }

        return folders;
    }
}

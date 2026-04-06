using PhotoOrganizer.Application.Folders;
using PhotoOrganizer.Domain.Interfaces;

namespace PhotoOrganizer.Infrastructure.Services;

public sealed class FolderService(IFolderRepository repository) : IFolderService
{
    public async Task<IReadOnlyList<FolderDto>> GetAllFoldersAsync()
    {
        var folders = await repository.GetAllFoldersAsync();
        return folders.Select(f => new FolderDto
        {
            Path = f.Path,
            Label = f.Label,
            Type = f.Type.ToString(),
            Enabled = f.Enabled
        }).ToList();
    }
}

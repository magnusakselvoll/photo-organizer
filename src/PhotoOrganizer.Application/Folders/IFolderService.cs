namespace PhotoOrganizer.Application.Folders;

public interface IFolderService
{
    Task<IReadOnlyList<FolderDto>> GetAllFoldersAsync();
}

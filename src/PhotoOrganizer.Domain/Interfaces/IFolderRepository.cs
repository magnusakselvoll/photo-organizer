namespace PhotoOrganizer.Domain.Interfaces;

public interface IFolderRepository
{
    Task<IReadOnlyList<SourceFolder>> GetAllFoldersAsync();
    Task<SourceFolder?> GetFolderByPathAsync(string path);
}

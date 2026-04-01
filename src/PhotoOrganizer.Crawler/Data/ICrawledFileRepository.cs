namespace PhotoOrganizer.Crawler.Data;

public interface ICrawledFileRepository
{
    Task<CrawledFileRecord?> GetByPathAsync(string filePath);
    Task<CrawledFileRecord> UpsertAsync(string filePath, string? fileHash, DateTimeOffset modifiedAt);
    Task MarkDeletedAsync(IEnumerable<int> fileIds);
    Task UpdateModifiedAtAsync(int fileId, DateTimeOffset modifiedAt);
    Task<IReadOnlyList<CrawledFileRecord>> GetActiveFilesAsync();
    Task RecordStepRunAsync(int fileId, string stepName, int stepVersion, string status, string? errorMessage);
}

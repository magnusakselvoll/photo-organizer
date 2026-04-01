using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.Crawler.Sidecars;

public interface ISidecarStore
{
    Task<PhotoMetaSidecar?> ReadPhotoMetaAsync(string photoFilePath);
    Task WritePhotoMetaAsync(string photoFilePath, PhotoMetaSidecar sidecar);
    Task<FolderSidecar?> ReadFolderSidecarAsync(string folderPath);
    Task WriteFolderSidecarAsync(string folderPath, FolderSidecar sidecar);
}

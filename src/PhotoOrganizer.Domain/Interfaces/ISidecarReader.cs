using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.Domain.Interfaces;

public interface ISidecarReader
{
    Task<PhotoMetaSidecar?> ReadPhotoMetaAsync(string photoFilePath);
    Task<FolderSidecar?> ReadFolderSidecarAsync(string folderPath);
}

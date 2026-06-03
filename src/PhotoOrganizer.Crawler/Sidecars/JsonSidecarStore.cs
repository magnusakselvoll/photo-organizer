using System.Text.Json;
using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.Crawler.Sidecars;

public sealed class JsonSidecarStore : ISidecarStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<PhotoMetaSidecar?> ReadPhotoMetaAsync(string photoFilePath)
    {
        var sidecarPath = GetPhotoMetaPath(photoFilePath);
        if (!File.Exists(sidecarPath))
            return null;
        await using var stream = File.OpenRead(sidecarPath);
        return await JsonSerializer.DeserializeAsync<PhotoMetaSidecar>(stream, Options);
    }

    public async Task WritePhotoMetaAsync(string photoFilePath, PhotoMetaSidecar sidecar)
    {
        var sidecarPath = GetPhotoMetaPath(photoFilePath);
        await WriteAtomicAsync(sidecarPath, sidecar);
    }

    public async Task<FolderSidecar?> ReadFolderSidecarAsync(string folderPath)
    {
        var sidecarPath = Path.Combine(folderPath, "_folder.json");
        if (!File.Exists(sidecarPath))
            return null;
        await using var stream = File.OpenRead(sidecarPath);
        return await JsonSerializer.DeserializeAsync<FolderSidecar>(stream, Options);
    }

    public async Task WriteFolderSidecarAsync(string folderPath, FolderSidecar sidecar)
    {
        var sidecarPath = Path.Combine(folderPath, "_folder.json");
        await WriteAtomicAsync(sidecarPath, sidecar);
    }

    private async Task WriteAtomicAsync<T>(string targetPath, T value)
    {
        var dir = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var tempPath = Path.Combine(dir, Path.GetRandomFileName());
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, Options);
            }
            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private static string GetPhotoMetaPath(string photoFilePath)
    {
        var dir = Path.GetDirectoryName(photoFilePath) ?? string.Empty;
        var fileName = Path.GetFileName(photoFilePath);
        return Path.Combine(dir, $"{fileName}.meta.json");
    }
}

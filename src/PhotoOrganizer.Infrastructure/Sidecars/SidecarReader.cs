using System.Text.Json;
using PhotoOrganizer.Domain.Exceptions;
using PhotoOrganizer.Domain.Interfaces;
using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.Infrastructure.Sidecars;

public sealed class SidecarReader : ISidecarReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<PhotoMetaSidecar?> ReadPhotoMetaAsync(string photoFilePath)
    {
        var sidecarPath = GetPhotoMetaPath(photoFilePath);
        if (!File.Exists(sidecarPath))
            return null;

        try
        {
            await using var stream = File.OpenRead(sidecarPath);
            return await JsonSerializer.DeserializeAsync<PhotoMetaSidecar>(stream, Options);
        }
        catch (JsonException ex)
        {
            throw new SidecarParsingException(sidecarPath, ex);
        }
    }

    public async Task<FolderSidecar?> ReadFolderSidecarAsync(string folderPath)
    {
        var sidecarPath = Path.Combine(folderPath, "_folder.json");
        if (!File.Exists(sidecarPath))
            return null;

        try
        {
            await using var stream = File.OpenRead(sidecarPath);
            return await JsonSerializer.DeserializeAsync<FolderSidecar>(stream, Options);
        }
        catch (JsonException ex)
        {
            throw new SidecarParsingException(sidecarPath, ex);
        }
    }

    private static string GetPhotoMetaPath(string photoFilePath)
    {
        var dir = Path.GetDirectoryName(photoFilePath) ?? string.Empty;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(photoFilePath);
        return Path.Combine(dir, $"{nameWithoutExt}.meta.json");
    }
}

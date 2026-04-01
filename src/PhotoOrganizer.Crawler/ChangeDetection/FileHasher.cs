using System.Security.Cryptography;

namespace PhotoOrganizer.Crawler.ChangeDetection;

public class FileHasher
{
    public virtual async Task<string> ComputeHashAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexStringLower(hash);
    }
}

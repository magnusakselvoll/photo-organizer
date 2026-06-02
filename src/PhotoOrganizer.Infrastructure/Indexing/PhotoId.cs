using System.Security.Cryptography;
using System.Text;

namespace PhotoOrganizer.Infrastructure.Indexing;

/// <summary>
/// Computes stable, deterministic photo IDs from file paths.
/// Both the randomized indexer and the legacy file-system repository use this,
/// ensuring the same photo always gets the same GUID regardless of which
/// repository implementation is active.
/// </summary>
public static class PhotoId
{
    /// <summary>
    /// Returns a deterministic GUID derived from the SHA-256 hash of the UTF-8 file path.
    /// The first 16 bytes of the hash are used as the GUID bytes.
    /// </summary>
    public static Guid FromFilePath(string filePath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(filePath));
        return new Guid(hash[..16]);
    }
}

using ImageMagick;
using PhotoOrganizer.Domain.Interfaces;

namespace PhotoOrganizer.Infrastructure.Imaging;

/// <summary>
/// Transcodes HEIC/HEIF images to JPEG using Magick.NET (ImageMagick with libheif support).
/// </summary>
public class MagickImageTranscoder : IImageTranscoder
{
    private static readonly HashSet<string> TranscodableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".heic",
        ".heif",
    };

    public bool IsTranscodable(string filePath) =>
        TranscodableExtensions.Contains(Path.GetExtension(filePath));

    public Task<Stream> TranscodeToJpegAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // MagickImage operations are synchronous; wrap in Task.Run to avoid blocking the request thread.
        return Task.Run<Stream>(() =>
        {
            using var image = new MagickImage(filePath);
            image.Format = MagickFormat.Jpeg;

            var output = new MemoryStream();
            image.Write(output);
            output.Position = 0;
            return output;
        }, cancellationToken);
    }
}

namespace PhotoOrganizer.Domain.Interfaces;

/// <summary>
/// Transcodes image files that browsers cannot natively render (e.g. HEIC) to JPEG on the fly.
/// </summary>
public interface IImageTranscoder
{
    /// <summary>Returns true when the given file path requires transcoding for browser display.</summary>
    bool IsTranscodable(string filePath);

    /// <summary>
    /// Transcodes the image at <paramref name="filePath"/> to JPEG and returns a seekable stream
    /// containing the JPEG bytes. The caller is responsible for disposing the stream.
    /// </summary>
    Task<Stream> TranscodeToJpegAsync(string filePath, CancellationToken cancellationToken = default);
}

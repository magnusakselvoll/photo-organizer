using ImageMagick;
using PhotoOrganizer.Infrastructure.Imaging;

namespace PhotoOrganizer.Infrastructure.Tests;

/// <summary>
/// Unit tests for <see cref="MagickImageTranscoder"/>.
/// These tests run against the real Magick.NET library but use no network or server — they are
/// unit tests (no <c>[TestCategory("Integration")]</c>).
/// </summary>
[TestClass]
public sealed class MagickImageTranscoderTests
{
    private readonly MagickImageTranscoder _transcoder = new();

    // --- IsTranscodable ---

    [TestMethod]
    [DataRow("photo.heic", true,  DisplayName = ".heic → transcodable")]
    [DataRow("photo.heif", true,  DisplayName = ".heif → transcodable")]
    [DataRow("photo.HEIC", true,  DisplayName = ".HEIC (upper) → transcodable")]
    [DataRow("photo.HEIF", true,  DisplayName = ".HEIF (upper) → transcodable")]
    [DataRow("photo.jpg",  false, DisplayName = ".jpg → not transcodable")]
    [DataRow("photo.png",  false, DisplayName = ".png → not transcodable")]
    [DataRow("photo.cr2",  false, DisplayName = ".cr2 → not transcodable")]
    public void IsTranscodable_ReturnsExpected(string filePath, bool expected)
    {
        Assert.AreEqual(expected, _transcoder.IsTranscodable(filePath));
    }

    // --- TranscodeToJpegAsync: corrupt file ---

    [TestMethod]
    public async Task TranscodeToJpegAsync_CorruptFile_ThrowsMagickException()
    {
        // Arrange: write garbage bytes into a temp *.heic file.
        var tempPath = Path.Combine(Path.GetTempPath(), $"corrupt-{Guid.NewGuid():N}.heic");
        try
        {
            await File.WriteAllBytesAsync(tempPath, [0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE]);

            // Act & Assert
            try
            {
                await _transcoder.TranscodeToJpegAsync(tempPath);
                Assert.Fail("Expected MagickException was not thrown for a corrupt HEIC file");
            }
            catch (MagickException)
            {
                // Expected — corrupt file should fail with a Magick error.
            }
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    // --- TranscodeToJpegAsync: pre-cancelled token ---

    [TestMethod]
    public async Task TranscodeToJpegAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"cancel-{Guid.NewGuid():N}.heic");
        try
        {
            // The file just needs to exist; Task.Run will respect the cancelled token before
            // even entering the Magick work if the token is already cancelled.
            await File.WriteAllBytesAsync(tempPath, [0x00]);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                await _transcoder.TranscodeToJpegAsync(tempPath, cts.Token);
                Assert.Fail("Expected OperationCanceledException was not thrown for a pre-cancelled token");
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}

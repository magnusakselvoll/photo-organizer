using PhotoOrganizer.Crawler.Data;
using PhotoOrganizer.Crawler.Pipeline;
using PhotoOrganizer.Crawler.Steps;
using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.Crawler.Tests;

[TestClass]
public class MetadataStepTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Initialize() =>
        _tempDir = Directory.CreateTempSubdirectory("photo-organizer-tests-").FullName;

    [TestCleanup]
    public void Cleanup() =>
        Directory.Delete(_tempDir, recursive: true);

    [TestMethod]
    public async Task ExtractsCapturedAt_FromExifData()
    {
        // Minimal JPEG with EXIF containing DateTimeOriginal = "2023:07:14 18:30:00"
        var photoPath = Path.Combine(_tempDir, "with_exif.jpg");
        await File.WriteAllBytesAsync(photoPath, JpegWithExifDate());

        var step = new MetadataStep();
        var context = MakeContext(photoPath);
        await step.ExecuteAsync(context);

        Assert.IsNotNull(context.Sidecar.CapturedAt);
        var captured = context.Sidecar.CapturedAt!.Value;
        Assert.AreEqual(2023, captured.Year);
        Assert.AreEqual(7, captured.Month);
        Assert.AreEqual(14, captured.Day);
        Assert.AreEqual(18, captured.Hour);
        Assert.AreEqual(30, captured.Minute);
    }

    [TestMethod]
    public async Task SetsCapturedAtToNull_WhenNoExif()
    {
        // Minimal valid JPEG with no EXIF
        var photoPath = Path.Combine(_tempDir, "no_exif.jpg");
        await File.WriteAllBytesAsync(photoPath, MinimalJpegNoExif());

        var step = new MetadataStep();
        var context = MakeContext(photoPath);
        await step.ExecuteAsync(context);

        Assert.IsNull(context.Sidecar.CapturedAt);
    }

    [TestMethod]
    public async Task HandlesUnreadableFile_Gracefully()
    {
        // A file that exists but contains garbage data
        var photoPath = Path.Combine(_tempDir, "garbage.jpg");
        await File.WriteAllBytesAsync(photoPath, [0xFF, 0x00, 0x01, 0x02]);

        var step = new MetadataStep();
        var context = MakeContext(photoPath);

        // Should not throw — should just set CapturedAt to null
        await step.ExecuteAsync(context);
        Assert.IsNull(context.Sidecar.CapturedAt);
    }

    [TestMethod]
    public void HasCorrectMetadata()
    {
        var step = new MetadataStep();
        Assert.AreEqual("metadata", step.Name);
        Assert.AreEqual(1, step.Version);
        Assert.AreEqual(0, step.DependsOn.Count);
    }

    private static ProcessingContext MakeContext(string filePath) => new()
    {
        FilePath = filePath,
        Sidecar = new PhotoMetaSidecar(),
        DbRecord = new CrawledFileRecord { Id = 1, FilePath = filePath, FirstSeenAt = DateTimeOffset.UtcNow }
    };

    /// <summary>
    /// Minimal JPEG bytes with an EXIF segment containing DateTimeOriginal = "2023:07:14 18:30:00".
    /// Constructed manually to avoid external test assets.
    /// </summary>
    private static byte[] JpegWithExifDate()
    {
        // JPEG SOI
        var soi = new byte[] { 0xFF, 0xD8 };

        // EXIF APP1 segment
        // Structure: FF E1 [length 2 bytes] "Exif\0\0" [TIFF header] [IFD0] [SubIFD with DateTimeOriginal]
        var exifHeader = new byte[] { 0x45, 0x78, 0x69, 0x66, 0x00, 0x00 }; // "Exif\0\0"

        // TIFF header (little-endian)
        var tiffHeader = new byte[]
        {
            0x49, 0x49,             // "II" = little-endian
            0x2A, 0x00,             // magic = 42
            0x08, 0x00, 0x00, 0x00  // IFD0 offset = 8
        };

        // IFD0: 1 entry pointing to SubIFD
        // Tag 0x8769 = ExifSubIFD, Type = LONG (4), Count = 1, Value = offset to SubIFD
        // SubIFD starts after IFD0: offset = 8 (tiff header) + 2 (count) + 12 (entry) + 4 (next IFD ptr) = 26
        var subIfdOffset = 26u;
        var ifd0 = new byte[]
        {
            0x01, 0x00,                         // entry count = 1
            0x69, 0x87,                         // tag = 0x8769 ExifSubIFD
            0x04, 0x00,                         // type = LONG
            0x01, 0x00, 0x00, 0x00,             // count = 1
            (byte)(subIfdOffset & 0xFF),
            (byte)((subIfdOffset >> 8) & 0xFF),
            (byte)((subIfdOffset >> 16) & 0xFF),
            (byte)((subIfdOffset >> 24) & 0xFF), // value = offset to SubIFD
            0x00, 0x00, 0x00, 0x00              // next IFD = 0 (none)
        };

        // SubIFD: 1 entry for DateTimeOriginal (tag 0x9003)
        // DateTimeOriginal value = "2023:07:14 18:30:00\0" = 20 bytes
        // String data starts after SubIFD: offset = 26 + 2 + 12 + 4 = 44
        var stringOffset = 44u;
        var dateString = System.Text.Encoding.ASCII.GetBytes("2023:07:14 18:30:00\0");
        var subIfd = new byte[]
        {
            0x01, 0x00,                          // entry count = 1
            0x03, 0x90,                          // tag = 0x9003 DateTimeOriginal
            0x02, 0x00,                          // type = ASCII
            0x14, 0x00, 0x00, 0x00,              // count = 20
            (byte)(stringOffset & 0xFF),
            (byte)((stringOffset >> 8) & 0xFF),
            (byte)((stringOffset >> 16) & 0xFF),
            (byte)((stringOffset >> 24) & 0xFF), // offset to string
            0x00, 0x00, 0x00, 0x00               // next IFD = 0
        };

        var tiffData = tiffHeader.Concat(ifd0).Concat(subIfd).Concat(dateString).ToArray();
        var app1Data = exifHeader.Concat(tiffData).ToArray();
        var app1Length = (ushort)(app1Data.Length + 2); // +2 for length field itself
        var app1Segment = new byte[]
        {
            0xFF, 0xE1,
            (byte)(app1Length >> 8), (byte)(app1Length & 0xFF)
        }.Concat(app1Data).ToArray();

        // JPEG EOI
        var eoi = new byte[] { 0xFF, 0xD9 };

        return soi.Concat(app1Segment).Concat(eoi).ToArray();
    }

    /// <summary>Minimal valid JPEG with no EXIF data.</summary>
    private static byte[] MinimalJpegNoExif() =>
    [
        0xFF, 0xD8, // SOI
        0xFF, 0xE0, 0x00, 0x10, // APP0 marker + length (16 bytes)
        0x4A, 0x46, 0x49, 0x46, 0x00, // "JFIF\0"
        0x01, 0x01, // version
        0x00,       // aspect ratio units
        0x00, 0x01, // X density
        0x00, 0x01, // Y density
        0x00, 0x00, // thumbnail
        0xFF, 0xD9  // EOI
    ];
}

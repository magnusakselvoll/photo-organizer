using System.Text.Json;
using PhotoOrganizer.Crawler.Sidecars;
using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.Crawler.Tests;

[TestClass]
public class SidecarStoreTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Initialize() =>
        _tempDir = Directory.CreateTempSubdirectory("photo-organizer-tests-").FullName;

    [TestCleanup]
    public void Cleanup() =>
        Directory.Delete(_tempDir, recursive: true);

    [TestMethod]
    public async Task FolderSidecar_RoundTrip()
    {
        var store = new JsonSidecarStore();
        var original = new FolderSidecar { Version = 1, Label = "Test Folder", Type = "originals", Enabled = true };
        await store.WriteFolderSidecarAsync(_tempDir, original);

        var loaded = await store.ReadFolderSidecarAsync(_tempDir);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(original.Label, loaded.Label);
        Assert.AreEqual(original.Type, loaded.Type);
        Assert.AreEqual(original.Enabled, loaded.Enabled);
        Assert.AreEqual(original.Version, loaded.Version);
    }

    [TestMethod]
    public async Task PhotoMetaSidecar_RoundTrip()
    {
        var store = new JsonSidecarStore();
        var photoPath = Path.Combine(_tempDir, "photo.jpg");
        var now = DateTimeOffset.Parse("2024-06-15T10:30:00Z");
        var original = new PhotoMetaSidecar
        {
            Version = 1,
            CapturedAt = now,
            Tags = ["beach", "summer"],
            CrawlSteps = { ["metadata"] = new CrawlStepRecord { Version = 1, CompletedAt = now } }
        };

        await store.WritePhotoMetaAsync(photoPath, original);
        var loaded = await store.ReadPhotoMetaAsync(photoPath);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(original.CapturedAt, loaded.CapturedAt);
        CollectionAssert.AreEqual(original.Tags, loaded.Tags);
        Assert.IsTrue(loaded.CrawlSteps.ContainsKey("metadata"));
        Assert.AreEqual(1, loaded.CrawlSteps["metadata"].Version);
    }

    [TestMethod]
    public async Task PhotoMetaSidecar_PreservesUnknownFields()
    {
        // Write a sidecar JSON with an extra unknown field (simulating future schema addition)
        var photoPath = Path.Combine(_tempDir, "photo.jpg");
        var sidecarPath = Path.Combine(_tempDir, "photo.jpg.meta.json");
        var json = """{"version":1,"capturedAt":null,"unknownFutureField":"someValue"}""";
        await File.WriteAllTextAsync(sidecarPath, json);

        var store = new JsonSidecarStore();
        var loaded = await store.ReadPhotoMetaAsync(photoPath);
        Assert.IsNotNull(loaded);

        // Re-write and verify the unknown field is preserved (path still photo.jpg.meta.json)
        await store.WritePhotoMetaAsync(photoPath, loaded);
        var rewritten = await File.ReadAllTextAsync(sidecarPath);
        Assert.IsTrue(rewritten.Contains("unknownFutureField"), "Unknown fields must be preserved on round-trip");
    }

    [TestMethod]
    public async Task ReadPhotoMeta_ReturnsNull_WhenNoSidecarFile()
    {
        var store = new JsonSidecarStore();
        var result = await store.ReadPhotoMetaAsync(Path.Combine(_tempDir, "missing.jpg"));
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ReadFolderSidecar_ReturnsNull_WhenNoSidecarFile()
    {
        var store = new JsonSidecarStore();
        var result = await store.ReadFolderSidecarAsync(_tempDir);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task SidecarPath_DerivedCorrectly()
    {
        var store = new JsonSidecarStore();
        var photoPath = Path.Combine(_tempDir, "IMG_1234.JPG");
        // Sidecar keeps the original extension: IMG_1234.JPG.meta.json
        var expectedSidecarPath = Path.Combine(_tempDir, "IMG_1234.JPG.meta.json");

        await store.WritePhotoMetaAsync(photoPath, new PhotoMetaSidecar());
        Assert.IsTrue(File.Exists(expectedSidecarPath));
    }

    [TestMethod]
    public async Task WritePhotoMeta_LeavesNoTempFiles_AfterSuccessfulWrite()
    {
        var store = new JsonSidecarStore();
        var photoPath = Path.Combine(_tempDir, "photo.jpg");

        await store.WritePhotoMetaAsync(photoPath, new PhotoMetaSidecar { Version = 1 });

        var files = Directory.GetFiles(_tempDir);
        Assert.AreEqual(1, files.Length, "Only the sidecar file should remain — no temp files");
        Assert.IsTrue(files[0].EndsWith(".meta.json"), "The only file should be the sidecar");
    }

    [TestMethod]
    public async Task WriteFolderSidecar_LeavesNoTempFiles_AfterSuccessfulWrite()
    {
        var store = new JsonSidecarStore();

        await store.WriteFolderSidecarAsync(_tempDir, new FolderSidecar { Version = 1, Type = "originals" });

        var files = Directory.GetFiles(_tempDir);
        Assert.AreEqual(1, files.Length, "Only the sidecar file should remain — no temp files");
        Assert.AreEqual("_folder.json", Path.GetFileName(files[0]));
    }

    [TestMethod]
    public async Task WritePhotoMeta_OriginalIntact_WhenAbandonedTempFileExists()
    {
        // Simulates a prior interrupted write: a temp file was left behind in the directory
        // but the original sidecar is still intact. Reads must ignore the abandoned temp file.
        var store = new JsonSidecarStore();
        var photoPath = Path.Combine(_tempDir, "photo.jpg");
        var original = new PhotoMetaSidecar { Version = 1, Tags = ["original"] };
        await store.WritePhotoMetaAsync(photoPath, original);

        // Plant an abandoned temp file (partial/invalid JSON) as if a previous write was killed mid-stream
        await File.WriteAllTextAsync(Path.Combine(_tempDir, Path.GetRandomFileName()), "partial");

        var loaded = await store.ReadPhotoMetaAsync(photoPath);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(1, loaded.Version);
        CollectionAssert.AreEqual(new[] { "original" }, loaded.Tags);
    }

    [TestMethod]
    public async Task WritePhotoMeta_LeavesNoTempFiles_WhenWriteFails()
    {
        var store = new JsonSidecarStore();
        // Point to a path whose parent directory does not exist — File.Create will throw
        var photoPath = Path.Combine(_tempDir, "nonexistent-subdir", "photo.jpg");

        try
        {
            await store.WritePhotoMetaAsync(photoPath, new PhotoMetaSidecar());
            Assert.Fail("Expected an exception for nonexistent directory");
        }
        catch (DirectoryNotFoundException) { }

        Assert.AreEqual(0, Directory.GetFiles(_tempDir).Length, "No temp files should remain after a failed write");
    }
}

using PhotoOrganizer.Crawler.Discovery;

namespace PhotoOrganizer.Crawler.Tests;

[TestClass]
public class FileDiscovererTests
{
    private string _tempDir = null!;
    private FileDiscoverer _discoverer = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Directory.CreateTempSubdirectory("photo-organizer-tests-").FullName;
        _discoverer = new FileDiscoverer();
    }

    [TestCleanup]
    public void Cleanup() =>
        Directory.Delete(_tempDir, recursive: true);

    [TestMethod]
    public void DiscoversSupportedExtensions()
    {
        var supported = new[] { ".jpg", ".jpeg", ".png", ".heic", ".cr2", ".cr3", ".orf", ".arw", ".nef", ".rw2", ".tiff", ".tif" };
        foreach (var ext in supported)
            File.WriteAllText(Path.Combine(_tempDir, $"photo{ext}"), "");

        var discovered = _discoverer.Discover(_tempDir);
        Assert.AreEqual(supported.Length, discovered.Count);
    }

    [TestMethod]
    public void SkipsUnsupportedExtensions()
    {
        File.WriteAllText(Path.Combine(_tempDir, "document.pdf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "video.mp4"), "");
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "");

        var discovered = _discoverer.Discover(_tempDir);
        Assert.AreEqual(1, discovered.Count);
        Assert.AreEqual("photo.jpg", Path.GetFileName(discovered[0].FilePath));
    }

    [TestMethod]
    public void SkipsSidecarFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "");
        File.WriteAllText(Path.Combine(_tempDir, "photo.meta.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "_folder.json"), "{}");

        var discovered = _discoverer.Discover(_tempDir);
        Assert.AreEqual(1, discovered.Count);
    }

    [TestMethod]
    public void DiscoverRecursively()
    {
        var subDir = Directory.CreateDirectory(Path.Combine(_tempDir, "2024", "June")).FullName;
        File.WriteAllText(Path.Combine(_tempDir, "top.jpg"), "");
        File.WriteAllText(Path.Combine(subDir, "nested.jpg"), "");

        var discovered = _discoverer.Discover(_tempDir);
        Assert.AreEqual(2, discovered.Count);
    }

    [TestMethod]
    public void ExtensionMatchingIsCaseInsensitive()
    {
        File.WriteAllText(Path.Combine(_tempDir, "PHOTO.JPG"), "");
        File.WriteAllText(Path.Combine(_tempDir, "photo.JPEG"), "");

        var discovered = _discoverer.Discover(_tempDir);
        Assert.AreEqual(2, discovered.Count);
    }

    [TestMethod]
    public void EmptyFolder_ReturnsEmpty()
    {
        var discovered = _discoverer.Discover(_tempDir);
        Assert.AreEqual(0, discovered.Count);
    }
}

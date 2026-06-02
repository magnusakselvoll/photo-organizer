namespace PhotoOrganizer.Crawler.Tests;

[TestClass]
public class MetaSidecarCleanerTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Initialize() =>
        _tempDir = Directory.CreateTempSubdirectory("MetaSidecarCleanerTests_").FullName;

    [TestCleanup]
    public void Cleanup() =>
        Directory.Delete(_tempDir, recursive: true);

    [TestMethod]
    public void DeletesMetaJsonFiles_LeavesOtherFilesIntact()
    {
        var photo = Path.Combine(_tempDir, "photo.jpg");
        var meta  = Path.Combine(_tempDir, "photo.jpg.meta.json");
        var folder = Path.Combine(_tempDir, "_folder.json");

        File.WriteAllText(photo, "");
        File.WriteAllText(meta, "{}");
        File.WriteAllText(folder, "{}");

        MetaSidecarCleaner.DeleteAll([_tempDir]);

        Assert.IsTrue(File.Exists(photo),  "Photo file must not be deleted");
        Assert.IsTrue(File.Exists(folder), "_folder.json must not be deleted");
        Assert.IsFalse(File.Exists(meta),  ".meta.json sidecar must be deleted");
    }

    [TestMethod]
    public void DeletesMetaJsonFilesRecursively()
    {
        var subDir = Directory.CreateDirectory(Path.Combine(_tempDir, "sub")).FullName;
        var metaTop = Path.Combine(_tempDir, "photo.jpg.meta.json");
        var metaSub = Path.Combine(subDir, "other.jpg.meta.json");

        File.WriteAllText(metaTop, "{}");
        File.WriteAllText(metaSub, "{}");

        MetaSidecarCleaner.DeleteAll([_tempDir]);

        Assert.IsFalse(File.Exists(metaTop));
        Assert.IsFalse(File.Exists(metaSub));
    }

    [TestMethod]
    public void EmptyRoot_DoesNotThrow()
    {
        // Should complete without error even when there are no .meta.json files.
        MetaSidecarCleaner.DeleteAll([_tempDir]);
    }

    [TestMethod]
    public void NonExistentRoot_DoesNotThrow()
    {
        var missing = Path.Combine(_tempDir, "does-not-exist");
        MetaSidecarCleaner.DeleteAll([missing]);
    }

    [TestMethod]
    public void DeletesFromMultipleRoots()
    {
        var root1 = Directory.CreateDirectory(Path.Combine(_tempDir, "root1")).FullName;
        var root2 = Directory.CreateDirectory(Path.Combine(_tempDir, "root2")).FullName;
        var meta1 = Path.Combine(root1, "a.jpg.meta.json");
        var meta2 = Path.Combine(root2, "b.jpg.meta.json");

        File.WriteAllText(meta1, "{}");
        File.WriteAllText(meta2, "{}");

        MetaSidecarCleaner.DeleteAll([root1, root2]);

        Assert.IsFalse(File.Exists(meta1));
        Assert.IsFalse(File.Exists(meta2));
    }
}

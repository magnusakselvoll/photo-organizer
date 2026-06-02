using PhotoOrganizer.Crawler.Discovery;

namespace PhotoOrganizer.Crawler.Tests;

[TestClass]
public sealed class ResilientFileWalkerTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Directory.CreateTempSubdirectory("CrawlerWalkerTests_").FullName;
    }

    [TestCleanup]
    public void Cleanup() =>
        Directory.Delete(_tempDir, recursive: true);

    [TestMethod]
    public void EnumerateFiles_ReturnsFilesFromRootAndSubdirectories()
    {
        var sub = Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        var nested = Directory.CreateDirectory(Path.Combine(sub.FullName, "nested"));
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "");
        File.WriteAllText(Path.Combine(sub.FullName, "b.txt"), "");
        File.WriteAllText(Path.Combine(nested.FullName, "c.txt"), "");

        var results = ResilientFileWalker.EnumerateFiles(_tempDir, "*").ToList();

        Assert.AreEqual(3, results.Count);
        CollectionAssert.Contains(results, Path.Combine(_tempDir, "a.txt"));
        CollectionAssert.Contains(results, Path.Combine(sub.FullName, "b.txt"));
        CollectionAssert.Contains(results, Path.Combine(nested.FullName, "c.txt"));
    }

    [TestMethod]
    public void EnumerateFiles_SearchPatternFiltersResults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "_folder.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "");

        var results = ResilientFileWalker.EnumerateFiles(_tempDir, "_folder.json").ToList();

        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].EndsWith("_folder.json", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void EnumerateFiles_ResultsAreInDeterministicOrder()
    {
        var sub1 = Directory.CreateDirectory(Path.Combine(_tempDir, "a"));
        var sub2 = Directory.CreateDirectory(Path.Combine(_tempDir, "b"));
        File.WriteAllText(Path.Combine(sub2.FullName, "file2.txt"), "");
        File.WriteAllText(Path.Combine(sub1.FullName, "file1.txt"), "");
        File.WriteAllText(Path.Combine(_tempDir, "file0.txt"), "");

        var results = ResilientFileWalker.EnumerateFiles(_tempDir, "*").ToList();

        // Expect ordinal sort: root file first, then sub/a, then sub/b
        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results[0].EndsWith("file0.txt", StringComparison.Ordinal));
        Assert.IsTrue(results[1].EndsWith("file1.txt", StringComparison.Ordinal));
        Assert.IsTrue(results[2].EndsWith("file2.txt", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EnumerateFiles_EmptyRoot_ReturnsEmpty()
    {
        var results = ResilientFileWalker.EnumerateFiles(_tempDir, "*").ToList();
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void EnumerateFiles_SkipsReparsePointSubdirectory_AndStillFindsOtherFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "valid.txt"), "");

        // A broken directory symlink — a reparse point whose target doesn't exist
        var brokenLink = Path.Combine(_tempDir, "broken-link");
        try
        {
            Directory.CreateSymbolicLink(brokenLink, Path.Combine(_tempDir, "does-not-exist"));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Inconclusive($"Symlink creation unsupported on this host: {ex.Message}");
            return;
        }

        // Must not throw; must still find the valid file
        var results = ResilientFileWalker.EnumerateFiles(_tempDir, "*").ToList();

        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].EndsWith("valid.txt", StringComparison.OrdinalIgnoreCase));
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using PhotoOrganizer.Infrastructure.Storage;

namespace PhotoOrganizer.Infrastructure.Tests;

[TestClass]
public sealed class ResilientFileWalkerTests
{
    private DirectoryInfo _tempDir = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Directory.CreateTempSubdirectory("ResilientWalkerTests_");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _tempDir.Delete(recursive: true);
    }

    [TestMethod]
    public void EnumerateFiles_ReturnsFilesFromRootAndSubdirectories()
    {
        var sub = _tempDir.CreateSubdirectory("sub");
        var nested = sub.CreateSubdirectory("nested");
        File.WriteAllText(Path.Combine(_tempDir.FullName, "a.txt"), "");
        File.WriteAllText(Path.Combine(sub.FullName, "b.txt"), "");
        File.WriteAllText(Path.Combine(nested.FullName, "c.txt"), "");

        var results = ResilientFileWalker.EnumerateFiles(_tempDir.FullName, "*", NullLogger.Instance).ToList();

        Assert.AreEqual(3, results.Count);
        CollectionAssert.Contains(results, Path.Combine(_tempDir.FullName, "a.txt"));
        CollectionAssert.Contains(results, Path.Combine(sub.FullName, "b.txt"));
        CollectionAssert.Contains(results, Path.Combine(nested.FullName, "c.txt"));
    }

    [TestMethod]
    public void EnumerateFiles_SearchPatternFiltersResults()
    {
        File.WriteAllText(Path.Combine(_tempDir.FullName, "_folder.json"), "{}");
        File.WriteAllText(Path.Combine(_tempDir.FullName, "photo.jpg"), "");

        var results = ResilientFileWalker.EnumerateFiles(_tempDir.FullName, "_folder.json", NullLogger.Instance).ToList();

        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].EndsWith("_folder.json", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void EnumerateFiles_ResultsAreInDeterministicOrder()
    {
        // Create files that would have different orderings on different OS directory walks
        var sub1 = _tempDir.CreateSubdirectory("a");
        var sub2 = _tempDir.CreateSubdirectory("b");
        File.WriteAllText(Path.Combine(sub2.FullName, "file2.txt"), "");
        File.WriteAllText(Path.Combine(sub1.FullName, "file1.txt"), "");
        File.WriteAllText(Path.Combine(_tempDir.FullName, "file0.txt"), "");

        var results = ResilientFileWalker.EnumerateFiles(_tempDir.FullName, "*", NullLogger.Instance).ToList();

        // Expect ordinal sort: root file first, then sub/a, then sub/b
        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results[0].EndsWith("file0.txt", StringComparison.Ordinal));
        Assert.IsTrue(results[1].EndsWith("file1.txt", StringComparison.Ordinal));
        Assert.IsTrue(results[2].EndsWith("file2.txt", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EnumerateFiles_EmptyRoot_ReturnsEmpty()
    {
        var results = ResilientFileWalker.EnumerateFiles(_tempDir.FullName, "*", NullLogger.Instance).ToList();
        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void EnumerateFiles_SkipsReparsePointSubdirectory_AndStillFindsOtherFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir.FullName, "valid.txt"), "");

        // A broken directory symlink — a reparse point whose target doesn't exist
        var brokenLink = Path.Combine(_tempDir.FullName, "broken-link");
        try
        {
            Directory.CreateSymbolicLink(brokenLink, Path.Combine(_tempDir.FullName, "does-not-exist"));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Inconclusive($"Symlink creation unsupported on this host: {ex.Message}");
            return;
        }

        // Must not throw; must still find the valid file
        var results = ResilientFileWalker.EnumerateFiles(_tempDir.FullName, "*", NullLogger.Instance).ToList();

        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(results[0].EndsWith("valid.txt", StringComparison.OrdinalIgnoreCase));
    }
}

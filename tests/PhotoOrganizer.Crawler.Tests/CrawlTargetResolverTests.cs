using PhotoOrganizer.Crawler.Discovery;
using PhotoOrganizer.Crawler.Sidecars;
using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.Crawler.Tests;

/// <summary>
/// Unit tests for <see cref="CrawlTargetResolver"/>. All tests use a real temp directory tree
/// with real <see cref="FileDiscoverer"/> and <see cref="JsonSidecarStore"/> to keep fakes out
/// of this layer — the I/O is cheap and deterministic.
/// </summary>
[TestClass]
public class CrawlTargetResolverTests
{
    private string _root = null!;
    private CrawlTargetResolver _resolver = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Directory.CreateTempSubdirectory("crawl-resolver-tests-").FullName;
        _resolver = new CrawlTargetResolver(new JsonSidecarStore(), new FileDiscoverer());
    }

    [TestCleanup]
    public void Cleanup() =>
        Directory.Delete(_root, recursive: true);

    // ── helpers ────────────────────────────────────────────────────────────────

    private static void WriteFolderJson(string dir, string type = "mixed", bool enabled = true, string label = "Test")
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "_folder.json"), $$"""
            {"version":1,"label":"{{label}}","type":"{{type}}","enabled":{{(enabled ? "true" : "false")}}}
            """);
    }

    private static void WritePhoto(string dir, string name = "photo.jpg")
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), "");
    }

    // ── tests ──────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SiblingUnits_EachOwnTheirFiles()
    {
        var originals = Path.Combine(_root, "originals");
        var edits = Path.Combine(_root, "edits");
        WriteFolderJson(originals, "originals");
        WriteFolderJson(edits, "edits");
        WritePhoto(originals, "IMG_001.jpg");
        WritePhoto(edits, "IMG_001_edit.jpg");

        var targets = await _resolver.ResolveAsync([_root]);

        Assert.AreEqual(2, targets.Count, "Expected two crawl targets");

        var orig = targets.Single(t => t.FolderPath.EndsWith("originals", StringComparison.OrdinalIgnoreCase));
        var edit = targets.Single(t => t.FolderPath.EndsWith("edits", StringComparison.OrdinalIgnoreCase));

        Assert.AreEqual(1, orig.Files.Count, "originals unit should own one file");
        Assert.AreEqual(1, edit.Files.Count, "edits unit should own one file");
        Assert.AreEqual("originals", orig.Sidecar.Type);
        Assert.AreEqual("edits", edit.Sidecar.Type);
    }

    [TestMethod]
    public async Task PhotoInNoSidecarSubfolder_OwnedByNearestAncestorUnit()
    {
        WriteFolderJson(_root, "originals");
        var sub = Path.Combine(_root, "2024", "June");
        WritePhoto(sub, "nested.jpg");

        var targets = await _resolver.ResolveAsync([_root]);

        Assert.AreEqual(1, targets.Count);
        Assert.AreEqual(_root, targets[0].FolderPath, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(1, targets[0].Files.Count, "nested photo should be owned by ancestor unit");
    }

    [TestMethod]
    public async Task DisabledUnit_ReturnedWithNoFiles_FlaggedDisabled()
    {
        WriteFolderJson(_root, "originals", enabled: false);
        WritePhoto(_root, "photo.jpg");

        var targets = await _resolver.ResolveAsync([_root]);

        Assert.AreEqual(1, targets.Count);
        Assert.IsFalse(targets[0].Sidecar.Enabled, "Disabled target should have Enabled=false");
        // Files are still resolved — the orchestrator decides to skip them based on Enabled
        Assert.AreEqual(1, targets[0].Files.Count);
    }

    [TestMethod]
    public async Task NestedUnitFolder_NotDoubleCountedByParent()
    {
        // Parent unit at root, child unit in subfolder — child's photo must belong only to child
        WriteFolderJson(_root, "originals");
        var sub = Path.Combine(_root, "edits");
        WriteFolderJson(sub, "edits");
        WritePhoto(_root, "a.jpg");
        WritePhoto(sub, "a_edit.jpg");

        var targets = await _resolver.ResolveAsync([_root]);

        Assert.AreEqual(2, targets.Count, "Both parent and child unit should be discovered");

        var parent = targets.Single(t => string.Equals(t.FolderPath, _root, StringComparison.OrdinalIgnoreCase));
        var child = targets.Single(t => t.FolderPath.EndsWith("edits", StringComparison.OrdinalIgnoreCase));

        Assert.AreEqual(1, parent.Files.Count, "Parent should own only its own photo");
        Assert.AreEqual(1, child.Files.Count, "Child should own only its own photo");

        var parentFile = parent.Files.Single();
        var childFile = child.Files.Single();
        Assert.IsTrue(parentFile.FilePath.EndsWith("a.jpg", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(childFile.FilePath.EndsWith("a_edit.jpg", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task ScanRootWithNoFolderJson_ReturnsNoTargets()
    {
        WritePhoto(_root, "photo.jpg"); // photo exists but no _folder.json anywhere

        var targets = await _resolver.ResolveAsync([_root]);

        Assert.AreEqual(0, targets.Count, "No units should be discovered when no _folder.json exists");
    }

    [TestMethod]
    public async Task NonExistentScanRoot_ReturnsNoTargets()
    {
        var targets = await _resolver.ResolveAsync([Path.Combine(_root, "does-not-exist")]);

        Assert.AreEqual(0, targets.Count);
    }

    [TestMethod]
    public async Task PhotoNotUnderAnyUnit_IsSkipped()
    {
        // A scan root with a unit in a subfolder — a photo directly at the root (outside the unit) should be skipped
        var sub = Path.Combine(_root, "managed");
        WriteFolderJson(sub, "originals");
        WritePhoto(sub, "managed.jpg");
        WritePhoto(_root, "unmanaged.jpg"); // not beneath any unit

        var targets = await _resolver.ResolveAsync([_root]);

        Assert.AreEqual(1, targets.Count);
        Assert.AreEqual(1, targets[0].Files.Count, "Only managed.jpg should be owned");
        Assert.IsTrue(targets[0].Files[0].FilePath.EndsWith("managed.jpg", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task MultipleScanRoots_DiscoveredIndependently()
    {
        var root2 = Directory.CreateTempSubdirectory("crawl-resolver-tests-2-").FullName;
        try
        {
            WriteFolderJson(_root, "originals", label: "Root1");
            WritePhoto(_root, "a.jpg");
            WriteFolderJson(root2, "edits", label: "Root2");
            WritePhoto(root2, "b.jpg");

            var targets = await _resolver.ResolveAsync([_root, root2]);

            Assert.AreEqual(2, targets.Count);
            Assert.IsTrue(targets.Any(t => t.Sidecar.Label == "Root1"));
            Assert.IsTrue(targets.Any(t => t.Sidecar.Label == "Root2"));
        }
        finally
        {
            Directory.Delete(root2, recursive: true);
        }
    }

    [TestMethod]
    public async Task ResolveAsync_SkipsReparsePointSubdirectory_AndStillFindsTargets()
    {
        WriteFolderJson(_root, "originals");
        WritePhoto(_root, "photo.jpg");

        // A broken directory symlink as a sibling subfolder — a reparse point whose target doesn't exist
        var brokenLink = Path.Combine(_root, "broken-link");
        try
        {
            Directory.CreateSymbolicLink(brokenLink, Path.Combine(_root, "does-not-exist"));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Inconclusive($"Symlink creation unsupported on this host: {ex.Message}");
            return;
        }

        // Must not throw; must still resolve the valid target
        var targets = await _resolver.ResolveAsync([_root]);

        Assert.AreEqual(1, targets.Count);
        Assert.AreEqual(1, targets[0].Files.Count);
    }
}

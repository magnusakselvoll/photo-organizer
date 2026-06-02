using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhotoOrganizer.Application;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Models;
using PhotoOrganizer.Infrastructure.Indexing;
using PhotoOrganizer.Infrastructure.Sidecars;

namespace PhotoOrganizer.Infrastructure.Tests.Indexing;

/// <summary>
/// Integration-style unit tests for RandomizedSidecarIndexer that build a real
/// directory tree in temp storage and assert the index is correctly populated.
/// </summary>
[TestClass]
public sealed class RandomizedSidecarIndexerTests
{
    private DirectoryInfo _tempDir = null!;
    private static readonly JsonSerializerOptions SidecarJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [TestInitialize]
    public void Initialize() =>
        _tempDir = Directory.CreateTempSubdirectory("RandomizedIndexerTests_");

    [TestCleanup]
    public void Cleanup() =>
        _tempDir.Delete(recursive: true);

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void CreateFolderJson(string dir, string type = "originals", bool enabled = true, string label = "")
    {
        Directory.CreateDirectory(dir);
        var sidecar = new { version = 1, label = string.IsNullOrEmpty(label) ? Path.GetFileName(dir) : label, type, enabled };
        File.WriteAllText(Path.Combine(dir, "_folder.json"),
            JsonSerializer.Serialize(sidecar, SidecarJson));
    }

    private static string CreateFakeJpeg(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, [0xFF, 0xD8, 0xFF, 0xE0]); // minimal JPEG magic bytes
        return path;
    }

    private static void CreatePhotoMeta(string photoPath, DateTimeOffset? capturedAt = null, bool isPreferred = false)
    {
        var sidecar = new PhotoMetaSidecar { CapturedAt = capturedAt, IsPreferred = isPreferred };
        var metaPath = Path.Combine(
            Path.GetDirectoryName(photoPath)!,
            Path.GetFileNameWithoutExtension(photoPath) + ".meta.json");
        File.WriteAllText(metaPath, JsonSerializer.Serialize(sidecar, SidecarJson));
    }

    private async Task<PhotoIndex> RunIndexerAsync(string scanRoot, int parallelism = 2,
        CancellationToken cancellationToken = default)
    {
        var settings = new PhotoOrganizerSettings
        {
            ScanRoots = [scanRoot],
            Indexing = new IndexingSettings
            {
                MaxParallelism = parallelism,
                CacheDirectory = " ", // disable cache in tests
                CacheWriteIntervalPhotos = 0
            }
        };

        var index = new PhotoIndex();
        var cache = new PhotoIndexCache(Options.Create(settings), NullLogger<PhotoIndexCache>.Instance);
        var indexer = new RandomizedSidecarIndexer(
            Options.Create(settings),
            new SidecarReader(),
            index,
            cache,
            NullLogger<RandomizedSidecarIndexer>.Instance);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));
        await indexer.StartAsync(cts.Token);

        // Wait for completion
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!index.IsComplete && DateTime.UtcNow < deadline)
            await Task.Delay(20, cts.Token);

        Assert.IsTrue(index.IsComplete, "Index did not complete within 10s");
        return index;
    }

    // ─── Basic indexing ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task IndexesAllPhotosInSingleFolder()
    {
        var folder = Path.Combine(_tempDir.FullName, "originals");
        CreateFolderJson(folder);
        CreateFakeJpeg(folder, "img001.jpg");
        CreateFakeJpeg(folder, "img002.jpg");
        CreateFakeJpeg(folder, "img003.jpg");

        var index = await RunIndexerAsync(_tempDir.FullName);

        Assert.AreEqual(3, index.Count, "Expected 3 photos");
    }

    [TestMethod]
    public async Task ReadsMetaJsonSidecars()
    {
        var folder = Path.Combine(_tempDir.FullName, "originals");
        CreateFolderJson(folder);
        var photoPath = CreateFakeJpeg(folder, "img001.jpg");
        var captured = new DateTimeOffset(2024, 7, 4, 10, 0, 0, TimeSpan.Zero);
        CreatePhotoMeta(photoPath, capturedAt: captured, isPreferred: true);

        var index = await RunIndexerAsync(_tempDir.FullName);

        var photo = index.SnapshotPhotos().Single();
        Assert.AreEqual(captured, photo.CapturedAt);
        Assert.IsTrue(photo.IsPreferred);
    }

    [TestMethod]
    public async Task RegistersDiscoveredFolders()
    {
        var folder = Path.Combine(_tempDir.FullName, "originals");
        CreateFolderJson(folder, type: "originals", label: "My Originals");

        var index = await RunIndexerAsync(_tempDir.FullName);

        var folders = index.SnapshotFolders();
        Assert.AreEqual(1, folders.Count);
        Assert.AreEqual(FolderType.Originals, folders[0].Type);
        Assert.AreEqual("My Originals", folders[0].Label);
    }

    // ─── Nested folders ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task IndexesPhotosInNestedSubfolders()
    {
        var root = Path.Combine(_tempDir.FullName, "library");
        CreateFolderJson(root);
        var sub = Path.Combine(root, "2024", "holidays");
        Directory.CreateDirectory(sub);
        CreateFakeJpeg(root, "top.jpg");
        CreateFakeJpeg(sub, "deep.jpg");

        var index = await RunIndexerAsync(_tempDir.FullName);

        Assert.AreEqual(2, index.Count, "Should index photos at all depths");
    }

    [TestMethod]
    public async Task NestedFolderJson_OverridesFolderType()
    {
        // Root folder = originals; nested subfolder with its own _folder.json = edits
        var originals = Path.Combine(_tempDir.FullName, "originals");
        var edits = Path.Combine(originals, "edits");
        CreateFolderJson(originals, type: "originals");
        CreateFolderJson(edits, type: "edits");
        CreateFakeJpeg(originals, "orig.jpg");
        CreateFakeJpeg(edits, "edit.jpg");

        var index = await RunIndexerAsync(_tempDir.FullName);

        Assert.AreEqual(2, index.Count);
        var origPhoto = index.SnapshotPhotos().Single(p => p.FileName == "orig");
        var editPhoto = index.SnapshotPhotos().Single(p => p.FileName == "edit");
        Assert.AreEqual(FolderType.Originals, origPhoto.FolderType);
        Assert.AreEqual(FolderType.Edits, editPhoto.FolderType);
    }

    // ─── Disabled folders ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task DisabledFolder_PhotosNotIndexed()
    {
        var enabled = Path.Combine(_tempDir.FullName, "enabled");
        var disabled = Path.Combine(_tempDir.FullName, "disabled");
        CreateFolderJson(enabled, enabled: true);
        CreateFolderJson(disabled, enabled: false);
        CreateFakeJpeg(enabled, "good.jpg");
        CreateFakeJpeg(disabled, "bad.jpg");

        var index = await RunIndexerAsync(_tempDir.FullName);

        Assert.AreEqual(1, index.Count);
        Assert.AreEqual("good", index.SnapshotPhotos()[0].FileName);
    }

    [TestMethod]
    public async Task DisabledFolder_SubtreePruned()
    {
        // disabled/ contains a nested subfolder with photos — all should be skipped
        var disabled = Path.Combine(_tempDir.FullName, "disabled");
        var sub = Path.Combine(disabled, "deep");
        CreateFolderJson(disabled, enabled: false);
        Directory.CreateDirectory(sub);
        CreateFakeJpeg(sub, "deep.jpg");

        var index = await RunIndexerAsync(_tempDir.FullName);

        Assert.AreEqual(0, index.Count);
    }

    // ─── Photos not under any crawl unit ─────────────────────────────────────

    [TestMethod]
    public async Task PhotosOutsideCrawlUnit_NotIndexed()
    {
        // A photo sitting in a directory with no _folder.json ancestor
        CreateFakeJpeg(_tempDir.FullName, "orphan.jpg");

        var index = await RunIndexerAsync(_tempDir.FullName);

        Assert.AreEqual(0, index.Count);
    }

    // ─── Deterministic ID ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task PhotoId_IsStableAndMatchesDeterministicGuid()
    {
        var folder = Path.Combine(_tempDir.FullName, "originals");
        CreateFolderJson(folder);
        var path = CreateFakeJpeg(folder, "img001.jpg");

        var index = await RunIndexerAsync(_tempDir.FullName);

        var photo = index.SnapshotPhotos().Single();
        Assert.AreEqual(PhotoId.FromFilePath(path), photo.Id);
    }

    // ─── FileModifiedAt ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task FileModifiedAt_IsPopulated()
    {
        var folder = Path.Combine(_tempDir.FullName, "originals");
        CreateFolderJson(folder);
        CreateFakeJpeg(folder, "img001.jpg");

        var index = await RunIndexerAsync(_tempDir.FullName);

        var photo = index.SnapshotPhotos().Single();
        Assert.IsNotNull(photo.FileModifiedAt, "FileModifiedAt should be populated from filesystem");
    }

    // ─── Non-photo files ignored ──────────────────────────────────────────────

    [TestMethod]
    public async Task NonPhotoFiles_AreIgnored()
    {
        var folder = Path.Combine(_tempDir.FullName, "originals");
        CreateFolderJson(folder);
        CreateFakeJpeg(folder, "img001.jpg");
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "ignore me");
        File.WriteAllText(Path.Combine(folder, "img001.meta.json"), "{}");

        var index = await RunIndexerAsync(_tempDir.FullName);

        Assert.AreEqual(1, index.Count, "Only .jpg should be indexed");
    }

    // ─── Empty library ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task EmptyLibrary_CompletesWithZeroPhotos()
    {
        // No _folder.json, no photos
        var index = await RunIndexerAsync(_tempDir.FullName);

        Assert.IsTrue(index.IsComplete);
        Assert.AreEqual(0, index.Count);
    }

    // ─── Multiple scan roots ──────────────────────────────────────────────────

    [TestMethod]
    public async Task MultipleRoots_AllPhotosIndexed()
    {
        var rootA = Path.Combine(_tempDir.FullName, "A");
        var rootB = Path.Combine(_tempDir.FullName, "B");
        CreateFolderJson(rootA);
        CreateFolderJson(rootB);
        CreateFakeJpeg(rootA, "a1.jpg");
        CreateFakeJpeg(rootA, "a2.jpg");
        CreateFakeJpeg(rootB, "b1.jpg");

        var settings = new PhotoOrganizerSettings
        {
            ScanRoots = [rootA, rootB],
            Indexing = new IndexingSettings { MaxParallelism = 2, CacheDirectory = " ", CacheWriteIntervalPhotos = 0 }
        };
        var index = new PhotoIndex();
        var cache = new PhotoIndexCache(Options.Create(settings), NullLogger<PhotoIndexCache>.Instance);
        var indexer = new RandomizedSidecarIndexer(
            Options.Create(settings),
            new SidecarReader(),
            index,
            cache,
            NullLogger<RandomizedSidecarIndexer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await indexer.StartAsync(cts.Token);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!index.IsComplete && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert.IsTrue(index.IsComplete);

        Assert.AreEqual(3, index.Count);
    }
}

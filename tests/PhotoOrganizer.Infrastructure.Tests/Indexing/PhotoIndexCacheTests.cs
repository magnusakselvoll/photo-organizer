using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhotoOrganizer.Application;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Infrastructure.Indexing;

namespace PhotoOrganizer.Infrastructure.Tests.Indexing;

[TestClass]
public sealed class PhotoIndexCacheTests
{
    private DirectoryInfo _tempDir = null!;

    [TestInitialize]
    public void Initialize() =>
        _tempDir = Directory.CreateTempSubdirectory("PhotoIndexCacheTests_");

    [TestCleanup]
    public void Cleanup() =>
        _tempDir.Delete(recursive: true);

    private PhotoIndexCache CreateCache(string[]? scanRoots = null, int ttlHours = 72) =>
        new(Options.Create(new PhotoOrganizerSettings
        {
            ScanRoots = scanRoots ?? [@"C:\photos"],
            Indexing = new IndexingSettings
            {
                CacheDirectory = _tempDir.FullName,
                CacheTtlHours = ttlHours
            }
        }), NullLogger<PhotoIndexCache>.Instance);

    private static PhotoIndex PopulatedIndex()
    {
        var index = new PhotoIndex();
        index.AddPhoto(new Photo
        {
            Id = Guid.NewGuid(),
            FilePath = @"C:\photos\originals\img001.jpg",
            FileName = "img001",
            FolderType = FolderType.Originals,
            CapturedAt = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero)
        });
        index.AddFolder(new SourceFolder
        {
            Path = @"C:\photos\originals",
            Label = "Originals",
            Type = FolderType.Originals,
            Enabled = true
        });
        return index;
    }

    // ─── Round-trip ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task SaveThenLoad_RestoresPhotosAndFolders()
    {
        var cache = CreateCache();
        var source = PopulatedIndex();

        await cache.SaveAsync(source, CancellationToken.None);

        var target = new PhotoIndex();
        var loaded = await cache.TryLoadAsync(target, CancellationToken.None);

        Assert.IsTrue(loaded);
        Assert.AreEqual(1, target.SnapshotPhotos().Count);
        Assert.AreEqual(1, target.SnapshotFolders().Count);
        var photo = target.SnapshotPhotos()[0];
        Assert.AreEqual(@"C:\photos\originals\img001.jpg", photo.FilePath);
        Assert.AreEqual(new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero), photo.CapturedAt);
    }

    // ─── TTL ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task TryLoad_WhenCacheExpired_ReturnsFalse()
    {
        // Create cache with a very short TTL and save
        var cache = CreateCache(ttlHours: 0); // TTL = 0 hours → expires immediately
        var source = PopulatedIndex();
        await cache.SaveAsync(source, CancellationToken.None);

        // Backdate the file's write time by 1 hour to exceed the zero TTL
        var cacheFile = Directory.GetFiles(_tempDir.FullName, "photo-index.json").First();
        File.SetLastWriteTimeUtc(cacheFile, DateTime.UtcNow.AddHours(-1));

        var target = new PhotoIndex();
        var loaded = await cache.TryLoadAsync(target, CancellationToken.None);

        Assert.IsFalse(loaded);
        Assert.AreEqual(0, target.Count);
    }

    // ─── ScanRoots hash mismatch ──────────────────────────────────────────────

    [TestMethod]
    public async Task TryLoad_WhenScanRootsChanged_ReturnsFalse()
    {
        // Save with one set of roots
        var saveCache = CreateCache(scanRoots: [@"C:\photos"]);
        await saveCache.SaveAsync(PopulatedIndex(), CancellationToken.None);

        // Load with different roots — should refuse the cache
        var loadCache = CreateCache(scanRoots: [@"D:\other-photos"]);
        var target = new PhotoIndex();
        var loaded = await loadCache.TryLoadAsync(target, CancellationToken.None);

        Assert.IsFalse(loaded);
    }

    // ─── Missing cache file ───────────────────────────────────────────────────

    [TestMethod]
    public async Task TryLoad_WhenNoCacheFile_ReturnsFalse()
    {
        var cache = CreateCache();
        var target = new PhotoIndex();

        var loaded = await cache.TryLoadAsync(target, CancellationToken.None);

        Assert.IsFalse(loaded);
        Assert.AreEqual(0, target.Count);
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_RemovesCacheFile()
    {
        var cache = CreateCache();
        await cache.SaveAsync(PopulatedIndex(), CancellationToken.None);
        Assert.IsTrue(Directory.GetFiles(_tempDir.FullName, "photo-index.json").Any());

        await cache.DeleteAsync();

        Assert.IsFalse(Directory.GetFiles(_tempDir.FullName, "photo-index.json").Any());
    }

    [TestMethod]
    public async Task DeleteAsync_WhenNoCacheFile_DoesNotThrow()
    {
        var cache = CreateCache();
        await cache.DeleteAsync(); // should not throw
    }

    // ─── Disabled cache (empty directory string) ──────────────────────────────

    [TestMethod]
    public async Task TryLoad_WhenCacheDirectoryEmpty_ReturnsFalse()
    {
        var cache = new PhotoIndexCache(
            Options.Create(new PhotoOrganizerSettings
            {
                ScanRoots = [@"C:\photos"],
                Indexing = new IndexingSettings { CacheDirectory = " " } // whitespace = disabled
            }),
            NullLogger<PhotoIndexCache>.Instance);

        var target = new PhotoIndex();
        var loaded = await cache.TryLoadAsync(target, CancellationToken.None);
        Assert.IsFalse(loaded);
    }
}

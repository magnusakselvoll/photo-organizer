using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;
using PhotoOrganizer.Infrastructure.Services;

namespace PhotoOrganizer.Infrastructure.Tests;

/// <summary>
/// Tests for the sorted-view memoization cache in <see cref="PhotoService"/>
/// (ADR 010). Verifies that the cache returns a stable result on cache hits and
/// invalidates correctly when the repository version changes.
/// </summary>
[TestClass]
public sealed class PhotoServiceCacheTests
{
    private static Photo Make(string name, DateTimeOffset capturedAt) => new()
    {
        Id = Guid.NewGuid(),
        FilePath = $"/photos/{name}.jpg",
        FileName = name,
        FolderType = FolderType.Originals,
        CapturedAt = capturedAt,
    };

    private static readonly PhotoFilter NoFilter = new() { Limit = 100 };

    // ─── Cache-hit behaviour ──────────────────────────────────────────────────

    [TestMethod]
    public async Task CacheHit_SameVersionDespiteUnderlyingChange_ReturnsCachedSet()
    {
        // Arrange: repo starts with one photo and a stable version.
        var photo1 = Make("alpha", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var repo = new MutableStubRepo([photo1], version: 1);
        var service = new PhotoService(repo);

        // First call populates the cache.
        var first = await service.GetPhotosAsync(NoFilter);
        Assert.AreEqual(1, first.TotalCount, "expected 1 photo on first call");

        // Add a photo to the underlying list BUT keep the version the same.
        var photo2 = Make("beta", new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero));
        repo.Photos.Add(photo2);
        // Version stays at 1 — simulates reading between two indexer AddPhoto calls.

        // Second call should return cached result (still 1 photo).
        var second = await service.GetPhotosAsync(NoFilter);
        Assert.AreEqual(1, second.TotalCount, "expected cache hit: underlying change without version bump should not be visible");
    }

    // ─── Cache-invalidation behaviour ─────────────────────────────────────────

    [TestMethod]
    public async Task CacheMiss_VersionBumped_NewPhotoVisible()
    {
        // Arrange: repo starts empty.
        var repo = new MutableStubRepo([], version: 0);
        var service = new PhotoService(repo);

        // First call → empty result; cache is populated for version 0.
        var first = await service.GetPhotosAsync(NoFilter);
        Assert.AreEqual(0, first.TotalCount, "expected 0 photos initially");

        // Add a photo AND bump the version (simulates indexer AddPhoto).
        var photo = Make("gamma", new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero));
        repo.Photos.Add(photo);
        repo.CurrentVersion = 1;

        // Second call must rebuild because version changed.
        var second = await service.GetPhotosAsync(NoFilter);
        Assert.AreEqual(1, second.TotalCount, "expected cache invalidation: new photo must appear after version bump");
    }

    // ─── Stub repository ──────────────────────────────────────────────────────

    private sealed class MutableStubRepo(IEnumerable<Photo> photos, long version) : IPhotoRepository
    {
        public List<Photo> Photos { get; } = photos.ToList();
        public long CurrentVersion { get; set; } = version;

        public long Version => CurrentVersion;

        public Task<IReadOnlyList<Photo>> GetAllPhotosAsync() =>
            Task.FromResult<IReadOnlyList<Photo>>(Photos);

        public Task<Photo?> GetByIdAsync(Guid id) =>
            Task.FromResult(Photos.FirstOrDefault(p => p.Id == id));
    }
}

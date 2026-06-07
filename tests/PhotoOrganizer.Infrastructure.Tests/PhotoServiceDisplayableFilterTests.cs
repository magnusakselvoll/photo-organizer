using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;
using PhotoOrganizer.Infrastructure.Services;

namespace PhotoOrganizer.Infrastructure.Tests;

/// <summary>
/// Tests that PhotoService.GetPhotosAsync excludes non-displayable photos (RAW, bare TIFF)
/// from grid and slideshow listings, while keeping browser-native and transcodable (HEIC) photos.
/// </summary>
[TestClass]
public sealed class PhotoServiceDisplayableFilterTests
{
    private static readonly DateTimeOffset AnyDate = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid GroupA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000");

    private static Photo Make(string name, string extension, bool isPreferred = false, Guid? duplicateGroupId = null) => new()
    {
        Id = Guid.NewGuid(),
        FilePath = $"/photos/{name}{extension}",
        FileName = $"{name}{extension}",
        FolderType = FolderType.Originals,
        CapturedAt = AnyDate,
        IsPreferred = isPreferred,
        DuplicateGroupId = duplicateGroupId,
    };

    private static async Task<PhotoPageDto> GetAllAsync(IEnumerable<Photo> photos)
    {
        var repo = new StubPhotoRepository(photos);
        var service = new PhotoService(repo);
        return await service.GetPhotosAsync(new PhotoFilter
        {
            Deduplicated = false,
            Page = 1,
            PageSize = int.MaxValue
        });
    }

    private static async Task<PhotoPageDto> GetDeduplicatedAsync(IEnumerable<Photo> photos)
    {
        var repo = new StubPhotoRepository(photos);
        var service = new PhotoService(repo);
        return await service.GetPhotosAsync(new PhotoFilter
        {
            Deduplicated = true,
            Page = 1,
            PageSize = int.MaxValue
        });
    }

    // ─── Singleton non-displayable files are excluded ─────────────────────────

    [TestMethod]
    [DataRow(".cr2")]
    [DataRow(".cr3")]
    [DataRow(".orf")]
    [DataRow(".arw")]
    [DataRow(".nef")]
    [DataRow(".rw2")]
    [DataRow(".tiff")]
    [DataRow(".tif")]
    public async Task GetPhotosAsync_SingletonRaw_IsExcluded(string extension)
    {
        var photos = new[] { Make("IMG_1001", extension) };

        var page = await GetAllAsync(photos);

        Assert.AreEqual(0, page.TotalCount);
        Assert.AreEqual(0, page.Items.Count);
    }

    // ─── Singleton displayable files are kept ─────────────────────────────────

    [TestMethod]
    [DataRow(".jpg")]
    [DataRow(".jpeg")]
    [DataRow(".png")]
    [DataRow(".gif")]
    [DataRow(".webp")]
    [DataRow(".avif")]
    [DataRow(".bmp")]
    public async Task GetPhotosAsync_SingletonBrowserNative_IsKept(string extension)
    {
        var photos = new[] { Make("IMG_1001", extension) };

        var page = await GetAllAsync(photos);

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual(1, page.Items.Count);
    }

    [TestMethod]
    [DataRow(".heic")]
    [DataRow(".heif")]
    public async Task GetPhotosAsync_SingletonTranscodable_IsKept(string extension)
    {
        var photos = new[] { Make("IMG_1001", extension) };

        var page = await GetAllAsync(photos);

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual(1, page.Items.Count);
    }

    // ─── TotalCount reflects the displayable-only set ─────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_TotalCountExcludesNonDisplayable()
    {
        var photos = new[]
        {
            Make("IMG_1001", ".jpg"),
            Make("IMG_1002", ".cr2"),  // non-displayable
            Make("IMG_1003", ".heic"),
        };

        var page = await GetAllAsync(photos);

        Assert.AreEqual(2, page.TotalCount, "TotalCount should count only displayable photos");
        CollectionAssert.AreEquivalent(
            new[] { "IMG_1001.jpg", "IMG_1003.heic" },
            page.Items.Select(p => p.FileName).ToArray());
    }

    // ─── Duplicate group: JPG preferred, RAW non-preferred → only JPG returned ─

    [TestMethod]
    public async Task GetPhotosAsync_DuplicateGroup_JpgAndRaw_DeduplicatedReturnsJpg()
    {
        var photos = new[]
        {
            Make("IMG_1001", ".jpg",  isPreferred: true,  duplicateGroupId: GroupA),
            Make("IMG_1001", ".cr2",  isPreferred: false, duplicateGroupId: GroupA),
        };

        var page = await GetDeduplicatedAsync(photos);

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual("IMG_1001.jpg", page.Items[0].FileName);
    }

    [TestMethod]
    public async Task GetPhotosAsync_DuplicateGroup_JpgAndRaw_NonDeduplicatedReturnsJpgOnly()
    {
        // Even without deduplication, the RAW must be filtered out as non-displayable.
        var photos = new[]
        {
            Make("IMG_1001", ".jpg",  isPreferred: true,  duplicateGroupId: GroupA),
            Make("IMG_1001", ".cr2",  isPreferred: false, duplicateGroupId: GroupA),
        };

        var page = await GetAllAsync(photos);

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual("IMG_1001.jpg", page.Items[0].FileName);
    }

    // ─── All-non-displayable duplicate group → empty results ──────────────────

    [TestMethod]
    public async Task GetPhotosAsync_AllRawDuplicateGroup_ReturnsEmpty()
    {
        var photos = new[]
        {
            Make("IMG_1001", ".cr2", isPreferred: true,  duplicateGroupId: GroupA),
            Make("IMG_1001", ".nef", isPreferred: false, duplicateGroupId: GroupA),
        };

        var page = await GetDeduplicatedAsync(photos);

        Assert.AreEqual(0, page.TotalCount);
        Assert.AreEqual(0, page.Items.Count);
    }

    // ─── Mixed displayable types are all kept ─────────────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_MixedDisplayableTypes_AllKept()
    {
        var photos = new[]
        {
            Make("a", ".jpg"),
            Make("b", ".png"),
            Make("c", ".heic"),
            Make("d", ".webp"),
        };

        var page = await GetAllAsync(photos);

        Assert.AreEqual(4, page.TotalCount);
    }

    // ─── Stub repository ──────────────────────────────────────────────────────

    private sealed class StubPhotoRepository(IEnumerable<Photo> photos) : IPhotoRepository
    {
        private readonly IReadOnlyList<Photo> _photos = photos.ToList();

        public Task<IReadOnlyList<Photo>> GetAllPhotosAsync() => Task.FromResult(_photos);
        public Task<Photo?> GetByIdAsync(Guid id) => Task.FromResult(_photos.FirstOrDefault(p => p.Id == id));
    }
}

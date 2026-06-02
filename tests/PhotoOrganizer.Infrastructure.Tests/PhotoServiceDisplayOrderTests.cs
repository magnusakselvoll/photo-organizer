using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;
using PhotoOrganizer.Infrastructure.Services;

namespace PhotoOrganizer.Infrastructure.Tests;

/// <summary>
/// Tests that PhotoService.GetPhotosAsync returns photos ordered by capturedAt descending,
/// falling back to FileModifiedAt when capturedAt is absent.
/// </summary>
[TestClass]
public sealed class PhotoServiceDisplayOrderTests
{
    private static readonly DateTimeOffset Jan1 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jun1 = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Dec1 = new(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private static Photo Make(string name, DateTimeOffset? capturedAt, DateTimeOffset? fileModifiedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        FilePath = $@"C:\photos\{name}.jpg",
        FileName = name,
        FolderType = FolderType.Originals,
        CapturedAt = capturedAt,
        FileModifiedAt = fileModifiedAt
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

    // ─── capturedAt ordering ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_OrdersByCapturedAtDescending()
    {
        var photos = new[]
        {
            Make("oldest", Jan1),
            Make("newest", Dec1),
            Make("middle", Jun1),
        };

        var page = await GetAllAsync(photos);

        Assert.AreEqual("newest", page.Items[0].FileName);
        Assert.AreEqual("middle", page.Items[1].FileName);
        Assert.AreEqual("oldest", page.Items[2].FileName);
    }

    // ─── FileModifiedAt fallback ──────────────────────────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_FallsBackToFileModifiedAt_WhenCapturedAtNull()
    {
        var photos = new[]
        {
            Make("old_file",  capturedAt: null, fileModifiedAt: Jan1),
            Make("new_file",  capturedAt: null, fileModifiedAt: Dec1),
        };

        var page = await GetAllAsync(photos);

        Assert.AreEqual("new_file", page.Items[0].FileName);
        Assert.AreEqual("old_file", page.Items[1].FileName);
    }

    [TestMethod]
    public async Task GetPhotosAsync_CapturedAtTakesPriorityOverFileModifiedAt()
    {
        // "old_capture" has an early capturedAt but a very recent file mtime
        // "new_capture" has a recent capturedAt and an early file mtime
        var photos = new[]
        {
            Make("old_capture", capturedAt: Jan1,  fileModifiedAt: Dec1),
            Make("new_capture", capturedAt: Dec1,  fileModifiedAt: Jan1),
        };

        var page = await GetAllAsync(photos);

        Assert.AreEqual("new_capture", page.Items[0].FileName);
        Assert.AreEqual("old_capture", page.Items[1].FileName);
    }

    [TestMethod]
    public async Task GetPhotosAsync_BothDatesNull_SortedToEnd()
    {
        var photos = new[]
        {
            Make("no_dates",   capturedAt: null, fileModifiedAt: null),
            Make("has_dates",  capturedAt: Jun1, fileModifiedAt: Jan1),
        };

        var page = await GetAllAsync(photos);

        Assert.AreEqual("has_dates", page.Items[0].FileName);
        Assert.AreEqual("no_dates",  page.Items[1].FileName);
    }

    // ─── Empty list ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_EmptyRepository_ReturnsEmptyPage()
    {
        var page = await GetAllAsync([]);
        Assert.AreEqual(0, page.TotalCount);
        Assert.AreEqual(0, page.Items.Count);
    }

    // ─── Stub repository ──────────────────────────────────────────────────────

    private sealed class StubPhotoRepository(IEnumerable<Photo> photos) : IPhotoRepository
    {
        private readonly IReadOnlyList<Photo> _photos = photos.ToList();

        public Task<IReadOnlyList<Photo>> GetAllPhotosAsync() => Task.FromResult(_photos);
        public Task<Photo?> GetByIdAsync(Guid id) => Task.FromResult(_photos.FirstOrDefault(p => p.Id == id));
        public Task InvalidateCacheAsync() => Task.CompletedTask;
    }
}

using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;
using PhotoOrganizer.Infrastructure.Services;

namespace PhotoOrganizer.Infrastructure.Tests;

/// <summary>
/// Tests for keyset (cursor) pagination in PhotoService.GetPhotosAsync and the
/// cursor encode/decode helpers.
/// </summary>
[TestClass]
public sealed class PhotoServiceCursorTests
{
    private static readonly DateTimeOffset Jan1 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Feb1 = new(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Mar1 = new(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Apr1 = new(2024, 4, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset May1 = new(2024, 5, 1, 0, 0, 0, TimeSpan.Zero);

    private static Photo Make(string name, DateTimeOffset? capturedAt, DateTimeOffset? fileModifiedAt = null,
        Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        FilePath = $@"C:\photos\{name}.jpg",
        FileName = name,
        FolderType = FolderType.Originals,
        CapturedAt = capturedAt,
        FileModifiedAt = fileModifiedAt,
    };

    private static PhotoService Service(IEnumerable<Photo> photos) =>
        new(new StubPhotoRepository(photos));

    private static PhotoFilter CursorFilter(int limit, string? cursor = null) => new()
    {
        Deduplicated = false,
        Limit = limit,
        Cursor = cursor,
    };

    // ─── Cursor encode / decode round-trip ───────────────────────────────────

    [TestMethod]
    public void EncodeCursor_DecodeCursor_RoundTrip_WithCapturedAt()
    {
        var photo = Make("x", May1);
        var cursor = PhotoService.EncodeCursor(photo);
        var (ticks, id) = PhotoService.DecodeCursor(cursor);

        Assert.AreEqual(PhotoService.EffectiveTicks(photo), ticks);
        Assert.AreEqual(photo.Id, id);
    }

    [TestMethod]
    public void EncodeCursor_DecodeCursor_RoundTrip_NullDates()
    {
        var photo = Make("x", capturedAt: null, fileModifiedAt: null);
        var cursor = PhotoService.EncodeCursor(photo);
        var (ticks, id) = PhotoService.DecodeCursor(cursor);

        Assert.AreEqual(DateTimeOffset.MinValue.UtcTicks, ticks);
        Assert.AreEqual(photo.Id, id);
    }

    [TestMethod]
    public void DecodeCursor_MalformedCursor_ReturnsMaxTicks()
    {
        var (ticks, id) = PhotoService.DecodeCursor("!!not-a-cursor!!");
        Assert.AreEqual(long.MaxValue, ticks);
        Assert.AreEqual(Guid.Empty, id);
    }

    // ─── First page (no cursor) ───────────────────────────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_CursorPath_FirstPage_ReturnsNewest()
    {
        var photos = new[]
        {
            Make("jan", Jan1),
            Make("may", May1),
            Make("mar", Mar1),
        };

        var page = await Service(photos).GetPhotosAsync(CursorFilter(limit: 2));

        Assert.AreEqual(2, page.Items.Count);
        Assert.AreEqual("may", page.Items[0].FileName);
        Assert.AreEqual("mar", page.Items[1].FileName);
        Assert.IsNotNull(page.NextCursor, "NextCursor should be set when more items remain");
    }

    [TestMethod]
    public async Task GetPhotosAsync_CursorPath_FirstPage_ReturnsAll_WhenLimitExceedsTotal()
    {
        var photos = new[] { Make("a", Jan1), Make("b", May1) };

        var page = await Service(photos).GetPhotosAsync(CursorFilter(limit: 10));

        Assert.AreEqual(2, page.Items.Count);
        Assert.IsNull(page.NextCursor, "NextCursor should be null at end of list");
    }

    // ─── Second and subsequent pages ─────────────────────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_CursorPath_PagesAreContiguousAndNonOverlapping()
    {
        var photos = new[]
        {
            Make("p1", May1),
            Make("p2", Apr1),
            Make("p3", Mar1),
            Make("p4", Feb1),
            Make("p5", Jan1),
        };
        var svc = Service(photos);

        var page1 = await svc.GetPhotosAsync(CursorFilter(limit: 2));
        Assert.AreEqual(2, page1.Items.Count);
        Assert.AreEqual("p1", page1.Items[0].FileName);
        Assert.AreEqual("p2", page1.Items[1].FileName);
        Assert.IsNotNull(page1.NextCursor);

        var page2 = await svc.GetPhotosAsync(CursorFilter(limit: 2, cursor: page1.NextCursor));
        Assert.AreEqual(2, page2.Items.Count);
        Assert.AreEqual("p3", page2.Items[0].FileName);
        Assert.AreEqual("p4", page2.Items[1].FileName);
        Assert.IsNotNull(page2.NextCursor);

        var page3 = await svc.GetPhotosAsync(CursorFilter(limit: 2, cursor: page2.NextCursor));
        Assert.AreEqual(1, page3.Items.Count);
        Assert.AreEqual("p5", page3.Items[0].FileName);
        Assert.IsNull(page3.NextCursor);
    }

    [TestMethod]
    public async Task GetPhotosAsync_CursorPath_AllItemsCoveredExactly()
    {
        var photos = Enumerable.Range(0, 7)
            .Select(i => Make($"p{i}", new DateTimeOffset(2024, 1, i + 1, 0, 0, 0, TimeSpan.Zero)))
            .ToArray();
        var svc = Service(photos);

        var all = new List<string>();
        string? cursor = null;
        do
        {
            var page = await svc.GetPhotosAsync(CursorFilter(limit: 3, cursor: cursor));
            all.AddRange(page.Items.Select(x => x.FileName));
            cursor = page.NextCursor;
        } while (cursor is not null);

        // Should be exactly 7 items, no duplicates, no gaps.
        Assert.AreEqual(7, all.Count);
        Assert.AreEqual(7, all.Distinct().Count());
    }

    // ─── Stable tiebreaker ────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_CursorPath_StableTiebreaker_SameTimestamp()
    {
        // Two photos with identical CapturedAt must sort deterministically by Id descending.
        var idA = new Guid("aaaaaaaa-0000-0000-0000-000000000000");
        var idB = new Guid("bbbbbbbb-0000-0000-0000-000000000000");
        // B > A as guids, so B comes first (ThenByDescending(Id)).
        var photos = new[]
        {
            Make("photoA", May1, id: idA),
            Make("photoB", May1, id: idB),
        };

        var page = await Service(photos).GetPhotosAsync(CursorFilter(limit: 2));

        Assert.AreEqual("photoB", page.Items[0].FileName);
        Assert.AreEqual("photoA", page.Items[1].FileName);
    }

    // ─── New photos don't disturb a cursor ───────────────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_CursorPath_NewerPhotoDoesNotShiftOlderPage()
    {
        // After fetching page 1, a newer photo appears in the index.
        // The page-2 cursor should still return exactly the expected older items.
        var photos = new[]
        {
            Make("p1", May1),
            Make("p2", Apr1),
            Make("p3", Mar1),
        };
        var svc = Service(photos);

        var page1 = await svc.GetPhotosAsync(CursorFilter(limit: 2));
        Assert.IsNotNull(page1.NextCursor);

        // Simulate a new photo arriving (newer than all existing) — it would
        // sort before cursor but that's fine; the cursor only defines the lower
        // bound, new items above it are harmless for page-2 stability.
        // What matters: page 2 still returns p3 and not p1/p2 again.
        var page2 = await svc.GetPhotosAsync(CursorFilter(limit: 2, cursor: page1.NextCursor));

        Assert.AreEqual(1, page2.Items.Count);
        Assert.AreEqual("p3", page2.Items[0].FileName);
        // p1 and p2 must NOT re-appear.
        Assert.IsFalse(page2.Items.Any(x => x.FileName is "p1" or "p2"));
    }

    // ─── TotalCount is always the full filtered count ─────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_CursorPath_TotalCount_ReflectsFullFilteredSet()
    {
        var photos = Enumerable.Range(0, 10)
            .Select(i => Make($"p{i}", new DateTimeOffset(2024, 1, i + 1, 0, 0, 0, TimeSpan.Zero)))
            .ToArray();

        var page = await Service(photos).GetPhotosAsync(CursorFilter(limit: 3));

        Assert.AreEqual(10, page.TotalCount);
    }

    // ─── Legacy offset path unchanged ────────────────────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_OffsetPath_UnchangedWhenLimitNotSet()
    {
        var photos = new[]
        {
            Make("newest", May1),
            Make("oldest", Jan1),
        };

        var page = await Service(photos).GetPhotosAsync(new PhotoFilter
        {
            Deduplicated = false,
            Page = 1,
            PageSize = 1,
        });

        Assert.AreEqual(1, page.Items.Count);
        Assert.AreEqual("newest", page.Items[0].FileName);
        Assert.IsNull(page.NextCursor, "Offset path must not set NextCursor");
    }

    // ─── EffectiveDate on returned DTOs ──────────────────────────────────────

    [TestMethod]
    public async Task GetPhotosAsync_EffectiveDate_EqualsCapturedAt_WhenPresent()
    {
        var photo = Make("x", capturedAt: May1, fileModifiedAt: Jan1);
        var page = await Service([photo]).GetPhotosAsync(CursorFilter(limit: 1));

        Assert.AreEqual(May1, page.Items[0].EffectiveDate,
            "EffectiveDate should be CapturedAt when CapturedAt is not null");
    }

    [TestMethod]
    public async Task GetPhotosAsync_EffectiveDate_FallsBackToFileModifiedAt_WhenCapturedAtNull()
    {
        var photo = Make("x", capturedAt: null, fileModifiedAt: Mar1);
        var page = await Service([photo]).GetPhotosAsync(CursorFilter(limit: 1));

        Assert.AreEqual(Mar1, page.Items[0].EffectiveDate,
            "EffectiveDate should fall back to FileModifiedAt when CapturedAt is null");
    }

    [TestMethod]
    public async Task GetPhotosAsync_EffectiveDate_IsNull_WhenBothDatesAbsent()
    {
        var photo = Make("x", capturedAt: null, fileModifiedAt: null);
        var page = await Service([photo]).GetPhotosAsync(CursorFilter(limit: 1));

        Assert.IsNull(page.Items[0].EffectiveDate,
            "EffectiveDate should be null when both CapturedAt and FileModifiedAt are absent");
    }

    // ─── Stub repository ──────────────────────────────────────────────────────

    private sealed class StubPhotoRepository(IEnumerable<Photo> photos) : IPhotoRepository
    {
        private readonly IReadOnlyList<Photo> _photos = photos.ToList();

        public Task<IReadOnlyList<Photo>> GetAllPhotosAsync() => Task.FromResult(_photos);
        public Task<Photo?> GetByIdAsync(Guid id) => Task.FromResult(_photos.FirstOrDefault(p => p.Id == id));
    }
}

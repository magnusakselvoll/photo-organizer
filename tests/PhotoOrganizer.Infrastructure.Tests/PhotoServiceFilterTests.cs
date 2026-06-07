using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;
using PhotoOrganizer.Infrastructure.Services;

namespace PhotoOrganizer.Infrastructure.Tests;

/// <summary>
/// Tests for the expanded browse filters: filename search, date range.
/// </summary>
[TestClass]
public sealed class PhotoServiceFilterTests
{
    private static readonly DateTimeOffset Jan15 = new(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jun15 = new(2024, 6, 15, 23, 59, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Dec31 = new(2024, 12, 31, 0, 0, 0, TimeSpan.Zero);

    private static Photo Make(
        string filePath,
        DateTimeOffset? capturedAt = null,
        DateTimeOffset? fileModifiedAt = null,
        IReadOnlyList<string>? tags = null) => new()
    {
        Id = Guid.NewGuid(),
        FilePath = filePath,
        FileName = Path.GetFileNameWithoutExtension(filePath),
        FolderType = FolderType.Originals,
        CapturedAt = capturedAt,
        FileModifiedAt = fileModifiedAt,
        Tags = tags ?? [],
    };

    private static async Task<PhotoPageDto> GetAllAsync(IEnumerable<Photo> photos, PhotoFilter? filter = null)
    {
        var repo = new StubPhotoRepository(photos);
        var service = new PhotoService(repo);
        return await service.GetPhotosAsync(filter ?? new PhotoFilter
        {
            Deduplicated = false,
            Page = 1,
            PageSize = int.MaxValue,
        });
    }

    // ─── Filename filter ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task FileName_SubstringMatch_CaseInsensitive()
    {
        var photos = new[]
        {
            Make("/photos/IMG_1001.jpg", capturedAt: Jan15),
            Make("/photos/vacation.png", capturedAt: Jun15),
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            FileName = "img",
        });

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual("IMG_1001.jpg", page.Items[0].FileName);
    }

    [TestMethod]
    public async Task FileName_MatchesExtension()
    {
        var photos = new[]
        {
            Make("/photos/photo.jpg", capturedAt: Jan15),
            Make("/photos/photo.png", capturedAt: Jun15),
            Make("/photos/photo.heic", capturedAt: Dec31),
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            FileName = ".png",
        });

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual("photo.png", page.Items[0].FileName);
        Assert.IsTrue(page.Items[0].FilePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task FileName_Empty_ReturnsAll()
    {
        var photos = new[]
        {
            Make("/photos/IMG_1001.jpg", capturedAt: Jan15),
            Make("/photos/vacation.png", capturedAt: Jun15),
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            FileName = "",
        });

        Assert.AreEqual(2, page.TotalCount);
    }

    [TestMethod]
    public async Task FileName_Null_ReturnsAll()
    {
        var photos = new[]
        {
            Make("/photos/IMG_1001.jpg", capturedAt: Jan15),
            Make("/photos/vacation.png", capturedAt: Jun15),
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            FileName = null,
        });

        Assert.AreEqual(2, page.TotalCount);
    }

    [TestMethod]
    public async Task FileName_NoMatch_ReturnsEmpty()
    {
        var photos = new[]
        {
            Make("/photos/IMG_1001.jpg", capturedAt: Jan15),
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            FileName = "DOES_NOT_EXIST",
        });

        Assert.AreEqual(0, page.TotalCount);
    }

    // ─── Date range — DateFrom ────────────────────────────────────────────────

    [TestMethod]
    public async Task DateFrom_InclusiveLowerBound()
    {
        var photos = new[]
        {
            Make("/photos/jan.jpg", capturedAt: Jan15),  // 2024-01-15
            Make("/photos/jun.jpg", capturedAt: Jun15),  // 2024-06-15
            Make("/photos/dec.jpg", capturedAt: Dec31),  // 2024-12-31
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            DateFrom = new DateOnly(2024, 6, 15),
        });

        Assert.AreEqual(2, page.TotalCount);
        Assert.IsTrue(page.Items.All(p => p.EffectiveDate >= new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero)));
    }

    // ─── Date range — DateTo ──────────────────────────────────────────────────

    [TestMethod]
    public async Task DateTo_InclusiveUpperBound_WholeDay()
    {
        // jun.jpg is at 23:59 on 2024-06-15 — it should be included in "to 2024-06-15".
        var photos = new[]
        {
            Make("/photos/jan.jpg", capturedAt: Jan15),  // 2024-01-15
            Make("/photos/jun.jpg", capturedAt: Jun15),  // 2024-06-15T23:59
            Make("/photos/dec.jpg", capturedAt: Dec31),  // 2024-12-31
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            DateTo = new DateOnly(2024, 6, 15),
        });

        Assert.AreEqual(2, page.TotalCount);
        var names = page.Items.Select(p => p.FileName).ToList();
        CollectionAssert.Contains(names, "jan.jpg");
        CollectionAssert.Contains(names, "jun.jpg");
        CollectionAssert.DoesNotContain(names, "dec.jpg");
    }

    // ─── Date range — both bounds ─────────────────────────────────────────────

    [TestMethod]
    public async Task DateFromAndTo_BothBoundsApplied()
    {
        var photos = new[]
        {
            Make("/photos/jan.jpg", capturedAt: Jan15),  // 2024-01-15
            Make("/photos/jun.jpg", capturedAt: Jun15),  // 2024-06-15
            Make("/photos/dec.jpg", capturedAt: Dec31),  // 2024-12-31
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            DateFrom = new DateOnly(2024, 3, 1),
            DateTo = new DateOnly(2024, 9, 1),
        });

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual("jun.jpg", page.Items[0].FileName);
    }

    [TestMethod]
    public async Task DateFrom_Equals_DateTo_OnlyThatDayReturned()
    {
        var photos = new[]
        {
            Make("/photos/jan.jpg", capturedAt: Jan15),
            Make("/photos/jun.jpg", capturedAt: Jun15),
            Make("/photos/dec.jpg", capturedAt: Dec31),
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            DateFrom = new DateOnly(2024, 6, 15),
            DateTo = new DateOnly(2024, 6, 15),
        });

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual("jun.jpg", page.Items[0].FileName);
    }

    [TestMethod]
    public async Task DateFrom_GreaterThan_DateTo_ReturnsEmpty()
    {
        var photos = new[]
        {
            Make("/photos/jan.jpg", capturedAt: Jan15),
            Make("/photos/jun.jpg", capturedAt: Jun15),
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            DateFrom = new DateOnly(2024, 12, 1),
            DateTo = new DateOnly(2024, 1, 1),
        });

        Assert.AreEqual(0, page.TotalCount);
    }

    // ─── Null effective date ──────────────────────────────────────────────────

    [TestMethod]
    public async Task NullEffectiveDate_ExcludedWhenDateFromSet()
    {
        var photos = new[]
        {
            Make("/photos/dated.jpg", capturedAt: Jun15),
            Make("/photos/undated.jpg", capturedAt: null, fileModifiedAt: null),
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            DateFrom = new DateOnly(2024, 1, 1),
        });

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual("dated.jpg", page.Items[0].FileName);
    }

    [TestMethod]
    public async Task NullEffectiveDate_ExcludedWhenDateToSet()
    {
        var photos = new[]
        {
            Make("/photos/dated.jpg", capturedAt: Jun15),
            Make("/photos/undated.jpg", capturedAt: null, fileModifiedAt: null),
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            DateTo = new DateOnly(2024, 12, 31),
        });

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual("dated.jpg", page.Items[0].FileName);
    }

    [TestMethod]
    public async Task NoBounds_ReturnsAll_IncludingUndated()
    {
        var photos = new[]
        {
            Make("/photos/dated.jpg", capturedAt: Jun15),
            Make("/photos/undated.jpg", capturedAt: null, fileModifiedAt: null),
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
        });

        Assert.AreEqual(2, page.TotalCount);
    }

    // ─── Composition ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task FilenameAndDateRange_ComposeTogether()
    {
        var photos = new[]
        {
            Make("/photos/IMG_1001.jpg", capturedAt: Jan15),
            Make("/photos/IMG_1002.jpg", capturedAt: Jun15),
            Make("/photos/vacation.jpg", capturedAt: Jun15),
        };

        var page = await GetAllAsync(photos, new PhotoFilter
        {
            Deduplicated = false,
            Page = 1, PageSize = int.MaxValue,
            FileName = "IMG",
            DateFrom = new DateOnly(2024, 6, 1),
        });

        Assert.AreEqual(1, page.TotalCount);
        Assert.AreEqual("IMG_1002.jpg", page.Items[0].FileName);
    }

    // ─── Cursor stability with new filters ───────────────────────────────────

    [TestMethod]
    public async Task FilenameFilter_CursorPagination_NoDuplicatesNoGaps()
    {
        // 5 photos matching "IMG"; paging with limit=2 should cover all 5 exactly.
        var photos = Enumerable.Range(1, 5)
            .Select(i => Make($"/photos/IMG_{i:D4}.jpg",
                capturedAt: new DateTimeOffset(2024, 1, i, 0, 0, 0, TimeSpan.Zero)))
            .Concat(new[]
            {
                // Two non-matching photos to ensure they stay filtered out across pages.
                Make("/photos/vacation_1.jpg", capturedAt: new DateTimeOffset(2024, 1, 6, 0, 0, 0, TimeSpan.Zero)),
                Make("/photos/vacation_2.jpg", capturedAt: new DateTimeOffset(2024, 1, 7, 0, 0, 0, TimeSpan.Zero)),
            })
            .ToArray();

        var svc = new PhotoService(new StubPhotoRepository(photos));
        var all = new List<string>();
        string? cursor = null;
        do
        {
            var page = await svc.GetPhotosAsync(new PhotoFilter
            {
                Deduplicated = false,
                Limit = 2,
                Cursor = cursor,
                FileName = "IMG",
            });
            all.AddRange(page.Items.Select(x => x.FileName));
            cursor = page.NextCursor;
        } while (cursor is not null);

        Assert.AreEqual(5, all.Count, "Exactly 5 matching photos expected");
        Assert.AreEqual(5, all.Distinct().Count(), "No duplicates expected");
        Assert.IsTrue(all.All(n => n.StartsWith("IMG_", StringComparison.OrdinalIgnoreCase)),
            "Only IMG_ photos should appear");
    }

    // ─── Stub repository ──────────────────────────────────────────────────────

    private sealed class StubPhotoRepository(IEnumerable<Photo> photos) : IPhotoRepository
    {
        private readonly IReadOnlyList<Photo> _photos = photos.ToList();

        public Task<IReadOnlyList<Photo>> GetAllPhotosAsync() => Task.FromResult(_photos);
        public Task<Photo?> GetByIdAsync(Guid id) => Task.FromResult(_photos.FirstOrDefault(p => p.Id == id));
    }
}

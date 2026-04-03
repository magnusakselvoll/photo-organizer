using PhotoOrganizer.Crawler.Pipeline;
using PhotoOrganizer.Crawler.Sidecars;
using PhotoOrganizer.Crawler.Steps;
using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.Crawler.Tests;

[TestClass]
public class DuplicatesStepTests
{
    // ---- Name normalization ----

    [TestMethod]
    public void NormalizeName_NoSuffix_LowercaseName()
    {
        Assert.AreEqual("photo", DuplicatesStep.NormalizeName("/photos/Photo.jpg"));
    }

    [TestMethod]
    [DataRow("/photos/photo_edit.jpg", "photo")]
    [DataRow("/photos/photo_edited.jpg", "photo")]
    [DataRow("/photos/photo-edit.jpg", "photo")]
    [DataRow("/photos/photo-edited.jpg", "photo")]
    [DataRow("/photos/photo_retouched.jpg", "photo")]
    [DataRow("/photos/photo-hdr.jpg", "photo")]
    [DataRow("/photos/photo_hdr.jpg", "photo")]
    public void NormalizeName_StripsSuffix(string filePath, string expected)
    {
        Assert.AreEqual(expected, DuplicatesStep.NormalizeName(filePath));
    }

    [TestMethod]
    public void NormalizeName_SuffixStrip_CaseInsensitive()
    {
        Assert.AreEqual("photo", DuplicatesStep.NormalizeName("/photos/Photo_EDIT.jpg"));
    }

    [TestMethod]
    public void NormalizeName_NoDuplicateSuffixStrip()
    {
        // Only one suffix stripped
        Assert.AreEqual("photo_edit", DuplicatesStep.NormalizeName("/photos/photo_edit_edit.jpg"));
    }

    [TestMethod]
    [DataRow("/photos/photo copy.jpg", "photo")]
    [DataRow("/photos/photo copy 2.jpg", "photo")]
    [DataRow("/photos/photo copy 37.jpg", "photo")]
    [DataRow("/photos/photo COPY.jpg", "photo")]
    public void NormalizeName_StripsMacOsCopySuffix(string filePath, string expected)
    {
        Assert.AreEqual(expected, DuplicatesStep.NormalizeName(filePath));
    }

    [TestMethod]
    [DataRow("/photos/20260405_photo.jpg", "photo")]
    [DataRow("/photos/20260405-photo.jpg", "photo")]
    [DataRow("/photos/20260405photo.jpg", "photo")]
    [DataRow("/photos/20260405_123456_photo.jpg", "photo")]
    [DataRow("/photos/20260405123456_photo.jpg", "photo")]
    [DataRow("/photos/20260405123456photo.jpg", "photo")]
    public void NormalizeName_StripsDatePrefix(string filePath, string expected)
    {
        Assert.AreEqual(expected, DuplicatesStep.NormalizeName(filePath));
    }

    [TestMethod]
    public void NormalizeName_DatePrefixAndEditSuffix_BothStripped()
    {
        Assert.AreEqual("photo", DuplicatesStep.NormalizeName("/photos/20260405_photo_edit.jpg"));
    }

    [TestMethod]
    public void NormalizeName_CopySuffixAndEditSuffix_BothStripped()
    {
        Assert.AreEqual("photo", DuplicatesStep.NormalizeName("/photos/photo_edit copy 2.jpg"));
    }

    [TestMethod]
    public void NormalizeName_MacOsCopyMatchesOriginal()
    {
        // The user's real-world case
        var original = DuplicatesStep.NormalizeName("/photos/3423523F-05E6-44D9-93B4-26EDDDC653EC_1_105_c.jpeg");
        var copy     = DuplicatesStep.NormalizeName("/photos/3423523F-05E6-44D9-93B4-26EDDDC653EC_1_105_c copy.jpeg");
        Assert.AreEqual(original, copy);
    }

    [TestMethod]
    public void NormalizeName_DatePrefixedMatchesOriginal()
    {
        // The user's real-world case
        var original = DuplicatesStep.NormalizeName("/photos/photo123.jpg");
        var dated    = DuplicatesStep.NormalizeName("/photos/20260405_photo123.jpg");
        Assert.AreEqual(original, dated);
    }

    // ---- Grouping ----

    [TestMethod]
    public async Task UniqueFiles_NoDuplicateGroupId()
    {
        var store = new InMemorySidecarStore();
        var step = new DuplicatesStep();
        var context = new BatchProcessingContext
        {
            FilePaths = ["/photos/alpha.jpg", "/photos/beta.jpg"],
            SidecarStore = store
        };

        await step.ExecuteAsync(context);

        // No sidecars written (nothing to clear, nothing to group)
        Assert.AreEqual(0, store.WrittenSidecars.Count);
    }

    [TestMethod]
    public async Task TwoFilesWithMatchingNormalizedName_ShareDuplicateGroupId()
    {
        var store = new InMemorySidecarStore();
        var step = new DuplicatesStep();
        var context = new BatchProcessingContext
        {
            FilePaths = ["/photos/photo.jpg", "/photos/photo_edit.jpg"],
            SidecarStore = store
        };

        await step.ExecuteAsync(context);

        var s1 = store.WrittenSidecars["/photos/photo.jpg"];
        var s2 = store.WrittenSidecars["/photos/photo_edit.jpg"];
        Assert.IsNotNull(s1.DuplicateGroupId);
        Assert.AreEqual(s1.DuplicateGroupId, s2.DuplicateGroupId);
    }

    [TestMethod]
    public async Task DuplicateGroupId_IsStableAcrossRuns()
    {
        var store1 = new InMemorySidecarStore();
        var store2 = new InMemorySidecarStore();
        var filePaths = new List<string> { "/photos/photo.jpg", "/photos/photo_edit.jpg" };
        var step = new DuplicatesStep();

        await step.ExecuteAsync(new BatchProcessingContext { FilePaths = filePaths, SidecarStore = store1 });
        await step.ExecuteAsync(new BatchProcessingContext { FilePaths = filePaths, SidecarStore = store2 });

        var id1 = store1.WrittenSidecars["/photos/photo.jpg"].DuplicateGroupId;
        var id2 = store2.WrittenSidecars["/photos/photo.jpg"].DuplicateGroupId;
        Assert.AreEqual(id1, id2);
    }

    // ---- Preference selection ----

    [TestMethod]
    public async Task EditsFolderPreferredOverOriginals()
    {
        var store = new InMemorySidecarStore();
        store.FolderSidecars["/originals"] = new FolderSidecar { Label = "Originals", Type = "originals", Enabled = true };
        store.FolderSidecars["/edits"] = new FolderSidecar { Label = "Edits", Type = "edits", Enabled = true };

        var step = new DuplicatesStep();
        var context = new BatchProcessingContext
        {
            FilePaths = ["/originals/photo.jpg", "/edits/photo.jpg"],
            SidecarStore = store,
            GetLastModified = _ => DateTime.MinValue
        };

        await step.ExecuteAsync(context);

        Assert.IsFalse(store.WrittenSidecars["/originals/photo.jpg"].IsPreferred);
        Assert.IsTrue(store.WrittenSidecars["/edits/photo.jpg"].IsPreferred);
    }

    [TestMethod]
    public async Task WithinSameType_MostRecentlyModifiedIsPreferred()
    {
        var store = new InMemorySidecarStore();
        store.FolderSidecars["/photos"] = new FolderSidecar { Label = "Photos", Type = "originals", Enabled = true };

        var modTimes = new Dictionary<string, DateTime>
        {
            ["/photos/photo.jpg"]      = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ["/photos/photo_edit.jpg"] = new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc),
        };

        var step = new DuplicatesStep();
        var context = new BatchProcessingContext
        {
            FilePaths = ["/photos/photo.jpg", "/photos/photo_edit.jpg"],
            SidecarStore = store,
            GetLastModified = path => modTimes.GetValueOrDefault(path, DateTime.MinValue)
        };

        await step.ExecuteAsync(context);

        Assert.IsFalse(store.WrittenSidecars["/photos/photo.jpg"].IsPreferred);
        Assert.IsTrue(store.WrittenSidecars["/photos/photo_edit.jpg"].IsPreferred);
    }

    [TestMethod]
    public async Task WithinSameType_TieBreakAlphabetical()
    {
        // photo.jpg and photo_edit.jpg both normalize to "photo", same mod time → alphabetical tiebreak
        var store = new InMemorySidecarStore();
        store.FolderSidecars["/photos"] = new FolderSidecar { Label = "Photos", Type = "originals", Enabled = true };

        var step = new DuplicatesStep();
        var context = new BatchProcessingContext
        {
            FilePaths = ["/photos/photo_edit.jpg", "/photos/photo.jpg"],
            SidecarStore = store,
            GetLastModified = _ => DateTime.MinValue  // tie on mod time → alphabetical
        };

        await step.ExecuteAsync(context);

        // "photo.jpg" < "photo_edit.jpg" alphabetically → photo.jpg preferred
        Assert.IsTrue(store.WrittenSidecars["/photos/photo.jpg"].IsPreferred);
        Assert.IsFalse(store.WrittenSidecars["/photos/photo_edit.jpg"].IsPreferred);
    }

    // ---- Cleanup ----

    [TestMethod]
    public async Task PreviouslyGroupedFile_NowUnique_GroupIdCleared()
    {
        var store = new InMemorySidecarStore();
        // file previously had a group ID
        store.Existing["/photos/photo.jpg"] = new PhotoMetaSidecar
        {
            DuplicateGroupId = Guid.NewGuid(),
            IsPreferred = true
        };

        var step = new DuplicatesStep();
        var context = new BatchProcessingContext
        {
            FilePaths = ["/photos/photo.jpg"],   // only this file now
            SidecarStore = store
        };

        await step.ExecuteAsync(context);

        var written = store.WrittenSidecars["/photos/photo.jpg"];
        Assert.IsNull(written.DuplicateGroupId);
        Assert.IsFalse(written.IsPreferred);
    }

    // ---- Shared in-memory fake ----

    private sealed class InMemorySidecarStore : ISidecarStore
    {
        public Dictionary<string, PhotoMetaSidecar> Existing { get; } = [];
        public Dictionary<string, PhotoMetaSidecar> WrittenSidecars { get; } = [];
        public Dictionary<string, FolderSidecar> FolderSidecars { get; } = [];

        public Task<PhotoMetaSidecar?> ReadPhotoMetaAsync(string photoFilePath) =>
            Task.FromResult(Existing.GetValueOrDefault(photoFilePath));

        public Task WritePhotoMetaAsync(string photoFilePath, PhotoMetaSidecar sidecar)
        {
            WrittenSidecars[photoFilePath] = sidecar;
            Existing[photoFilePath] = sidecar;
            return Task.CompletedTask;
        }

        public Task<FolderSidecar?> ReadFolderSidecarAsync(string folderPath)
        {
            var dir = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            FolderSidecars.TryGetValue(dir, out var sidecar);
            return Task.FromResult(sidecar);
        }

        public Task WriteFolderSidecarAsync(string folderPath, FolderSidecar sidecar) => Task.CompletedTask;
    }
}

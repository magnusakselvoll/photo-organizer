using PhotoOrganizer.Domain;
using PhotoOrganizer.Infrastructure.Indexing;

namespace PhotoOrganizer.Infrastructure.Tests.Indexing;

[TestClass]
public sealed class PhotoIndexTests
{
    private static Photo MakePhoto(string path) => new()
    {
        Id = PhotoId.FromFilePath(path),
        FilePath = path,
        FileName = Path.GetFileNameWithoutExtension(path),
        FolderType = FolderType.Originals
    };

    private static SourceFolder MakeFolder(string path) => new()
    {
        Path = path,
        Label = Path.GetFileName(path),
        Type = FolderType.Originals,
        Enabled = true
    };

    // ─── Basic add/snapshot ───────────────────────────────────────────────────

    [TestMethod]
    public void AddPhoto_ThenSnapshot_ContainsPhoto()
    {
        var index = new PhotoIndex();
        var photo = MakePhoto(@"C:\photos\img001.jpg");

        index.AddPhoto(photo);

        var snapshot = index.SnapshotPhotos();
        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual(photo.Id, snapshot[0].Id);
    }

    [TestMethod]
    public void AddFolder_ThenSnapshot_ContainsFolder()
    {
        var index = new PhotoIndex();
        var folder = MakeFolder(@"C:\photos\originals");

        index.AddFolder(folder);

        var snapshot = index.SnapshotFolders();
        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual(folder.Path, snapshot[0].Path);
    }

    [TestMethod]
    public void GetById_ExistingPhoto_ReturnsPhoto()
    {
        var index = new PhotoIndex();
        var photo = MakePhoto(@"C:\photos\img001.jpg");
        index.AddPhoto(photo);

        var found = index.GetById(photo.Id);

        Assert.IsNotNull(found);
        Assert.AreEqual(photo.Id, found.Id);
    }

    [TestMethod]
    public void GetById_MissingId_ReturnsNull()
    {
        var index = new PhotoIndex();
        Assert.IsNull(index.GetById(Guid.NewGuid()));
    }

    // ─── Idempotency ──────────────────────────────────────────────────────────

    [TestMethod]
    public void AddPhoto_SameIdTwice_LastWriteWins()
    {
        var index = new PhotoIndex();
        var path = @"C:\photos\img001.jpg";
        var id = PhotoId.FromFilePath(path);

        var first = new Photo { Id = id, FilePath = path, FileName = "img001", FolderType = FolderType.Originals, IsPreferred = false };
        var second = new Photo { Id = id, FilePath = path, FileName = "img001", FolderType = FolderType.Originals, IsPreferred = true };

        index.AddPhoto(first);
        index.AddPhoto(second);

        Assert.AreEqual(1, index.Count);
        Assert.IsTrue(index.GetById(id)!.IsPreferred);
    }

    // ─── Completion flag ─────────────────────────────────────────────────────

    [TestMethod]
    public void IsComplete_InitiallyFalse()
    {
        var index = new PhotoIndex();
        Assert.IsFalse(index.IsComplete);
    }

    [TestMethod]
    public void MarkComplete_SetsIsCompleteTrue()
    {
        var index = new PhotoIndex();
        index.MarkComplete();
        Assert.IsTrue(index.IsComplete);
    }

    [TestMethod]
    public void Clear_ResetsCountAndIsComplete()
    {
        var index = new PhotoIndex();
        index.AddPhoto(MakePhoto(@"C:\photos\img001.jpg"));
        index.MarkComplete();

        index.Clear();

        Assert.AreEqual(0, index.Count);
        Assert.IsFalse(index.IsComplete);
    }

    // ─── Thread safety ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task AddPhoto_ConcurrentAdds_AllPhotosPresent()
    {
        var index = new PhotoIndex();
        const int count = 500;
        var photos = Enumerable.Range(0, count)
            .Select(i => MakePhoto($@"C:\photos\img{i:D4}.jpg"))
            .ToList();

        // Add all photos concurrently from multiple tasks
        await Task.WhenAll(photos.Select(p => Task.Run(() => index.AddPhoto(p))));

        Assert.AreEqual(count, index.Count);
    }

    [TestMethod]
    public async Task SnapshotPhotos_WhileConcurrentAdds_NeverThrows()
    {
        var index = new PhotoIndex();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Writer task
        var writer = Task.Run(async () =>
        {
            var i = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                index.AddPhoto(MakePhoto($@"C:\photos\img{i++}.jpg"));
                await Task.Yield();
            }
        });

        // Reader task
        var reader = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try { _ = index.SnapshotPhotos(); }
                catch (Exception ex) { exceptions.Add(ex); }
                await Task.Yield();
            }
        });

        await Task.WhenAll(writer, reader);
        Assert.AreEqual(0, exceptions.Count, "SnapshotPhotos should never throw under concurrent writes");
    }
}

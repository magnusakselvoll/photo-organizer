using PhotoOrganizer.Crawler.ChangeDetection;
using PhotoOrganizer.Crawler.Data;
using PhotoOrganizer.Crawler.Discovery;

namespace PhotoOrganizer.Crawler.Tests;

[TestClass]
public class ChangeDetectorTests
{
    private static readonly DateTimeOffset BaseTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ChangeDetector CreateDetector(string? hashToReturn = null)
    {
        var hasher = new StubFileHasher(hashToReturn ?? "aabbcc");
        return new ChangeDetector(hasher);
    }

    private static CrawledFileRecord ExistingRecord(string? hash = "aabbcc", DateTimeOffset? modifiedAt = null) =>
        new()
        {
            Id = 1,
            FilePath = "/photos/test.jpg",
            FileHash = hash,
            ModifiedAt = modifiedAt ?? BaseTime,
            FirstSeenAt = BaseTime,
            LastCrawledAt = BaseTime
        };

    private static DiscoveredFile DiscoveredAt(DateTimeOffset modifiedAt) =>
        new("/photos/test.jpg", modifiedAt);

    [TestMethod]
    public async Task NewFile_ReturnsNew()
    {
        var detector = CreateDetector();
        var result = await detector.DetectChangeAsync(DiscoveredAt(BaseTime), null);
        Assert.AreEqual(ChangeKind.New, result.Kind);
        Assert.IsNull(result.ComputedHash);
    }

    [TestMethod]
    public async Task SameModTime_ReturnsUnchanged()
    {
        var detector = CreateDetector();
        var result = await detector.DetectChangeAsync(DiscoveredAt(BaseTime), ExistingRecord(modifiedAt: BaseTime));
        Assert.AreEqual(ChangeKind.Unchanged, result.Kind);
    }

    [TestMethod]
    public async Task DifferentModTime_SameHash_ReturnsModTimeOnly()
    {
        var detector = CreateDetector(hashToReturn: "aabbcc");
        var newTime = BaseTime.AddMinutes(5);
        var result = await detector.DetectChangeAsync(DiscoveredAt(newTime), ExistingRecord(hash: "aabbcc", modifiedAt: BaseTime));
        Assert.AreEqual(ChangeKind.ModTimeOnly, result.Kind);
        Assert.AreEqual("aabbcc", result.ComputedHash);
    }

    [TestMethod]
    public async Task DifferentModTime_DifferentHash_ReturnsChanged()
    {
        var detector = CreateDetector(hashToReturn: "11223344");
        var newTime = BaseTime.AddMinutes(5);
        var result = await detector.DetectChangeAsync(DiscoveredAt(newTime), ExistingRecord(hash: "aabbcc", modifiedAt: BaseTime));
        Assert.AreEqual(ChangeKind.Changed, result.Kind);
        Assert.AreEqual("11223344", result.ComputedHash);
    }

    [TestMethod]
    public async Task ExistingFileWithNoHash_DifferentModTime_ReturnsChanged()
    {
        // If existing record has no hash, treat it as changed
        var detector = CreateDetector(hashToReturn: "newHash");
        var newTime = BaseTime.AddMinutes(5);
        var result = await detector.DetectChangeAsync(DiscoveredAt(newTime), ExistingRecord(hash: null, modifiedAt: BaseTime));
        Assert.AreEqual(ChangeKind.Changed, result.Kind);
    }

    /// <summary>Stub hasher that returns a fixed hash value without touching the file system.</summary>
    private sealed class StubFileHasher : FileHasher
    {
        private readonly string _hash;
        public StubFileHasher(string hash) => _hash = hash;
        public override Task<string> ComputeHashAsync(string filePath) => Task.FromResult(_hash);
    }
}

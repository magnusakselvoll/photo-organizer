using PhotoOrganizer.Crawler.Data;
using PhotoOrganizer.Crawler.Pipeline;
using PhotoOrganizer.Crawler.Sidecars;
using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.Crawler.Tests;

[TestClass]
public class PipelineRunnerTests
{
    private static CrawledFileRecord MakeRecord(int id = 1) =>
        new() { Id = id, FilePath = "/photos/test.jpg", FirstSeenAt = DateTimeOffset.UtcNow };

    [TestMethod]
    public async Task Step_RunsAndUpdatesSidecar()
    {
        var sidecarStore = new InMemorySidecarStore();
        var fileRepo = new InMemoryFileRepo();
        var step = new TrackingStep("metadata", 1);
        var registry = new StepRegistry([step]);
        var runner = new PipelineRunner(registry, sidecarStore, fileRepo);

        var record = MakeRecord();
        await runner.RunAsync("/photos/test.jpg", record);

        Assert.AreEqual(1, step.ExecutionCount);
        var sidecar = sidecarStore.WrittenSidecars["/photos/test.jpg"];
        Assert.IsTrue(sidecar.CrawlSteps.ContainsKey("metadata"));
        Assert.AreEqual(1, sidecar.CrawlSteps["metadata"].Version);
    }

    [TestMethod]
    public async Task Step_SkippedIfAlreadyAtVersion()
    {
        var sidecarStore = new InMemorySidecarStore();
        sidecarStore.Existing["/photos/test.jpg"] = new PhotoMetaSidecar
        {
            CrawlSteps = { ["metadata"] = new CrawlStepRecord { Version = 1, CompletedAt = DateTimeOffset.UtcNow } }
        };
        var fileRepo = new InMemoryFileRepo();
        var step = new TrackingStep("metadata", 1);
        var registry = new StepRegistry([step]);
        var runner = new PipelineRunner(registry, sidecarStore, fileRepo);

        await runner.RunAsync("/photos/test.jpg", MakeRecord());

        Assert.AreEqual(0, step.ExecutionCount);
    }

    [TestMethod]
    public async Task Step_RunsIfVersionIncremented()
    {
        var sidecarStore = new InMemorySidecarStore();
        sidecarStore.Existing["/photos/test.jpg"] = new PhotoMetaSidecar
        {
            CrawlSteps = { ["metadata"] = new CrawlStepRecord { Version = 1, CompletedAt = DateTimeOffset.UtcNow } }
        };
        var fileRepo = new InMemoryFileRepo();
        var step = new TrackingStep("metadata", 2); // v2 > stored v1
        var registry = new StepRegistry([step]);
        var runner = new PipelineRunner(registry, sidecarStore, fileRepo);

        await runner.RunAsync("/photos/test.jpg", MakeRecord());

        Assert.AreEqual(1, step.ExecutionCount);
    }

    [TestMethod]
    public async Task FailedStep_StopsRemainingStepsForFile()
    {
        var sidecarStore = new InMemorySidecarStore();
        var fileRepo = new InMemoryFileRepo();
        var failingStep = new FailingStep("first", 1);
        var secondStep = new TrackingStep("second", 1, dependsOn: ["first"]);
        var registry = new StepRegistry([failingStep, secondStep]);
        var runner = new PipelineRunner(registry, sidecarStore, fileRepo);

        await runner.RunAsync("/photos/test.jpg", MakeRecord());

        Assert.AreEqual(0, secondStep.ExecutionCount);
        // "failed" status recorded in DB
        Assert.IsTrue(fileRepo.StepRuns.Any(r => r.StepName == "first" && r.Status == "failed"));
    }

    [TestMethod]
    public async Task MultipleSteps_ExecuteInOrder()
    {
        var order = new List<string>();
        var sidecarStore = new InMemorySidecarStore();
        var fileRepo = new InMemoryFileRepo();
        var registry = new StepRegistry([
            new OrderTrackingStep("first", 1, [], order),
            new OrderTrackingStep("second", 1, ["first"], order)
        ]);
        var runner = new PipelineRunner(registry, sidecarStore, fileRepo);

        await runner.RunAsync("/photos/test.jpg", MakeRecord());

        CollectionAssert.AreEqual(new[] { "first", "second" }, order);
    }

    // ---- Stubs ----

    private sealed class TrackingStep(string name, int version, string[]? dependsOn = null) : IProcessingStep
    {
        public string Name => name;
        public int Version => version;
        public IReadOnlyList<string> DependsOn => dependsOn ?? [];
        public int ExecutionCount { get; private set; }
        public Task ExecuteAsync(ProcessingContext context) { ExecutionCount++; return Task.CompletedTask; }
    }

    private sealed class FailingStep(string name, int version) : IProcessingStep
    {
        public string Name => name;
        public int Version => version;
        public IReadOnlyList<string> DependsOn => [];
        public Task ExecuteAsync(ProcessingContext context) => throw new InvalidOperationException("Simulated failure");
    }

    private sealed class OrderTrackingStep(string name, int version, string[] dependsOn, List<string> order) : IProcessingStep
    {
        public string Name => name;
        public int Version => version;
        public IReadOnlyList<string> DependsOn => dependsOn;
        public Task ExecuteAsync(ProcessingContext context) { order.Add(name); return Task.CompletedTask; }
    }

    private sealed class InMemorySidecarStore : ISidecarStore
    {
        public Dictionary<string, PhotoMetaSidecar> Existing { get; } = [];
        public Dictionary<string, PhotoMetaSidecar> WrittenSidecars { get; } = [];

        public Task<PhotoMetaSidecar?> ReadPhotoMetaAsync(string photoFilePath) =>
            Task.FromResult(Existing.GetValueOrDefault(photoFilePath));

        public Task WritePhotoMetaAsync(string photoFilePath, PhotoMetaSidecar sidecar)
        {
            WrittenSidecars[photoFilePath] = sidecar;
            return Task.CompletedTask;
        }

        public Task<FolderSidecar?> ReadFolderSidecarAsync(string folderPath) => Task.FromResult<FolderSidecar?>(null);
        public Task WriteFolderSidecarAsync(string folderPath, FolderSidecar sidecar) => Task.CompletedTask;
    }

    private sealed class InMemoryFileRepo : ICrawledFileRepository
    {
        public List<(string StepName, string Status)> StepRuns { get; } = [];

        public Task<CrawledFileRecord?> GetByPathAsync(string filePath) => Task.FromResult<CrawledFileRecord?>(null);
        public Task<CrawledFileRecord> UpsertAsync(string filePath, string? fileHash, DateTimeOffset modifiedAt) =>
            Task.FromResult(new CrawledFileRecord { Id = 1, FilePath = filePath, FirstSeenAt = DateTimeOffset.UtcNow });
        public Task MarkDeletedAsync(IEnumerable<int> fileIds) => Task.CompletedTask;
        public Task UpdateModifiedAtAsync(int fileId, DateTimeOffset modifiedAt) => Task.CompletedTask;
        public Task<IReadOnlyList<CrawledFileRecord>> GetActiveFilesAsync() =>
            Task.FromResult<IReadOnlyList<CrawledFileRecord>>([]);

        public Task RecordStepRunAsync(int fileId, string stepName, int stepVersion, string status, string? errorMessage)
        {
            StepRuns.Add((stepName, status));
            return Task.CompletedTask;
        }
    }
}

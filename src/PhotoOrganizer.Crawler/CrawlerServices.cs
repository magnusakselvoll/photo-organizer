using PhotoOrganizer.Crawler.ChangeDetection;
using PhotoOrganizer.Crawler.Configuration;
using PhotoOrganizer.Crawler.Data;
using PhotoOrganizer.Crawler.Discovery;
using PhotoOrganizer.Crawler.Pipeline;
using PhotoOrganizer.Crawler.Sidecars;
using PhotoOrganizer.Crawler.Steps;

namespace PhotoOrganizer.Crawler;

/// <summary>
/// Wires up all crawler services for a single crawl session.
/// </summary>
public sealed class CrawlerServices : IDisposable
{
    public CrawlOrchestrator Orchestrator { get; }

    private CrawlerServices(CrawlOrchestrator orchestrator) => Orchestrator = orchestrator;

    public static CrawlerServices Build(CrawlerConfig config)
    {
        var db = new CrawlerDatabase(config.DatabasePath);
        db.Initialize();

        var fileRepo = new SqliteCrawledFileRepository(db);
        var logRepo = new SqliteCrawlLogRepository(db);
        var sidecarStore = new JsonSidecarStore();
        var discoverer = new FileDiscoverer();
        var hasher = new FileHasher();
        var changeDetector = new ChangeDetector(hasher);

        var registry = new StepRegistry([new MetadataStep()], [new DuplicatesStep()]);
        var pipeline = new PipelineRunner(registry, sidecarStore, fileRepo);

        var orchestrator = new CrawlOrchestrator(fileRepo, logRepo, sidecarStore, discoverer, changeDetector, pipeline, registry.BatchSteps);
        return new CrawlerServices(orchestrator);
    }

    public void Dispose() { }
}

using PhotoOrganizer.Crawler.Data;
using PhotoOrganizer.Crawler.Sidecars;
using PhotoOrganizer.Domain.Models;
using Serilog;

namespace PhotoOrganizer.Crawler.Pipeline;

public sealed class PipelineRunner
{
    private readonly StepRegistry _registry;
    private readonly ISidecarStore _sidecarStore;
    private readonly ICrawledFileRepository _fileRepo;

    public PipelineRunner(StepRegistry registry, ISidecarStore sidecarStore, ICrawledFileRepository fileRepo)
    {
        _registry = registry;
        _sidecarStore = sidecarStore;
        _fileRepo = fileRepo;
    }

    public async Task RunAsync(string filePath, CrawledFileRecord dbRecord)
    {
        var sidecar = await _sidecarStore.ReadPhotoMetaAsync(filePath)
            ?? new PhotoMetaSidecar();

        var context = new ProcessingContext
        {
            FilePath = filePath,
            Sidecar = sidecar,
            DbRecord = dbRecord
        };

        var anyStepRan = false;

        foreach (var step in _registry.Steps)
        {
            if (sidecar.CrawlSteps.TryGetValue(step.Name, out var existing) && existing.Version >= step.Version)
            {
                Log.Debug("Skipping step {StepName} v{Version} for {FilePath} (already at v{ExistingVersion})",
                    step.Name, step.Version, filePath, existing.Version);
                continue;
            }

            try
            {
                await step.ExecuteAsync(context);

                sidecar.CrawlSteps[step.Name] = new CrawlStepRecord
                {
                    Version = step.Version,
                    CompletedAt = DateTimeOffset.UtcNow
                };

                await _fileRepo.RecordStepRunAsync(dbRecord.Id, step.Name, step.Version, "completed", null);
                anyStepRan = true;

                Log.Debug("Completed step {StepName} for {FilePath}", step.Name, filePath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Step {StepName} failed for {FilePath}", step.Name, filePath);
                await _fileRepo.RecordStepRunAsync(dbRecord.Id, step.Name, step.Version, "failed", ex.Message);
                // Stop processing remaining steps for this file since later steps may depend on this one
                break;
            }
        }

        if (anyStepRan)
            await _sidecarStore.WritePhotoMetaAsync(filePath, sidecar);
    }
}

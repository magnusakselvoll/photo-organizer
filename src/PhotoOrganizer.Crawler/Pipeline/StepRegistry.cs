namespace PhotoOrganizer.Crawler.Pipeline;

public sealed class StepRegistry
{
    private readonly IReadOnlyList<IProcessingStep> _steps;
    private readonly IReadOnlyList<IBatchProcessingStep> _batchSteps;

    public StepRegistry(IEnumerable<IProcessingStep> steps, IEnumerable<IBatchProcessingStep>? batchSteps = null)
    {
        var list = steps.ToList();
        var batchList = batchSteps?.ToList() ?? [];
        ValidateDependencyOrder(list, batchList);
        _steps = list;
        _batchSteps = batchList;
    }

    public IReadOnlyList<IProcessingStep> Steps => _steps;
    public IReadOnlyList<IBatchProcessingStep> BatchSteps => _batchSteps;

    private static void ValidateDependencyOrder(List<IProcessingStep> steps, List<IBatchProcessingStep> batchSteps)
    {
        var seen = new HashSet<string>();
        foreach (var step in steps)
        {
            foreach (var dep in step.DependsOn)
            {
                if (!seen.Contains(dep))
                    throw new InvalidOperationException(
                        $"Step '{step.Name}' depends on '{dep}' which is not registered before it.");
            }
            seen.Add(step.Name);
        }
        foreach (var step in batchSteps)
        {
            foreach (var dep in step.DependsOn)
            {
                if (!seen.Contains(dep))
                    throw new InvalidOperationException(
                        $"Batch step '{step.Name}' depends on '{dep}' which is not registered before it.");
            }
            seen.Add(step.Name);
        }
    }
}

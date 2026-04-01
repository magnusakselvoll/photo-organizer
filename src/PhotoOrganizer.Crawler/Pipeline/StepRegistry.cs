namespace PhotoOrganizer.Crawler.Pipeline;

public sealed class StepRegistry
{
    private readonly IReadOnlyList<IProcessingStep> _steps;

    public StepRegistry(IEnumerable<IProcessingStep> steps)
    {
        var list = steps.ToList();
        ValidateDependencyOrder(list);
        _steps = list;
    }

    public IReadOnlyList<IProcessingStep> Steps => _steps;

    private static void ValidateDependencyOrder(List<IProcessingStep> steps)
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
    }
}

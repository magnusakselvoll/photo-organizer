namespace PhotoOrganizer.Application.Crawler;

/// <summary>
/// Allowlist validation for <see cref="StartCrawlRequest"/> fields.
/// Mode and step values mirror those defined in PhotoOrganizer.Crawler
/// (RunCommand.cs for modes; StepRegistry / IBatchProcessingStep.Name for steps).
/// Kept here (Application layer) so the Server can reference it without depending on the Crawler project.
/// </summary>
public static class StartCrawlValidation
{
    /// <summary>Valid crawler modes (case-insensitive).</summary>
    public static readonly IReadOnlySet<string> AllowedModes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "full", "incremental", "targeted" };

    /// <summary>Valid targeted-step names (case-insensitive).</summary>
    public static readonly IReadOnlySet<string> AllowedSteps =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "metadata", "duplicates" };

    /// <summary>
    /// Returns an error message if <paramref name="request"/> contains an invalid
    /// <c>Mode</c> or <c>Step</c>, or <see langword="null"/> if the request is valid.
    /// </summary>
    public static string? Validate(StartCrawlRequest request)
    {
        if (!AllowedModes.Contains(request.Mode))
            return $"Invalid mode '{request.Mode}'. Allowed values: {string.Join(", ", AllowedModes)}.";

        if (!string.IsNullOrWhiteSpace(request.Step) && !AllowedSteps.Contains(request.Step))
            return $"Invalid step '{request.Step}'. Allowed values: {string.Join(", ", AllowedSteps)}.";

        return null;
    }
}

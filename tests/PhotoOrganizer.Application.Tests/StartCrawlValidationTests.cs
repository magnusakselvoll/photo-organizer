using PhotoOrganizer.Application.Crawler;

namespace PhotoOrganizer.Application.Tests;

[TestClass]
public sealed class StartCrawlValidationTests
{
    // --- Valid inputs ---

    [TestMethod]
    [DataRow("full",        null,          DisplayName = "full mode, no step")]
    [DataRow("incremental", null,          DisplayName = "incremental mode, no step")]
    [DataRow("targeted",    "duplicates",  DisplayName = "targeted mode with duplicates step")]
    [DataRow("targeted",    "metadata",    DisplayName = "targeted mode with metadata step")]
    [DataRow("FULL",        null,          DisplayName = "mode is case-insensitive")]
    [DataRow("targeted",    "DUPLICATES",  DisplayName = "step is case-insensitive")]
    [DataRow("full",        "",            DisplayName = "empty step treated as absent")]
    [DataRow("full",        "  ",          DisplayName = "whitespace-only step treated as absent")]
    public void Validate_ValidInput_ReturnsNull(string mode, string? step)
    {
        var request = new StartCrawlRequest { Mode = mode, Step = step };
        Assert.IsNull(StartCrawlValidation.Validate(request));
    }

    // --- Invalid mode ---

    [TestMethod]
    [DataRow("evil",    DisplayName = "unknown mode")]
    [DataRow("",        DisplayName = "empty mode")]
    [DataRow("PARTIAL", DisplayName = "invented mode")]
    [DataRow("full incremental", DisplayName = "mode with embedded space (injection attempt)")]
    public void Validate_InvalidMode_ReturnsError(string mode)
    {
        var request = new StartCrawlRequest { Mode = mode };
        var error = StartCrawlValidation.Validate(request);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, mode);
    }

    // --- Invalid step ---

    [TestMethod]
    [DataRow("duplicates --config evil", DisplayName = "step with embedded arg (injection attempt)")]
    [DataRow("unknown",                  DisplayName = "unknown step name")]
    [DataRow("metadata duplicates",      DisplayName = "step with embedded space")]
    public void Validate_InvalidStep_ReturnsError(string step)
    {
        var request = new StartCrawlRequest { Mode = "targeted", Step = step };
        var error = StartCrawlValidation.Validate(request);
        Assert.IsNotNull(error);
        StringAssert.Contains(error, step);
    }

    // --- Allowlists are complete ---

    [TestMethod]
    public void AllowedModes_ContainsExpectedValues()
    {
        CollectionAssert.Contains(StartCrawlValidation.AllowedModes.ToList(), "full");
        CollectionAssert.Contains(StartCrawlValidation.AllowedModes.ToList(), "incremental");
        CollectionAssert.Contains(StartCrawlValidation.AllowedModes.ToList(), "targeted");
    }

    [TestMethod]
    public void AllowedSteps_ContainsExpectedValues()
    {
        CollectionAssert.Contains(StartCrawlValidation.AllowedSteps.ToList(), "metadata");
        CollectionAssert.Contains(StartCrawlValidation.AllowedSteps.ToList(), "duplicates");
    }
}

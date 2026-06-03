using PhotoOrganizer.Application.Crawler;
using PhotoOrganizer.Infrastructure.Crawler;

namespace PhotoOrganizer.Infrastructure.Tests;

/// <summary>
/// Verifies that BuildArgs produces one token per logical argument (no re-tokenization).
/// Uses InternalsVisibleTo so CrawlerService.BuildArgs can be called directly without
/// going through ProcessStartInfo.
/// </summary>
[TestClass]
public sealed class CrawlerServiceBuildArgsTests
{
    [TestMethod]
    public void BuildArgs_IncrementalMode_NoStep_NoConfig_ProducesExpectedTokens()
    {
        var request = new StartCrawlRequest { Mode = "incremental" };
        var tokens = CrawlerService.BuildArgs(request, configPath: null);

        CollectionAssert.AreEqual(new[] { "run", "--mode", "incremental" }, tokens.ToList());
    }

    [TestMethod]
    public void BuildArgs_FullMode_WithStep_ProducesStepTokens()
    {
        var request = new StartCrawlRequest { Mode = "full", Step = "duplicates" };
        var tokens = CrawlerService.BuildArgs(request, configPath: null);

        CollectionAssert.AreEqual(new[] { "run", "--mode", "full", "--step", "duplicates" }, tokens.ToList());
    }

    [TestMethod]
    public void BuildArgs_WithConfigPath_AppendsConfigTokens()
    {
        var request = new StartCrawlRequest { Mode = "incremental" };
        var tokens = CrawlerService.BuildArgs(request, configPath: "/path/to/config.json");

        CollectionAssert.AreEqual(
            new[] { "run", "--mode", "incremental", "--config", "/path/to/config.json" },
            tokens.ToList());
    }

    [TestMethod]
    public void BuildArgs_StepContainingSpace_RemainsOneSingleToken()
    {
        // Defense-in-depth: even if validation is bypassed, ArgumentList prevents re-tokenization.
        // A space-containing value must not be split into multiple tokens.
        var request = new StartCrawlRequest { Mode = "targeted", Step = "duplicates --config evil" };
        var tokens = CrawlerService.BuildArgs(request, configPath: null);

        // Tokens: run, --mode, targeted, --step, "duplicates --config evil" (one token, not split)
        Assert.AreEqual(5, tokens.Count,
            $"Expected 5 tokens but got {tokens.Count}: [{string.Join(", ", tokens)}]");
        Assert.AreEqual("duplicates --config evil", tokens[4],
            "The step value must be a single token regardless of embedded spaces.");
    }

    [TestMethod]
    public void BuildArgs_NullOrWhitespaceStep_IsOmitted()
    {
        var requestNull = new StartCrawlRequest { Mode = "incremental", Step = null };
        var requestEmpty = new StartCrawlRequest { Mode = "incremental", Step = "   " };

        var tokensNull = CrawlerService.BuildArgs(requestNull, configPath: null);
        var tokensEmpty = CrawlerService.BuildArgs(requestEmpty, configPath: null);

        CollectionAssert.AreEqual(new[] { "run", "--mode", "incremental" }, tokensNull.ToList());
        CollectionAssert.AreEqual(new[] { "run", "--mode", "incremental" }, tokensEmpty.ToList());
    }

    [TestMethod]
    public void BuildArgs_NullOrWhitespaceConfig_IsOmitted()
    {
        var request = new StartCrawlRequest { Mode = "incremental" };

        var tokensNull = CrawlerService.BuildArgs(request, configPath: null);
        var tokensEmpty = CrawlerService.BuildArgs(request, configPath: "  ");

        CollectionAssert.AreEqual(new[] { "run", "--mode", "incremental" }, tokensNull.ToList());
        CollectionAssert.AreEqual(new[] { "run", "--mode", "incremental" }, tokensEmpty.ToList());
    }
}

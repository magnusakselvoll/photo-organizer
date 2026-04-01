using PhotoOrganizer.Crawler.Pipeline;

namespace PhotoOrganizer.Crawler.Tests;

[TestClass]
public class StepRegistryTests
{
    [TestMethod]
    public void ValidOrder_NoException()
    {
        var registry = new StepRegistry([
            new StubStep("a", []),
            new StubStep("b", ["a"])
        ]);
        Assert.AreEqual(2, registry.Steps.Count);
    }

    [TestMethod]
    public void InvalidOrder_ThrowsInvalidOperationException()
    {
        try
        {
            _ = new StepRegistry([
                new StubStep("b", ["a"]),  // "a" not registered before "b"
                new StubStep("a", [])
            ]);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException) { }
    }

    [TestMethod]
    public void StepsAreInRegistrationOrder()
    {
        var registry = new StepRegistry([
            new StubStep("first", []),
            new StubStep("second", ["first"]),
            new StubStep("third", ["second"])
        ]);
        var names = registry.Steps.Select(s => s.Name).ToList();
        CollectionAssert.AreEqual(new[] { "first", "second", "third" }, names);
    }

    private sealed class StubStep(string name, string[] dependsOn) : IProcessingStep
    {
        public string Name => name;
        public int Version => 1;
        public IReadOnlyList<string> DependsOn => dependsOn;
        public Task ExecuteAsync(ProcessingContext context) => Task.CompletedTask;
    }
}

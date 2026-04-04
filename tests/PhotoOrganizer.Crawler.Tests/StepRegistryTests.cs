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

    [TestMethod]
    public void BatchStep_DependsOnPerFileStep_Valid()
    {
        var registry = new StepRegistry(
            [new StubStep("metadata", [])],
            [new StubBatchStep("duplicates", ["metadata"])]);
        Assert.AreEqual(1, registry.Steps.Count);
        Assert.AreEqual(1, registry.BatchSteps.Count);
    }

    [TestMethod]
    public void BatchStep_DependsOnUnregisteredStep_ThrowsInvalidOperationException()
    {
        try
        {
            _ = new StepRegistry(
                [],
                [new StubBatchStep("duplicates", ["metadata"])]);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException) { }
    }

    [TestMethod]
    public void NoBatchSteps_BatchStepsIsEmpty()
    {
        var registry = new StepRegistry([new StubStep("metadata", [])]);
        Assert.AreEqual(0, registry.BatchSteps.Count);
    }

    private sealed class StubStep(string name, string[] dependsOn) : IProcessingStep
    {
        public string Name => name;
        public int Version => 1;
        public IReadOnlyList<string> DependsOn => dependsOn;
        public Task ExecuteAsync(ProcessingContext context) => Task.CompletedTask;
    }

    private sealed class StubBatchStep(string name, string[] dependsOn) : IBatchProcessingStep
    {
        public string Name => name;
        public int Version => 1;
        public IReadOnlyList<string> DependsOn => dependsOn;
        public Task ExecuteAsync(BatchProcessingContext context) => Task.CompletedTask;
    }
}

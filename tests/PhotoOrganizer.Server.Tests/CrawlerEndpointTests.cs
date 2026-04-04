using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PhotoOrganizer.Application.Crawler;

namespace PhotoOrganizer.Server.Tests;

[TestClass]
public class CrawlerEndpointTests
{
    private static WebApplicationFactory<Program> CreateFactory(ICrawlerService crawlerService)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real CrawlerService with a fake
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICrawlerService));
                if (descriptor is not null)
                    services.Remove(descriptor);
                services.AddSingleton(crawlerService);
            });
        });
    }

    [TestMethod]
    public async Task PostCrawlerStart_WhenNotRunning_Returns202()
    {
        var fake = new FakeCrawlerService { StartResult = true };
        await using var factory = CreateFactory(fake);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/crawler/start", new StartCrawlRequest { Mode = "incremental" });

        Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
    }

    [TestMethod]
    public async Task PostCrawlerStart_WhenAlreadyRunning_Returns409()
    {
        var fake = new FakeCrawlerService { StartResult = false };
        await using var factory = CreateFactory(fake);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/crawler/start", new StartCrawlRequest { Mode = "incremental" });

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
    }

    [TestMethod]
    public async Task GetCrawlerStatus_ReturnsStatusDto()
    {
        var fake = new FakeCrawlerService
        {
            Status = new CrawlerStatusDto { Status = "running", FilesScanned = 42 }
        };
        await using var factory = CreateFactory(fake);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crawler/status");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<CrawlerStatusDto>();
        Assert.IsNotNull(dto);
        Assert.AreEqual("running", dto.Status);
        Assert.AreEqual(42, dto.FilesScanned);
    }

    private sealed class FakeCrawlerService : ICrawlerService
    {
        public bool StartResult { get; set; } = true;
        public CrawlerStatusDto Status { get; set; } = new CrawlerStatusDto { Status = "idle" };

        public Task<CrawlerStatusDto> GetStatusAsync() => Task.FromResult(Status);
        public Task<bool> StartCrawlAsync(StartCrawlRequest request) => Task.FromResult(StartResult);
    }
}

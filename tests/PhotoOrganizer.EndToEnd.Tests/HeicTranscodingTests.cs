using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PhotoOrganizer.Application;
using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Crawler;
using PhotoOrganizer.Crawler.Configuration;

namespace PhotoOrganizer.EndToEnd.Tests;

/// <summary>
/// Integration tests verifying that HEIC images are transcoded to JPEG by the server
/// so they can be displayed in browsers that don't natively support HEIC (Chrome, Firefox).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class HeicTranscodingTests
{
    private static string _tempDir = "";
    private static string _photosRoot = "";
    private static string _dbPath = "";

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"photo-heic-{Guid.NewGuid():N}");
        _photosRoot = Path.Combine(_tempDir, "heic");
        _dbPath = Path.Combine(_tempDir, "crawler.db");

        // Copy the committed HEIC fixture to a temp working copy and run the crawler.
        var fixturesSource = Path.Combine(AppContext.BaseDirectory, "fixtures", "heic");
        CopyDirectory(fixturesSource, _photosRoot);

        var config = new CrawlerConfig { DatabasePath = _dbPath };
        using var services = CrawlerServices.Build(config);
        await services.Orchestrator.RunAsync([_photosRoot], fullMode: true);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestMethod]
    public async Task Server_GetPhotoImage_HeicIsTranscodedToJpeg()
    {
        await using var factory = CreateServerFactory();
        var client = factory.CreateClient();
        await WaitForIndexAsync(client);

        // Find the HEIC photo via the API.
        var listResponse = await client.GetAsync("/api/photos?deduplicated=false&pageSize=10");
        Assert.AreEqual(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PhotoPageDto>();
        Assert.IsNotNull(page);
        Assert.AreEqual(1, page.TotalCount, "Expected exactly one photo (the HEIC fixture)");

        var id = page.Items[0].Id;
        var imageResponse = await client.GetAsync($"/api/photos/{id}/image");

        Assert.AreEqual(HttpStatusCode.OK, imageResponse.StatusCode);
        Assert.AreEqual("image/jpeg", imageResponse.Content.Headers.ContentType?.MediaType,
            "HEIC should be transcoded to JPEG so all browsers can display it");

        // Verify the response body is a valid JPEG (starts with the JPEG magic bytes FF D8 FF).
        var bytes = await imageResponse.Content.ReadAsByteArrayAsync();
        Assert.IsTrue(bytes.Length >= 3, "Image response body is too short");
        Assert.AreEqual(0xFF, bytes[0], "Expected JPEG magic byte 0 (0xFF)");
        Assert.AreEqual(0xD8, bytes[1], "Expected JPEG magic byte 1 (0xD8)");
        Assert.AreEqual(0xFF, bytes[2], "Expected JPEG magic byte 2 (0xFF)");
    }

    private WebApplicationFactory<Program> CreateServerFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<PhotoOrganizerSettings>(opts =>
                {
                    opts.ScanRoots = [_photosRoot];
                    opts.Indexing.CacheDirectory = Path.Combine(_tempDir, "server-index-cache");
                    opts.Indexing.CacheWriteIntervalPhotos = 0;
                });
            });
        });

    private static async Task WaitForIndexAsync(HttpClient client, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (DateTime.UtcNow < deadline)
        {
            var resp = await client.GetAsync("/api/index/status");
            resp.EnsureSuccessStatusCode();
            var doc = await resp.Content.ReadFromJsonAsync<IndexStatusDto>();
            if (doc?.Complete == true)
                return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Photo index did not complete within the timeout.");
    }

    private sealed class IndexStatusDto
    {
        public bool Complete { get; set; }
        public int Count { get; set; }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var subDir in Directory.EnumerateDirectories(source))
        {
            var destSub = Path.Combine(destination, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSub);
        }
    }
}

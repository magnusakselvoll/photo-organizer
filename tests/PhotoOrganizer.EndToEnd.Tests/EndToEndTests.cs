using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PhotoOrganizer.Application;
using PhotoOrganizer.Application.Photos;
using PhotoOrganizer.Crawler;
using PhotoOrganizer.Crawler.Configuration;
using PhotoOrganizer.Domain.Models;

namespace PhotoOrganizer.EndToEnd.Tests;

[TestClass]
[TestCategory("Integration")]
public class EndToEndTests
{
    private static string _tempDir = "";
    private static string _photosRoot = "";
    private static string _originalsDir = "";
    private static string _editsDir = "";
    private static string _dbPath = "";

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"photo-e2e-{Guid.NewGuid():N}");
        _photosRoot = Path.Combine(_tempDir, "photos");
        _originalsDir = Path.Combine(_photosRoot, "originals");
        _editsDir = Path.Combine(_photosRoot, "edits");
        _dbPath = Path.Combine(_tempDir, "crawler.db");

        var fixturesSource = Path.Combine(AppContext.BaseDirectory, "fixtures", "photos");
        CopyDirectory(fixturesSource, _photosRoot);

        var config = new CrawlerConfig { DatabasePath = _dbPath };
        using var services = CrawlerServices.Build(config);
        // Pass only the library root — recursive _folder.json discovery finds originals/ and edits/
        await services.Orchestrator.RunAsync([_photosRoot], fullMode: true);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ─── Crawler assertions ───────────────────────────────────────────────────

    [TestMethod]
    public void Crawler_WritesMetaJsonSidecarsForAllPhotos()
    {
        var jpgs = Directory.GetFiles(_originalsDir, "*.jpg")
            .Concat(Directory.GetFiles(_editsDir, "*.jpg"))
            .ToList();

        Assert.AreEqual(15, jpgs.Count);

        foreach (var jpg in jpgs)
        {
            var sidecarPath = Path.ChangeExtension(jpg, null) + ".meta.json";
            Assert.IsTrue(File.Exists(sidecarPath), $"Missing sidecar for {Path.GetFileName(jpg)}");
        }
    }

    [TestMethod]
    public async Task Crawler_SidecarsCapturedAtIsPopulated()
    {
        var sidecars = await ReadAllSidecarsAsync();
        Assert.AreEqual(15, sidecars.Count);
        foreach (var (path, sidecar) in sidecars)
            Assert.IsNotNull(sidecar.CapturedAt, $"capturedAt is null in {Path.GetFileName(path)}");
    }

    [TestMethod]
    public async Task Crawler_IdentifiesThreeDuplicateGroups()
    {
        var sidecars = await ReadAllSidecarsAsync();

        var groups = sidecars
            .Where(kv => kv.sidecar.DuplicateGroupId is not null)
            .GroupBy(kv => kv.sidecar.DuplicateGroupId!.Value)
            .ToList();

        Assert.AreEqual(3, groups.Count, "Expected 3 duplicate groups");

        foreach (var group in groups)
        {
            var preferred = group.Where(kv => kv.sidecar.IsPreferred).ToList();
            Assert.AreEqual(1, preferred.Count,
                $"Group {group.Key} should have exactly one preferred photo, got {preferred.Count}");
        }
    }

    [TestMethod]
    public async Task Crawler_PreferredPhotosAreInEditsFolder()
    {
        var sidecars = await ReadAllSidecarsAsync();

        var preferred = sidecars
            .Where(kv => kv.sidecar.IsPreferred)
            .ToList();

        Assert.AreEqual(3, preferred.Count);
        foreach (var (path, _) in preferred)
            Assert.IsTrue(path.StartsWith(_editsDir, StringComparison.OrdinalIgnoreCase),
                $"Preferred photo {Path.GetFileName(path)} should be in edits folder");
    }

    [TestMethod]
    public async Task Crawler_CrawlLogRecordIsCompleted()
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT status, files_processed FROM crawl_log";
        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.IsTrue(await reader.ReadAsync(), "Expected at least one crawl_log row");
        Assert.AreEqual("completed", reader.GetString(0), "crawl status should be 'completed'");
        Assert.AreEqual(15, reader.GetInt32(1), "files_processed should be 15");
        Assert.IsFalse(await reader.ReadAsync(), "Expected exactly one crawl_log row");
    }

    // ─── Server assertions ────────────────────────────────────────────────────

    [TestMethod]
    public async Task Server_GetPhotos_AllPhotos_Returns15()
    {
        await using var factory = CreateServerFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/photos?deduplicated=false");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PhotoPageDto>();
        Assert.IsNotNull(page);
        Assert.AreEqual(15, page.TotalCount);
    }

    [TestMethod]
    public async Task Server_GetPhotos_Deduplicated_Returns12()
    {
        await using var factory = CreateServerFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/photos?deduplicated=true");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PhotoPageDto>();
        Assert.IsNotNull(page);
        Assert.AreEqual(12, page.TotalCount);
    }

    [TestMethod]
    public async Task Server_GetPhotoImage_ReturnsJpegContent()
    {
        await using var factory = CreateServerFactory();
        var client = factory.CreateClient();

        // Get a deduplicated photo to find a valid ID
        var listResponse = await client.GetAsync("/api/photos?deduplicated=true&pageSize=1");
        var page = await listResponse.Content.ReadFromJsonAsync<PhotoPageDto>();
        Assert.IsNotNull(page);
        Assert.IsTrue(page.Items.Count > 0);
        var id = page.Items[0].Id;

        var response = await client.GetAsync($"/api/photos/{id}/image");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task Server_GetSlideshowNext_ReturnsPreferredPhoto()
    {
        await using var factory = CreateServerFactory();
        var client = factory.CreateClient();

        // Build set of deduplicated IDs
        var listResponse = await client.GetAsync("/api/photos?deduplicated=true&pageSize=100");
        var page = await listResponse.Content.ReadFromJsonAsync<PhotoPageDto>();
        Assert.IsNotNull(page);
        var deduplicatedIds = page.Items.Select(p => p.Id).ToHashSet();

        var response = await client.GetAsync("/api/slideshow/next");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var photo = await response.Content.ReadFromJsonAsync<PhotoDto>();
        Assert.IsNotNull(photo);
        Assert.IsTrue(deduplicatedIds.Contains(photo.Id),
            $"Slideshow returned photo {photo.Id} which is not in the deduplicated set");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private WebApplicationFactory<Program> CreateServerFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<PhotoOrganizerSettings>(opts =>
                {
                    opts.ScanRoots = [_photosRoot];
                });
            });
        });

    private static async Task<List<(string path, PhotoMetaSidecar sidecar)>> ReadAllSidecarsAsync()
    {
        var result = new List<(string, PhotoMetaSidecar)>();
        var sidecarOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        foreach (var dir in new[] { _originalsDir, _editsDir })
        {
            foreach (var metaFile in Directory.GetFiles(dir, "*.meta.json"))
            {
                await using var stream = File.OpenRead(metaFile);
                var sidecar = await JsonSerializer.DeserializeAsync<PhotoMetaSidecar>(stream, sidecarOptions);
                Assert.IsNotNull(sidecar, $"Could not deserialize {metaFile}");
                result.Add((metaFile, sidecar));
            }
        }

        return result;
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

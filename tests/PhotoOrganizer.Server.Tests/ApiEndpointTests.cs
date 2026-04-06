using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PhotoOrganizer.Application.Folders;
using PhotoOrganizer.Application.Photos;

namespace PhotoOrganizer.Server.Tests;

[TestClass]
[TestCategory("Integration")]
public class ApiEndpointTests
{
    private static readonly FolderDto SampleFolder = new()
    {
        Path = "/photos",
        Label = "Photos",
        Type = "Originals",
        Enabled = true
    };

    private static readonly PhotoDto SamplePhoto = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        FilePath = "/photos/test.jpg",
        FileName = "test",
        FolderType = "Originals",
        IsPreferred = true
    };

    private static WebApplicationFactory<Program> CreateFactory(
        IFolderService? folderService = null,
        IPhotoService? photoService = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                if (folderService is not null)
                    Replace<IFolderService>(services, folderService);

                if (photoService is not null)
                    Replace<IPhotoService>(services, photoService);
            });
        });
    }

    private static void Replace<T>(IServiceCollection services, T implementation) where T : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor is not null)
            services.Remove(descriptor);
        services.AddSingleton(implementation);
    }

    // --- /api/folders ---

    [TestMethod]
    public async Task GetFolders_ReturnsOkWithFolders()
    {
        var fake = new FakeFolderService { Folders = [SampleFolder] };
        await using var factory = CreateFactory(folderService: fake);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/folders");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var folders = await response.Content.ReadFromJsonAsync<FolderDto[]>();
        Assert.IsNotNull(folders);
        Assert.AreEqual(1, folders.Length);
        Assert.AreEqual("/photos", folders[0].Path);
    }

    // --- /api/photos ---

    [TestMethod]
    public async Task GetPhotos_ReturnsOkWithPage()
    {
        var page = new PhotoPageDto { Items = [SamplePhoto], TotalCount = 1, Page = 1, PageSize = 50 };
        var fake = new FakePhotoService { Page = page };
        await using var factory = CreateFactory(photoService: fake);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/photos");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PhotoPageDto>();
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.TotalCount);
    }

    [TestMethod]
    public async Task GetPhotos_PassesFilterParameters()
    {
        var fake = new FakePhotoService { Page = new PhotoPageDto { Items = [], TotalCount = 0, Page = 2, PageSize = 10 } };
        await using var factory = CreateFactory(photoService: fake);
        var client = factory.CreateClient();

        await client.GetAsync("/api/photos?folder=/photos&type=originals&deduplicated=false&page=2&pageSize=10");

        Assert.IsNotNull(fake.LastFilter);
        Assert.AreEqual("/photos", fake.LastFilter.Folder);
        Assert.AreEqual("originals", fake.LastFilter.Type);
        Assert.IsFalse(fake.LastFilter.Deduplicated);
        Assert.AreEqual(2, fake.LastFilter.Page);
        Assert.AreEqual(10, fake.LastFilter.PageSize);
    }

    // --- /api/photos/{id} ---

    [TestMethod]
    public async Task GetPhotoById_WhenFound_ReturnsOk()
    {
        var fake = new FakePhotoService { Photo = SamplePhoto };
        await using var factory = CreateFactory(photoService: fake);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/photos/{SamplePhoto.Id}");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PhotoDto>();
        Assert.IsNotNull(dto);
        Assert.AreEqual(SamplePhoto.Id, dto.Id);
    }

    [TestMethod]
    public async Task GetPhotoById_WhenNotFound_Returns404()
    {
        var fake = new FakePhotoService { Photo = null };
        await using var factory = CreateFactory(photoService: fake);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/photos/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- /api/slideshow/next ---

    [TestMethod]
    public async Task GetSlideshowNext_WhenPhotosExist_ReturnsOk()
    {
        var page = new PhotoPageDto { Items = [SamplePhoto], TotalCount = 1, Page = 1, PageSize = int.MaxValue };
        var fake = new FakePhotoService { Page = page };
        await using var factory = CreateFactory(photoService: fake);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/slideshow/next");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PhotoDto>();
        Assert.IsNotNull(dto);
    }

    [TestMethod]
    public async Task GetSlideshowNext_WhenNoPhotos_Returns404()
    {
        var page = new PhotoPageDto { Items = [], TotalCount = 0, Page = 1, PageSize = int.MaxValue };
        var fake = new FakePhotoService { Page = page };
        await using var factory = CreateFactory(photoService: fake);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/slideshow/next");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- /api/config ---

    [TestMethod]
    public async Task GetConfig_ReturnsOk()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/config");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    // --- Fake implementations ---

    private sealed class FakeFolderService : IFolderService
    {
        public IReadOnlyList<FolderDto> Folders { get; set; } = [];
        public Task<IReadOnlyList<FolderDto>> GetAllFoldersAsync() => Task.FromResult(Folders);
    }

    private sealed class FakePhotoService : IPhotoService
    {
        public PhotoPageDto Page { get; set; } = new() { Items = [], TotalCount = 0, Page = 1, PageSize = 50 };
        public PhotoDto? Photo { get; set; }
        public PhotoFilter? LastFilter { get; private set; }

        public Task<PhotoPageDto> GetPhotosAsync(PhotoFilter filter)
        {
            LastFilter = filter;
            return Task.FromResult(Page);
        }

        public Task<PhotoDto?> GetPhotoByIdAsync(Guid id) => Task.FromResult(Photo);
    }
}

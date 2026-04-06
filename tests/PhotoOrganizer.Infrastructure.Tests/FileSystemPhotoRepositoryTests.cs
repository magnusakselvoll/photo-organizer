using Microsoft.Extensions.Options;
using PhotoOrganizer.Application;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Domain.Interfaces;
using PhotoOrganizer.Infrastructure.Sidecars;
using PhotoOrganizer.Infrastructure.Storage;

namespace PhotoOrganizer.Infrastructure.Tests;

[TestClass]
public sealed class FileSystemPhotoRepositoryTests
{
    private DirectoryInfo _tempDir = null!;
    private SidecarReader _sidecarReader = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Directory.CreateTempSubdirectory("PhotoRepoTests_");
        _sidecarReader = new SidecarReader();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _tempDir.Delete(recursive: true);
    }

    private IFolderRepository CreateFolderRepository(params string[] scanRoots)
    {
        var settings = Options.Create(new PhotoOrganizerSettings { ScanRoots = scanRoots });
        return new FileSystemFolderRepository(settings, _sidecarReader);
    }

    private FileSystemPhotoRepository CreatePhotoRepository(params string[] scanRoots)
    {
        return new FileSystemPhotoRepository(CreateFolderRepository(scanRoots), _sidecarReader);
    }

    private static async Task WriteFolderSidecar(string folderPath, string type = "mixed", bool enabled = true)
    {
        await File.WriteAllTextAsync(Path.Combine(folderPath, "_folder.json"),
            $$"""{ "version": 1, "label": "Test", "type": "{{type}}", "enabled": {{(enabled ? "true" : "false")}} }""");
    }

    private static void WritePhotoFile(string folderPath, string fileName)
    {
        File.WriteAllBytes(Path.Combine(folderPath, fileName), []);
    }

    [TestMethod]
    public async Task GetAllPhotos_DiscoversPhotosInEnabledFolders()
    {
        await WriteFolderSidecar(_tempDir.FullName);
        WritePhotoFile(_tempDir.FullName, "photo1.jpg");
        WritePhotoFile(_tempDir.FullName, "photo2.jpeg");

        var repo = CreatePhotoRepository(_tempDir.FullName);
        var photos = await repo.GetAllPhotosAsync();

        Assert.AreEqual(2, photos.Count);
    }

    [TestMethod]
    public async Task GetAllPhotos_SkipsDisabledFolders()
    {
        await WriteFolderSidecar(_tempDir.FullName, enabled: false);
        WritePhotoFile(_tempDir.FullName, "photo.jpg");

        var repo = CreatePhotoRepository(_tempDir.FullName);
        var photos = await repo.GetAllPhotosAsync();

        Assert.AreEqual(0, photos.Count);
    }

    [TestMethod]
    public async Task GetAllPhotos_SkipsNonPhotoFiles()
    {
        await WriteFolderSidecar(_tempDir.FullName);
        WritePhotoFile(_tempDir.FullName, "photo.jpg");
        File.WriteAllText(Path.Combine(_tempDir.FullName, "readme.txt"), "not a photo");

        var repo = CreatePhotoRepository(_tempDir.FullName);
        var photos = await repo.GetAllPhotosAsync();

        Assert.AreEqual(1, photos.Count);
    }

    [TestMethod]
    public async Task GetAllPhotos_MissingSidecar_UsesDefaults()
    {
        await WriteFolderSidecar(_tempDir.FullName);
        WritePhotoFile(_tempDir.FullName, "photo.jpg");

        var repo = CreatePhotoRepository(_tempDir.FullName);
        var photos = await repo.GetAllPhotosAsync();

        Assert.AreEqual(1, photos.Count);
        var photo = photos[0];
        Assert.IsNull(photo.CapturedAt);
        Assert.IsNull(photo.DuplicateGroupId);
        Assert.IsFalse(photo.IsPreferred);
        Assert.AreEqual(0, photo.Tags.Count);
    }

    [TestMethod]
    public async Task GetAllPhotos_DeterministicId_StableAcrossRepeatCalls()
    {
        await WriteFolderSidecar(_tempDir.FullName);
        WritePhotoFile(_tempDir.FullName, "photo.jpg");

        var repo = CreatePhotoRepository(_tempDir.FullName);
        var photos1 = await repo.GetAllPhotosAsync();

        await repo.InvalidateCacheAsync();
        var photos2 = await repo.GetAllPhotosAsync();

        Assert.AreEqual(photos1[0].Id, photos2[0].Id);
    }

    [TestMethod]
    public async Task GetAllPhotos_SetsFileNameWithoutExtension()
    {
        await WriteFolderSidecar(_tempDir.FullName);
        WritePhotoFile(_tempDir.FullName, "IMG_1234.jpg");

        var repo = CreatePhotoRepository(_tempDir.FullName);
        var photos = await repo.GetAllPhotosAsync();

        Assert.AreEqual("IMG_1234", photos[0].FileName);
    }

    [TestMethod]
    public async Task GetAllPhotos_SetsFolderTypeFromFolder()
    {
        await WriteFolderSidecar(_tempDir.FullName, type: "edits");
        WritePhotoFile(_tempDir.FullName, "photo.jpg");

        var repo = CreatePhotoRepository(_tempDir.FullName);
        var photos = await repo.GetAllPhotosAsync();

        Assert.AreEqual(FolderType.Edits, photos[0].FolderType);
    }

    [TestMethod]
    public async Task GetById_ReturnsCorrectPhoto()
    {
        await WriteFolderSidecar(_tempDir.FullName);
        WritePhotoFile(_tempDir.FullName, "photo.jpg");

        var repo = CreatePhotoRepository(_tempDir.FullName);
        var photos = await repo.GetAllPhotosAsync();
        var id = photos[0].Id;

        var result = await repo.GetByIdAsync(id);

        Assert.IsNotNull(result);
        Assert.AreEqual(id, result.Id);
    }

    [TestMethod]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        await WriteFolderSidecar(_tempDir.FullName);

        var repo = CreatePhotoRepository(_tempDir.FullName);
        var result = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task InvalidateCache_ForcesReload()
    {
        await WriteFolderSidecar(_tempDir.FullName);
        WritePhotoFile(_tempDir.FullName, "photo.jpg");

        var repo = CreatePhotoRepository(_tempDir.FullName);
        var before = await repo.GetAllPhotosAsync();

        // Add a new photo after initial load
        WritePhotoFile(_tempDir.FullName, "photo2.jpg");
        await repo.InvalidateCacheAsync();

        var after = await repo.GetAllPhotosAsync();

        Assert.AreEqual(1, before.Count);
        Assert.AreEqual(2, after.Count);
    }
}

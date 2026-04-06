using Microsoft.Extensions.Options;
using PhotoOrganizer.Application;
using PhotoOrganizer.Domain;
using PhotoOrganizer.Infrastructure.Sidecars;
using PhotoOrganizer.Infrastructure.Storage;

namespace PhotoOrganizer.Infrastructure.Tests;

[TestClass]
public sealed class FileSystemFolderRepositoryTests
{
    private DirectoryInfo _tempDir = null!;
    private SidecarReader _sidecarReader = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Directory.CreateTempSubdirectory("FolderRepoTests_");
        _sidecarReader = new SidecarReader();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _tempDir.Delete(recursive: true);
    }

    private FileSystemFolderRepository CreateRepository(params string[] scanRoots)
    {
        var settings = Options.Create(new PhotoOrganizerSettings { ScanRoots = scanRoots });
        return new FileSystemFolderRepository(settings, _sidecarReader);
    }

    private static async Task WriteFolderSidecar(string folderPath, string label, string type = "mixed", bool enabled = true)
    {
        await File.WriteAllTextAsync(Path.Combine(folderPath, "_folder.json"), $$"""
            { "version": 1, "label": "{{label}}", "type": "{{type}}", "enabled": {{(enabled ? "true" : "false")}} }
            """);
    }

    [TestMethod]
    public async Task GetAllFolders_DiscoversFoldersInScanRoots()
    {
        await WriteFolderSidecar(_tempDir.FullName, "Root Folder");

        var repo = CreateRepository(_tempDir.FullName);
        var folders = await repo.GetAllFoldersAsync();

        Assert.AreEqual(1, folders.Count);
        Assert.AreEqual("Root Folder", folders[0].Label);
    }

    [TestMethod]
    public async Task GetAllFolders_DiscoversNestedFolders()
    {
        var subDir = _tempDir.CreateSubdirectory("sub");
        await WriteFolderSidecar(_tempDir.FullName, "Parent");
        await WriteFolderSidecar(subDir.FullName, "Child");

        var repo = CreateRepository(_tempDir.FullName);
        var folders = await repo.GetAllFoldersAsync();

        Assert.AreEqual(2, folders.Count);
    }

    [TestMethod]
    public async Task GetAllFolders_IgnoresDirectoriesWithoutFolderJson()
    {
        _tempDir.CreateSubdirectory("no-sidecar");
        await WriteFolderSidecar(_tempDir.FullName, "Has Sidecar");

        var repo = CreateRepository(_tempDir.FullName);
        var folders = await repo.GetAllFoldersAsync();

        Assert.AreEqual(1, folders.Count);
    }

    [TestMethod]
    public async Task GetAllFolders_ParsesFolderType_Originals()
    {
        await WriteFolderSidecar(_tempDir.FullName, "Originals Folder", type: "originals");

        var repo = CreateRepository(_tempDir.FullName);
        var folders = await repo.GetAllFoldersAsync();

        Assert.AreEqual(FolderType.Originals, folders[0].Type);
    }

    [TestMethod]
    public async Task GetAllFolders_ParsesFolderType_Edits()
    {
        await WriteFolderSidecar(_tempDir.FullName, "Edits Folder", type: "edits");

        var repo = CreateRepository(_tempDir.FullName);
        var folders = await repo.GetAllFoldersAsync();

        Assert.AreEqual(FolderType.Edits, folders[0].Type);
    }

    [TestMethod]
    public async Task GetAllFolders_ParsesFolderType_Mixed()
    {
        await WriteFolderSidecar(_tempDir.FullName, "Mixed Folder", type: "mixed");

        var repo = CreateRepository(_tempDir.FullName);
        var folders = await repo.GetAllFoldersAsync();

        Assert.AreEqual(FolderType.Mixed, folders[0].Type);
    }

    [TestMethod]
    public async Task GetAllFolders_CachesResults_SecondCallReturnsSameList()
    {
        await WriteFolderSidecar(_tempDir.FullName, "Cached Folder");

        var repo = CreateRepository(_tempDir.FullName);
        var first = await repo.GetAllFoldersAsync();
        var second = await repo.GetAllFoldersAsync();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public async Task GetAllFolders_NonExistentScanRoot_ReturnsEmpty()
    {
        var repo = CreateRepository("/this/path/does/not/exist");
        var folders = await repo.GetAllFoldersAsync();

        Assert.AreEqual(0, folders.Count);
    }

    [TestMethod]
    public async Task GetFolderByPath_ExistingFolder_ReturnsFolder()
    {
        await WriteFolderSidecar(_tempDir.FullName, "My Folder");

        var repo = CreateRepository(_tempDir.FullName);
        var folder = await repo.GetFolderByPathAsync(_tempDir.FullName);

        Assert.IsNotNull(folder);
        Assert.AreEqual("My Folder", folder.Label);
    }

    [TestMethod]
    public async Task GetFolderByPath_UnknownPath_ReturnsNull()
    {
        await WriteFolderSidecar(_tempDir.FullName, "My Folder");

        var repo = CreateRepository(_tempDir.FullName);
        var folder = await repo.GetFolderByPathAsync("/some/other/path");

        Assert.IsNull(folder);
    }
}

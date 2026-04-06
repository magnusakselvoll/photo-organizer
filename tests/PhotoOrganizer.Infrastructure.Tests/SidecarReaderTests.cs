using System.Text.Json;
using PhotoOrganizer.Domain.Exceptions;
using PhotoOrganizer.Infrastructure.Sidecars;

namespace PhotoOrganizer.Infrastructure.Tests;

[TestClass]
public sealed class SidecarReaderTests
{
    private DirectoryInfo _tempDir = null!;
    private SidecarReader _reader = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Directory.CreateTempSubdirectory("SidecarReaderTests_");
        _reader = new SidecarReader();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _tempDir.Delete(recursive: true);
    }

    [TestMethod]
    public async Task ReadPhotoMeta_ValidJson_ReturnsPopulatedSidecar()
    {
        var photoPath = Path.Combine(_tempDir.FullName, "test.jpg");
        var sidecarPath = Path.Combine(_tempDir.FullName, "test.meta.json");
        var capturedAt = new DateTimeOffset(2023, 7, 14, 18, 30, 0, TimeSpan.FromHours(2));
        var groupId = Guid.NewGuid();

        await File.WriteAllTextAsync(sidecarPath, $$"""
            {
              "version": 1,
              "capturedAt": "{{capturedAt:O}}",
              "duplicateGroupId": "{{groupId}}",
              "isPreferred": true,
              "tags": ["holiday", "beach"]
            }
            """);

        var result = await _reader.ReadPhotoMetaAsync(photoPath);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Version);
        Assert.AreEqual(capturedAt, result.CapturedAt);
        Assert.AreEqual(groupId, result.DuplicateGroupId);
        Assert.IsTrue(result.IsPreferred);
        CollectionAssert.AreEqual(new[] { "holiday", "beach" }, result.Tags);
    }

    [TestMethod]
    public async Task ReadPhotoMeta_MissingFile_ReturnsNull()
    {
        var photoPath = Path.Combine(_tempDir.FullName, "nonexistent.jpg");

        var result = await _reader.ReadPhotoMetaAsync(photoPath);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ReadPhotoMeta_MalformedJson_ThrowsSidecarParsingException()
    {
        var photoPath = Path.Combine(_tempDir.FullName, "test.jpg");
        var sidecarPath = Path.Combine(_tempDir.FullName, "test.meta.json");
        await File.WriteAllTextAsync(sidecarPath, "{ this is not valid json }");

        try
        {
            await _reader.ReadPhotoMetaAsync(photoPath);
            Assert.Fail("Expected SidecarParsingException was not thrown");
        }
        catch (SidecarParsingException) { }
    }

    [TestMethod]
    public async Task ReadPhotoMeta_UnknownFields_DoesNotFail()
    {
        var photoPath = Path.Combine(_tempDir.FullName, "test.jpg");
        var sidecarPath = Path.Combine(_tempDir.FullName, "test.meta.json");
        await File.WriteAllTextAsync(sidecarPath, """
            {
              "version": 1,
              "futureField": "some future value",
              "anotherUnknown": 42
            }
            """);

        var result = await _reader.ReadPhotoMetaAsync(photoPath);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Version);
    }

    [TestMethod]
    public async Task ReadFolderSidecar_ValidJson_ReturnsFolderSidecar()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir.FullName, "_folder.json"), """
            {
              "version": 1,
              "label": "Holiday 2023",
              "type": "originals",
              "enabled": true
            }
            """);

        var result = await _reader.ReadFolderSidecarAsync(_tempDir.FullName);

        Assert.IsNotNull(result);
        Assert.AreEqual("Holiday 2023", result.Label);
        Assert.AreEqual("originals", result.Type);
        Assert.IsTrue(result.Enabled);
    }

    [TestMethod]
    public async Task ReadFolderSidecar_MissingFile_ReturnsNull()
    {
        var result = await _reader.ReadFolderSidecarAsync(_tempDir.FullName);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ReadFolderSidecar_MalformedJson_ThrowsSidecarParsingException()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir.FullName, "_folder.json"), "not json at all");

        try
        {
            await _reader.ReadFolderSidecarAsync(_tempDir.FullName);
            Assert.Fail("Expected SidecarParsingException was not thrown");
        }
        catch (SidecarParsingException) { }
    }
}

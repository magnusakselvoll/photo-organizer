using PhotoOrganizer.Domain;

namespace PhotoOrganizer.Domain.Tests;

/// <summary>
/// Guards the invariant that every format the browser can display (natively or via transcoding)
/// is also discoverable by the crawler and indexer. If this fails, files of that type will be
/// silently skipped during discovery and will never appear in grid or slideshow listings.
/// </summary>
[TestClass]
public sealed class SupportedPhotoExtensionsTests
{
    [TestMethod]
    public void SupportedPhotoExtensions_ContainsAllDisplayableExtensions()
    {
        // Arrange
        var displayable = DisplayableImageFormats.AllDisplayableExtensions;
        var supported = SupportedPhotoExtensions.All;

        // Act
        var missing = displayable.Where(ext => !supported.Contains(ext)).ToList();

        // Assert
        Assert.AreEqual(0, missing.Count,
            $"These displayable extensions are not discoverable and would never appear in listings: {string.Join(", ", missing)}");
    }

    [TestMethod]
    [DataRow(".heif",  DisplayName = "heif (transcodable, previously missing)")]
    [DataRow(".webp",  DisplayName = "webp (browser-native, previously missing)")]
    [DataRow(".gif",   DisplayName = "gif (browser-native, previously missing)")]
    [DataRow(".avif",  DisplayName = "avif (browser-native, previously missing)")]
    [DataRow(".bmp",   DisplayName = "bmp (browser-native, previously missing)")]
    [DataRow(".jpg",   DisplayName = "jpg (always present)")]
    [DataRow(".jpeg",  DisplayName = "jpeg (always present)")]
    [DataRow(".png",   DisplayName = "png (always present)")]
    [DataRow(".heic",  DisplayName = "heic (always present)")]
    [DataRow(".cr2",   DisplayName = "cr2 (raw, non-displayable)")]
    [DataRow(".cr3",   DisplayName = "cr3 (raw, non-displayable)")]
    [DataRow(".orf",   DisplayName = "orf (raw, non-displayable)")]
    [DataRow(".arw",   DisplayName = "arw (raw, non-displayable)")]
    [DataRow(".nef",   DisplayName = "nef (raw, non-displayable)")]
    [DataRow(".rw2",   DisplayName = "rw2 (raw, non-displayable)")]
    [DataRow(".tiff",  DisplayName = "tiff (non-displayable)")]
    [DataRow(".tif",   DisplayName = "tif (non-displayable)")]
    public void SupportedPhotoExtensions_IsSupported_ReturnsTrue(string extension)
    {
        var filePath = $"photo{extension}";
        Assert.IsTrue(SupportedPhotoExtensions.IsSupported(filePath),
            $"Extension '{extension}' should be supported.");
    }

    [TestMethod]
    public void SupportedPhotoExtensions_IsSupported_CaseInsensitive()
    {
        Assert.IsTrue(SupportedPhotoExtensions.IsSupported("photo.HEIF"));
        Assert.IsTrue(SupportedPhotoExtensions.IsSupported("photo.WEBP"));
        Assert.IsTrue(SupportedPhotoExtensions.IsSupported("photo.JPG"));
        Assert.IsTrue(SupportedPhotoExtensions.IsSupported("photo.CR2"));
    }

    [TestMethod]
    public void SupportedPhotoExtensions_IsSupported_ReturnsFalse_ForNonPhotoFiles()
    {
        Assert.IsFalse(SupportedPhotoExtensions.IsSupported("document.pdf"));
        Assert.IsFalse(SupportedPhotoExtensions.IsSupported("archive.zip"));
        Assert.IsFalse(SupportedPhotoExtensions.IsSupported("video.mp4"));
        Assert.IsFalse(SupportedPhotoExtensions.IsSupported("noextension"));
    }
}

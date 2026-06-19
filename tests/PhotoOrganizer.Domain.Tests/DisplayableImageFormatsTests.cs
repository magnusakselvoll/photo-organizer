using PhotoOrganizer.Domain;

namespace PhotoOrganizer.Domain.Tests;

/// <summary>
/// Tests for <see cref="DisplayableImageFormats"/> — the single source of truth for which
/// formats the server will serve in grid and slideshow listings.
/// A regression here (e.g. accidentally adding .tiff to displayable, or breaking case-folding)
/// changes what photos appear in the UI without any other test catching it.
/// </summary>
[TestClass]
public sealed class DisplayableImageFormatsTests
{
    // --- IsBrowserDisplayable ---

    [TestMethod]
    [DataRow("photo.jpg",  DisplayName = ".jpg")]
    [DataRow("photo.jpeg", DisplayName = ".jpeg")]
    [DataRow("photo.png",  DisplayName = ".png")]
    [DataRow("photo.gif",  DisplayName = ".gif")]
    [DataRow("photo.webp", DisplayName = ".webp")]
    [DataRow("photo.avif", DisplayName = ".avif")]
    [DataRow("photo.bmp",  DisplayName = ".bmp")]
    public void IsBrowserDisplayable_BrowserNativeFormat_ReturnsTrue(string filePath)
    {
        Assert.IsTrue(DisplayableImageFormats.IsBrowserDisplayable(filePath));
    }

    [TestMethod]
    [DataRow("photo.heic", DisplayName = ".heic (transcodable, not browser-native)")]
    [DataRow("photo.heif", DisplayName = ".heif (transcodable, not browser-native)")]
    [DataRow("photo.cr2",  DisplayName = ".cr2 (RAW)")]
    [DataRow("photo.nef",  DisplayName = ".nef (RAW)")]
    [DataRow("photo.tiff", DisplayName = ".tiff (non-displayable)")]
    public void IsBrowserDisplayable_NonNativeFormat_ReturnsFalse(string filePath)
    {
        Assert.IsFalse(DisplayableImageFormats.IsBrowserDisplayable(filePath));
    }

    [TestMethod]
    public void IsBrowserDisplayable_CaseInsensitive()
    {
        Assert.IsTrue(DisplayableImageFormats.IsBrowserDisplayable("photo.JPG"));
        Assert.IsTrue(DisplayableImageFormats.IsBrowserDisplayable("photo.PNG"));
        Assert.IsTrue(DisplayableImageFormats.IsBrowserDisplayable("photo.WEBP"));
    }

    // --- IsTranscodable ---

    [TestMethod]
    [DataRow("photo.heic", DisplayName = ".heic")]
    [DataRow("photo.heif", DisplayName = ".heif")]
    public void IsTranscodable_TranscodableFormat_ReturnsTrue(string filePath)
    {
        Assert.IsTrue(DisplayableImageFormats.IsTranscodable(filePath));
    }

    [TestMethod]
    [DataRow("photo.jpg",  DisplayName = ".jpg (browser-native, not transcodable)")]
    [DataRow("photo.png",  DisplayName = ".png (browser-native, not transcodable)")]
    [DataRow("photo.cr2",  DisplayName = ".cr2 (RAW, not transcodable)")]
    [DataRow("photo.tiff", DisplayName = ".tiff (non-displayable, not transcodable)")]
    public void IsTranscodable_NonTranscodableFormat_ReturnsFalse(string filePath)
    {
        Assert.IsFalse(DisplayableImageFormats.IsTranscodable(filePath));
    }

    [TestMethod]
    public void IsTranscodable_CaseInsensitive()
    {
        Assert.IsTrue(DisplayableImageFormats.IsTranscodable("photo.HEIC"));
        Assert.IsTrue(DisplayableImageFormats.IsTranscodable("photo.HEIF"));
    }

    // --- IsDisplayable ---

    [TestMethod]
    [DataRow("photo.jpg",  DisplayName = ".jpg (browser-native)")]
    [DataRow("photo.png",  DisplayName = ".png (browser-native)")]
    [DataRow("photo.gif",  DisplayName = ".gif (browser-native)")]
    [DataRow("photo.webp", DisplayName = ".webp (browser-native)")]
    [DataRow("photo.avif", DisplayName = ".avif (browser-native)")]
    [DataRow("photo.bmp",  DisplayName = ".bmp (browser-native)")]
    [DataRow("photo.heic", DisplayName = ".heic (transcodable)")]
    [DataRow("photo.heif", DisplayName = ".heif (transcodable)")]
    public void IsDisplayable_DisplayableFormat_ReturnsTrue(string filePath)
    {
        Assert.IsTrue(DisplayableImageFormats.IsDisplayable(filePath));
    }

    [TestMethod]
    [DataRow("photo.cr2",  DisplayName = ".cr2 (RAW)")]
    [DataRow("photo.cr3",  DisplayName = ".cr3 (RAW)")]
    [DataRow("photo.nef",  DisplayName = ".nef (RAW)")]
    [DataRow("photo.arw",  DisplayName = ".arw (RAW)")]
    [DataRow("photo.tiff", DisplayName = ".tiff")]
    [DataRow("photo.tif",  DisplayName = ".tif")]
    [DataRow("photo.pdf",  DisplayName = ".pdf")]
    public void IsDisplayable_NonDisplayableFormat_ReturnsFalse(string filePath)
    {
        Assert.IsFalse(DisplayableImageFormats.IsDisplayable(filePath));
    }

    // --- AllDisplayableExtensions ---

    [TestMethod]
    public void AllDisplayableExtensions_ContainsBothBrowserNativeAndTranscodable()
    {
        var all = DisplayableImageFormats.AllDisplayableExtensions;

        // browser-native
        Assert.IsTrue(all.Contains(".jpg"),  ".jpg should be in AllDisplayableExtensions");
        Assert.IsTrue(all.Contains(".jpeg"), ".jpeg should be in AllDisplayableExtensions");
        Assert.IsTrue(all.Contains(".png"),  ".png should be in AllDisplayableExtensions");
        Assert.IsTrue(all.Contains(".gif"),  ".gif should be in AllDisplayableExtensions");
        Assert.IsTrue(all.Contains(".webp"), ".webp should be in AllDisplayableExtensions");
        Assert.IsTrue(all.Contains(".avif"), ".avif should be in AllDisplayableExtensions");
        Assert.IsTrue(all.Contains(".bmp"),  ".bmp should be in AllDisplayableExtensions");

        // transcodable
        Assert.IsTrue(all.Contains(".heic"), ".heic should be in AllDisplayableExtensions");
        Assert.IsTrue(all.Contains(".heif"), ".heif should be in AllDisplayableExtensions");
    }

    [TestMethod]
    public void AllDisplayableExtensions_DoesNotContainRawOrNonDisplayable()
    {
        var all = DisplayableImageFormats.AllDisplayableExtensions;
        Assert.IsFalse(all.Contains(".cr2"),  ".cr2 should not be displayable");
        Assert.IsFalse(all.Contains(".tiff"), ".tiff should not be displayable");
        Assert.IsFalse(all.Contains(".nef"),  ".nef should not be displayable");
    }
}

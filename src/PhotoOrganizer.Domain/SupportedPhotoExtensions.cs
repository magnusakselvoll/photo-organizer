namespace PhotoOrganizer.Domain;

/// <summary>
/// Single source of truth for which file extensions the crawler and indexer recognise as photos.
/// The discoverable set is a superset of <see cref="DisplayableImageFormats.AllDisplayableExtensions"/>:
/// every format the browser can display (natively or via transcoding) is also discoverable, plus
/// RAW and TIFF formats that are indexed and downloadable but never shown in grid or slideshow listings.
/// </summary>
public static class SupportedPhotoExtensions
{
    /// <summary>
    /// Formats that are indexed and downloadable but are not browser-displayable, so they are
    /// never included in grid or slideshow listings.
    /// </summary>
    private static readonly HashSet<string> NonDisplayablePhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cr2", ".cr3", ".orf", ".arw", ".nef", ".rw2", ".tiff", ".tif",
    };

    /// <summary>
    /// All extensions the crawler and indexer recognise as photos.
    /// Includes every entry in <see cref="DisplayableImageFormats.AllDisplayableExtensions"/>
    /// plus RAW/TIFF formats that are non-displayable.
    /// </summary>
    public static readonly IReadOnlySet<string> All = BuildAll();

    /// <summary>Returns true when the file's extension is a recognised photo format.</summary>
    public static bool IsSupported(string filePath) =>
        All.Contains(Path.GetExtension(filePath));

    private static HashSet<string> BuildAll()
    {
        var set = new HashSet<string>(DisplayableImageFormats.AllDisplayableExtensions, StringComparer.OrdinalIgnoreCase);
        set.UnionWith(NonDisplayablePhotoExtensions);
        return set;
    }
}

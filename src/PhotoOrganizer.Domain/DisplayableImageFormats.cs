namespace PhotoOrganizer.Domain;

/// <summary>
/// Single source of truth for which image formats are displayable by the server.
/// A photo is "displayable" when the browser can render it — either natively or after
/// server-side transcoding (HEIC → JPEG). Non-displayable formats (RAW, bare TIFF) are
/// never included in grid or slideshow listings, though they remain downloadable via the
/// version panel's image endpoint.
/// </summary>
public static class DisplayableImageFormats
{
    /// <summary>
    /// Extensions that browsers can render natively inside an &lt;img&gt; element.
    /// RAW formats and container formats that require transcoding are excluded.
    /// </summary>
    private static readonly HashSet<string> BrowserDisplayableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif", ".bmp",
    };

    /// <summary>
    /// Extensions that the server transcodes to JPEG on the fly before sending to the browser.
    /// </summary>
    private static readonly HashSet<string> TranscodableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".heic", ".heif",
    };

    /// <summary>Returns true when the file's extension can be rendered natively by browsers.</summary>
    public static bool IsBrowserDisplayable(string filePath) =>
        BrowserDisplayableExtensions.Contains(Path.GetExtension(filePath));

    /// <summary>Returns true when the server transcodes this file to JPEG for browser display.</summary>
    public static bool IsTranscodable(string filePath) =>
        TranscodableExtensions.Contains(Path.GetExtension(filePath));

    /// <summary>
    /// Returns true when the browser can display this file, either natively or via server-side
    /// transcoding. Use this to decide whether to include a photo in grid and slideshow listings.
    /// </summary>
    public static bool IsDisplayable(string filePath) =>
        IsBrowserDisplayable(filePath) || IsTranscodable(filePath);
}

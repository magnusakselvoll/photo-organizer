namespace PhotoOrganizer.Application.Photos;

public sealed record PhotoFilter
{
    public string? Folder { get; init; }
    public string? Type { get; init; }
    public bool Deduplicated { get; init; } = true;

    // Expanded filters — all nullable so existing callers are unaffected.
    /// <summary>Case-insensitive substring match on the photo's filename (with extension).</summary>
    public string? FileName { get; init; }
    /// <summary>Inclusive lower bound on effective date (CapturedAt ?? FileModifiedAt), day-granularity.</summary>
    public DateOnly? DateFrom { get; init; }
    /// <summary>Inclusive upper bound on effective date (whole day included), day-granularity.</summary>
    public DateOnly? DateTo { get; init; }

    // Offset pagination (legacy — used by slideshow and existing callers)
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    // Keyset / cursor pagination — takes precedence over offset when Limit is set.
    // Cursor encodes the exclusive lower bound of the next page; null means "start from the top".
    public string? Cursor { get; init; }
    public int? Limit { get; init; }
}

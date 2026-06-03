namespace PhotoOrganizer.Application.Photos;

public sealed record PhotoFilter
{
    public string? Folder { get; init; }
    public string? Type { get; init; }
    public bool Deduplicated { get; init; } = true;

    // Offset pagination (legacy — used by slideshow and existing callers)
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    // Keyset / cursor pagination — takes precedence over offset when Limit is set.
    // Cursor encodes the exclusive lower bound of the next page; null means "start from the top".
    public string? Cursor { get; init; }
    public int? Limit { get; init; }
}

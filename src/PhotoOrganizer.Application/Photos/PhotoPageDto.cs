namespace PhotoOrganizer.Application.Photos;

public sealed record PhotoPageDto
{
    public required IReadOnlyList<PhotoDto> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }

    /// <summary>
    /// Opaque cursor for the next keyset page. Null when the end of the list has been reached
    /// or when the response was produced by the legacy offset-pagination path.
    /// </summary>
    public string? NextCursor { get; init; }
}

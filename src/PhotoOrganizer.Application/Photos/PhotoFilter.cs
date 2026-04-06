namespace PhotoOrganizer.Application.Photos;

public sealed record PhotoFilter
{
    public string? Folder { get; init; }
    public string? Type { get; init; }
    public bool Deduplicated { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

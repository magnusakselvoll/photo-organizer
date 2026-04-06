namespace PhotoOrganizer.Application.Photos;

public sealed record PhotoPageDto
{
    public required IReadOnlyList<PhotoDto> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

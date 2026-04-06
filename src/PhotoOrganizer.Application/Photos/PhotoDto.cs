namespace PhotoOrganizer.Application.Photos;

public sealed record PhotoDto
{
    public required Guid Id { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public DateTimeOffset? CapturedAt { get; init; }
    public required string FolderType { get; init; }
    public Guid? DuplicateGroupId { get; init; }
    public bool IsPreferred { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}

namespace PhotoOrganizer.Domain;

public sealed record Photo
{
    public required Guid Id { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public DateTimeOffset? CapturedAt { get; init; }
    public required FolderType FolderType { get; init; }
    public Guid? DuplicateGroupId { get; init; }
    public bool IsPreferred { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}

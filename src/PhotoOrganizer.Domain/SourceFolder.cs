namespace PhotoOrganizer.Domain;

public sealed record SourceFolder
{
    public required string Path { get; init; }
    public required string Label { get; init; }
    public required FolderType Type { get; init; }
    public required bool Enabled { get; init; }
}

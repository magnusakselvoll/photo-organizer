namespace PhotoOrganizer.Application.Folders;

public sealed record FolderDto
{
    public required string Path { get; init; }
    public required string Label { get; init; }
    public required string Type { get; init; }
    public required bool Enabled { get; init; }
}

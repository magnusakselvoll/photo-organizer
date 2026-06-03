namespace PhotoOrganizer.Application.Index;

public sealed record IndexStatsDto
{
    public required bool Complete { get; init; }
    public required int TotalPhotoCount { get; init; }
    public required long SidecarSizeBytes { get; init; }
    public required IReadOnlyList<FolderStatsDto> Folders { get; init; }
}

public sealed record FolderStatsDto
{
    public required string Path { get; init; }
    public required string Label { get; init; }
    public required string Type { get; init; }
    public required int PhotoCount { get; init; }
}

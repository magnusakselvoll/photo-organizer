namespace PhotoOrganizer.Application.Photos;

public sealed record PhotoDto
{
    public required Guid Id { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public DateTimeOffset? CapturedAt { get; init; }

    /// <summary>
    /// Sort key: CapturedAt if present, otherwise FileModifiedAt. Matches the effective timestamp
    /// used for ordering and cursor pagination. Always set on list responses; null only when both
    /// CapturedAt and FileModifiedAt are absent.
    /// </summary>
    public DateTimeOffset? EffectiveDate { get; init; }

    public required string FolderType { get; init; }
    public Guid? DuplicateGroupId { get; init; }
    public bool IsPreferred { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// All versions of this photo (same duplicate group), ordered preferred first.
    /// Populated only by single-photo lookups; empty in list/page responses.
    /// </summary>
    public IReadOnlyList<PhotoVersionDto> Versions { get; init; } = [];
}

/// <summary>A lightweight summary of one version within a duplicate group.</summary>
public sealed record PhotoVersionDto
{
    public required Guid Id { get; init; }
    public required string FileName { get; init; }
    public required string FolderType { get; init; }
    public required string FilePath { get; init; }
    public bool IsPreferred { get; init; }
}

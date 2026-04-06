namespace PhotoOrganizer.Domain;

public enum FolderType
{
    Originals,
    Edits,
    Mixed
}

public static class FolderTypeExtensions
{
    public static FolderType Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "originals" => FolderType.Originals,
        "edits" => FolderType.Edits,
        _ => FolderType.Mixed
    };
}

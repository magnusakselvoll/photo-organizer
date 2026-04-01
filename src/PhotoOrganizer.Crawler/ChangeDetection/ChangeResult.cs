namespace PhotoOrganizer.Crawler.ChangeDetection;

public sealed record ChangeResult(ChangeKind Kind, string? ComputedHash = null);

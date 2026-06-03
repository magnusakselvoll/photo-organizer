namespace PhotoOrganizer.Application;

public sealed class PhotoOrganizerSettings
{
    public string[] ScanRoots { get; set; } = [];
    public SlideshowSettings Slideshow { get; set; } = new();
    public IndexingSettings Indexing { get; set; } = new();
}

public sealed class SlideshowSettings
{
    public int IntervalSeconds { get; set; } = 8;
    public int TransitionMs { get; set; } = 500;
}

public sealed class IndexingSettings
{
    /// <summary>Number of concurrent worker threads during the randomized background index build.</summary>
    public int MaxParallelism { get; set; } = 3;
}

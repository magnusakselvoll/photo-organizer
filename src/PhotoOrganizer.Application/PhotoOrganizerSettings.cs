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

    /// <summary>Directory for the server-owned index cache file. Defaults to a subdirectory of
    /// the system temp folder. Use an empty string to disable the cache entirely.</summary>
    public string CacheDirectory { get; set; } = "";

    /// <summary>How long the index cache is considered fresh. Once expired the index is rebuilt
    /// from sidecars on the next server start.</summary>
    public int CacheTtlHours { get; set; } = 72;

    /// <summary>Write the in-progress cache every N photos indexed (0 = only on completion).</summary>
    public int CacheWriteIntervalPhotos { get; set; } = 500;
}

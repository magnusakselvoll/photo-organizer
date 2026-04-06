namespace PhotoOrganizer.Application;

public sealed class PhotoOrganizerSettings
{
    public string[] ScanRoots { get; set; } = [];
    public SlideshowSettings Slideshow { get; set; } = new();
}

public sealed class SlideshowSettings
{
    public int IntervalSeconds { get; set; } = 8;
    public int TransitionMs { get; set; } = 500;
}

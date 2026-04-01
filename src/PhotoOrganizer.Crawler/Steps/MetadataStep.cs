using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PhotoOrganizer.Crawler.Pipeline;
using Serilog;

namespace PhotoOrganizer.Crawler.Steps;

public sealed class MetadataStep : IProcessingStep
{
    public string Name => "metadata";
    public int Version => 1;
    public IReadOnlyList<string> DependsOn => [];

    public Task ExecuteAsync(ProcessingContext context)
    {
        context.Sidecar.CapturedAt = ExtractCapturedAt(context.FilePath);
        return Task.CompletedTask;
    }

    private static DateTimeOffset? ExtractCapturedAt(string filePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);
            var exif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exif is null)
                return null;

            if (!exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dateTime))
                return null;

            return new DateTimeOffset(dateTime, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not extract EXIF metadata from {FilePath}", filePath);
            return null;
        }
    }
}

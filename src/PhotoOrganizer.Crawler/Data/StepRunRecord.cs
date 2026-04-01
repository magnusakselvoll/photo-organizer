namespace PhotoOrganizer.Crawler.Data;

public sealed class StepRunRecord
{
    public int FileId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public int StepVersion { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string Status { get; set; } = "completed";
    public string? ErrorMessage { get; set; }
}

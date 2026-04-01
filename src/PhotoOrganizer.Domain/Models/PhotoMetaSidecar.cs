using System.Text.Json.Serialization;

namespace PhotoOrganizer.Domain.Models;

public sealed class PhotoMetaSidecar
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("capturedAt")]
    public DateTimeOffset? CapturedAt { get; set; }

    [JsonPropertyName("duplicateGroupId")]
    public Guid? DuplicateGroupId { get; set; }

    [JsonPropertyName("isPreferred")]
    public bool IsPreferred { get; set; } = false;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("crawlSteps")]
    public Dictionary<string, CrawlStepRecord> CrawlSteps { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
}

public sealed class CrawlStepRecord
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; set; }
}

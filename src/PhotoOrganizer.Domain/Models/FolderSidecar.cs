using System.Text.Json.Serialization;

namespace PhotoOrganizer.Domain.Models;

public sealed class FolderSidecar
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; } = "mixed";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }
}

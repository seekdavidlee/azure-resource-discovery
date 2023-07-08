using System.Text.Json.Serialization;

namespace AzureResourceDiscovery.Core;

public class UniqueResource
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ard-resource-id")]
    public string? ResourceId { get; set; }

    [JsonIgnore]
    public const string TagKey = "ard-resource-id";

    [JsonPropertyName("resource-type")]
    public string? ResourceType { get; set; }

    [JsonPropertyName("resource-group-names")]
    public List<string>? ResourceGroupNames { get; set; }
}

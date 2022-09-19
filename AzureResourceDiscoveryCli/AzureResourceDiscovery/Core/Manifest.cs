using System.Text.Json.Serialization;

namespace AzureResourceDiscovery.Core
{
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

    public class GroupResources
    {
        [JsonPropertyName("resource-group-names")]
        public List<string>? ResourceGroupNames { get; set; }

        [JsonPropertyName("ard-solution-id")]
        public string? SolutionId { get; set; }

        [JsonPropertyName("ard-environment")]
        public string? Environment { get; set; }

        [JsonPropertyName("ard-region")]
        public string? Region { get; set; }
    }

    public class Manifest
    {
        [JsonPropertyName("resource-group-location")]
        public string? ResourceGroupLocation { get; set; }

        [JsonPropertyName("unique-resources")]
        public List<UniqueResource>? UniqueResources { get; set; }

        [JsonPropertyName("group-resources")]
        public List<GroupResources>? GroupResources { get; set; }
    }
}

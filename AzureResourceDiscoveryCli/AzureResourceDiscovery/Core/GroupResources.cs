using System.Text.Json.Serialization;

namespace AzureResourceDiscovery.Core
{
    public class GroupResources
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("resource-group-names")]
        public List<string>? ResourceGroupNames { get; set; }

        [JsonPropertyName(Constants.ArdSolutionId)]
        public string? SolutionId { get; set; }

        [JsonPropertyName(Constants.ArdEnvironment)]
        public string? Environment { get; set; }

        [JsonPropertyName(Constants.ArdRegion)]
        public string? Region { get; set; }
    }
}

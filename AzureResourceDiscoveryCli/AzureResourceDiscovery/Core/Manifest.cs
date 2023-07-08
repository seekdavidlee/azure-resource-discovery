using System.Text.Json.Serialization;

namespace AzureResourceDiscovery.Core;

public class Manifest
{
    [JsonPropertyName("managed-identity")]
    public ManagedIdentityResource? ManagedIdentity { get; set; }

    [JsonPropertyName("resource-group-location")]
    public string? ResourceGroupLocation { get; set; }

    [JsonPropertyName("unique-resources")]
    public List<UniqueResource>? UniqueResources { get; set; }

    [JsonPropertyName("group-resources")]
    public List<GroupResources>? GroupResources { get; set; }
}

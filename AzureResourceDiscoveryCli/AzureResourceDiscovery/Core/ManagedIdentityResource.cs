using System.Text.Json.Serialization;

namespace AzureResourceDiscovery.Core;

/// <summary>
/// Managed identity resource.
/// </summary>
public class ManagedIdentityResource
{
    /// <summary>
    /// Gets or sets the resource group name of the managed identity name.
    /// </summary>
    [JsonPropertyName("resource-group-name")]
    public string? ResourceGroupName { get; set; }

    /// <summary>
    /// Gets or sets the managed identity name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

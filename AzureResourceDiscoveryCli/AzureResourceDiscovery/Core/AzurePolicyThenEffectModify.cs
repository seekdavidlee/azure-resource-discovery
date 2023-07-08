using System.Text.Json.Serialization;

namespace AzureResourceDiscovery.Core;

public class AzurePolicyThenEffectModify
{
    public AzurePolicyThenEffectModify()
    {
        Details = new();
    }

    [JsonPropertyName("effect")]
    public string Effect { get; set; } = "modify";

    [JsonPropertyName("details")]
    public AzurePolicyThenEffectDetails Details { get; set; }
}

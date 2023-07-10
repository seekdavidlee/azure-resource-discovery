using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureResourceDiscovery.Core;

public class AzurePolicyDefinition
{
    public AzurePolicyDefinition(AzurePolicy azurePolicy, string displayName, string description)
    {
        PolicyRule = azurePolicy;
        DisplayName = displayName;
        Description = description;
    }

    public AzurePolicy PolicyRule { get; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; }

    [JsonPropertyName("description")]
    public string Description { get; }

    public override string ToString()
    {
        // See: https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-character-encoding
        // Used to allow single quote, otherwise it will be converted.
        JsonSerializerOptions options = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        return JsonSerializer.Serialize(this, options);
    }
}

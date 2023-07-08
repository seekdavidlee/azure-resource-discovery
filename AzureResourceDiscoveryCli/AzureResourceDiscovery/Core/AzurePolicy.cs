using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureResourceDiscovery.Core;

public class AzurePolicy
{
    public AzurePolicy()
    {
        If = new AzurePolicyDtoIf();
        ThenEffectModify = new AzurePolicyThenEffectModify();
    }

    [JsonPropertyName("if")]
    public AzurePolicyDtoIf If { get; }

    [JsonPropertyName("then")]
    public AzurePolicyThenEffectModify ThenEffectModify { get; }

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

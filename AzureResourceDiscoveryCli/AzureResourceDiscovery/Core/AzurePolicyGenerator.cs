using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
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

public class AzurePolicyGenerator
{
    public Manifest? Manifest { get; private set; }

    public bool Apply(string content)
    {
        Manifest = JsonSerializer.Deserialize<Manifest>(content);

        if (Manifest == null) return false;

        if (Manifest.UniqueResources != null)
        {
            foreach (var uniqueResource in Manifest.UniqueResources)
            {
                AzurePolicy azurePolicy = new();

                if (uniqueResource.ResourceGroupNames is null) throw new ApplicationException("ResourceGroupNames cannot be null!");
                if (string.IsNullOrEmpty(uniqueResource.Name)) throw new ApplicationException("Name cannot be null.");
                if (string.IsNullOrEmpty(uniqueResource.ResourceType)) throw new ApplicationException("ResourceType cannot be null.");
                if (string.IsNullOrEmpty(uniqueResource.ResourceId)) throw new ApplicationException("ResourceId cannot be null.");

                azurePolicy.If.UniqueResource(
                    uniqueResource.ResourceType,
                    UniqueResource.TagKey,
                    uniqueResource.ResourceId);

                if (string.IsNullOrEmpty(uniqueResource.ResourceId)) throw new ApplicationException("ResourceId cannot be null!");

                azurePolicy.ThenEffectModify.Details.AddOrReplaceTag("ard-resource-id", uniqueResource.ResourceId);

                azurePolicy.ThenEffectModify.Details.RoleDefinationIds.Add(Constants.RoleDefinationIds.TagContributor);

                ProcessAzureDefinition(new AzurePolicyDefinition(azurePolicy, $"Enforce ard-resource-id {uniqueResource.Name}", $"Enforce ard-resource-id for {uniqueResource.Name}"));
            }
        }

        if (Manifest.GroupResources is not null)
        {
            foreach (var groupResource in Manifest.GroupResources)
            {
                if (string.IsNullOrEmpty(groupResource.Name)) throw new ApplicationException("Name cannot be null.");
                if (groupResource.ResourceGroupNames == null) throw new ApplicationException("ResourceGroupNames cannot be null!");
                if (groupResource.SolutionId == null) throw new ApplicationException("Solution Id cannot be null!");
                if (groupResource.Environment == null) throw new ApplicationException("Environment cannot be null!");

                AzurePolicy azurePolicy = new();

                var dic = new Dictionary<string, string>();

                dic[Constants.ArdSolutionId] = groupResource.SolutionId;
                dic[Constants.ArdEnvironment] = groupResource.Environment;

                azurePolicy.ThenEffectModify.Details.AddOrReplaceTag(Constants.ArdSolutionId, groupResource.SolutionId);
                azurePolicy.ThenEffectModify.Details.AddOrReplaceTag(Constants.ArdEnvironment, groupResource.Environment);

                if (!string.IsNullOrEmpty(groupResource.Region))
                {
                    dic[Constants.ArdRegion] = groupResource.Region;
                    azurePolicy.ThenEffectModify.Details.AddOrReplaceTag(Constants.ArdRegion, groupResource.Region);
                }

                azurePolicy.If.AnyResource(dic);

                azurePolicy.ThenEffectModify.Details.RoleDefinationIds.Add(Constants.RoleDefinationIds.TagContributor);

                ProcessAzureDefinition(new AzurePolicyDefinition(azurePolicy, $"Enforce ard solution specific tags for {groupResource.Name}", $"Enforce ard solution specific tags for {groupResource.Name}"));
            }
        }

        return true;
    }

    private static ArmClient? _client;
    private static string? _subscriptionId;
    private static SubscriptionPolicyDefinitionCollection? _subscriptionPolicyDefinitions;
    private static void ProcessAzureDefinition(AzurePolicyDefinition azurePolicyDefinition)
    {
        if (_client is null)
        {
            _client = new ArmClient(new DefaultAzureCredential());
            var sub = _client.GetDefaultSubscription();
            if (sub.Id.SubscriptionId is not null)
            {
                _subscriptionId = sub.Id.SubscriptionId;
            }

            _subscriptionPolicyDefinitions = sub.GetSubscriptionPolicyDefinitions();
        }

        var found = _subscriptionPolicyDefinitions is not null && _subscriptionPolicyDefinitions.Any(x => x.Data.DisplayName == azurePolicyDefinition.DisplayName);

        if (found)
        {
            Console.WriteLine($"{azurePolicyDefinition.DisplayName} exist.");
            return;
        }
        ResourceIdentifier id = new ResourceIdentifier($"/subscriptions/{_subscriptionId}/providers/Microsoft.Authorization/policyDefinitions/{Guid.NewGuid()}");
        var data = new GenericResourceData(AzureLocation.CentralUS);
        data.Properties = new BinaryData(azurePolicyDefinition.ToString());
        _client.GetGenericResources().CreateOrUpdate(Azure.WaitUntil.Completed, id, data);
        Console.WriteLine($"Created {id}");
    }
}

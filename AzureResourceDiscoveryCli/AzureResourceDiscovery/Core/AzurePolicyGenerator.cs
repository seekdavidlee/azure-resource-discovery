using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;

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
    private readonly ResourceGroupCollection _resourceGroups;
    private readonly ArmClient _client;
    private readonly string _subscriptionId;
    private readonly SubscriptionResource _subscriptionResource;
    private readonly SubscriptionPolicyDefinitionCollection _subscriptionPolicyDefinitions;

    public AzurePolicyGenerator()
    {
        _client = new ArmClient(new DefaultAzureCredential());
        _subscriptionResource = _client.GetDefaultSubscription();
        if (_subscriptionResource.Id.SubscriptionId is null)
        {
            throw new Exception("Unexpected for subscription Id to be null.");
        }

        _subscriptionId = _subscriptionResource.Id.SubscriptionId;
        _subscriptionPolicyDefinitions = _subscriptionResource.GetSubscriptionPolicyDefinitions();
        _resourceGroups = _subscriptionResource.GetResourceGroups();
    }

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

                var result = ProcessAzureDefinition(new AzurePolicyDefinition(azurePolicy, $"Enforce ard-resource-id {uniqueResource.Name}", $"Enforce ard-resource-id for {uniqueResource.Name}"));


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

                var dic = new Dictionary<string, string>
                {
                    [Constants.ArdSolutionId] = groupResource.SolutionId,
                    [Constants.ArdEnvironment] = groupResource.Environment
                };

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

    private void CreateResourceGroups(List<string> resourceGroupNames, string? location, Dictionary<string, string>? tags)
    {
        if (location is null)
        {
            location = Manifest?.ResourceGroupLocation;
        }

        if (location is null)
        {
            throw new Exception("Unexpected that Resource Group location is null.");
        }

        foreach (var resourceGroupName in resourceGroupNames)
        {
            CreateResourceGroupIfMissing(resourceGroupName, location, (rg) =>
            {
                bool createOrUpdate = false;
                if (rg.Tags.TryGetValue(Constants.ArdInternalSolutionId, out string? val))
                {
                    if (val != Constants.ArdInternalSolutionIdValue)
                    {
                        rg.Tags[Constants.ArdInternalSolutionId] = Constants.ArdInternalSolutionIdValue;
                        createOrUpdate = true;
                    }
                }
                else
                {
                    rg.Tags.Add(Constants.ArdInternalSolutionId, Constants.ArdInternalSolutionIdValue);
                    createOrUpdate = true;
                }

                return createOrUpdate;
            });
        }
    }

    private void CreateResourceGroupIfMissing(string resourceGroupName, string location, Func<ResourceGroupData, bool> taggingAction)
    {
        var group = _resourceGroups.SingleOrDefault(x => x.Id.ResourceGroupName == resourceGroupName);
        if (group is null)
        {
            var resourceGroupData = new ResourceGroupData(location);
            taggingAction(resourceGroupData);
            _resourceGroups.CreateOrUpdate(WaitUntil.Completed, resourceGroupName, resourceGroupData);
        }
        else
        {
            if (taggingAction(group.Data))
            {
                _resourceGroups.CreateOrUpdate(WaitUntil.Completed, resourceGroupName, group.Data);
            }
        }
    }


    private ResourceIdentifier? ProcessAzureDefinition(AzurePolicyDefinition azurePolicyDefinition)
    {
        var found = _subscriptionPolicyDefinitions.SingleOrDefault(x => x.Data.DisplayName == azurePolicyDefinition.DisplayName);

        if (found is not null)
        {
            Console.WriteLine($"{azurePolicyDefinition.DisplayName} exist.");
            return found.Id;
        }
        ResourceIdentifier id = new ResourceIdentifier($"/subscriptions/{_subscriptionId}/providers/Microsoft.Authorization/policyDefinitions/{Guid.NewGuid()}");
        var data = new GenericResourceData(AzureLocation.CentralUS);
        data.Properties = new BinaryData(azurePolicyDefinition.ToString());
        _client.GetGenericResources().CreateOrUpdate(Azure.WaitUntil.Completed, id, data);
        Console.WriteLine($"Created {id}");

        return id;
    }
}

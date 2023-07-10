using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using Azure.Identity;
using Azure;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace AzureResourceDiscovery.Core;

public interface IAzureClient
{
    ResourceIdentifier? ProcessAzureDefinition(AzurePolicyDefinition azurePolicyDefinition);
    void CreateResourceGroups(List<string> resourceGroupNames, string location, Dictionary<string, string>? tags);
}

public class AzureClient : IAzureClient
{
    private readonly ResourceGroupCollection _resourceGroups;
    private readonly ArmClient _client;
    private readonly string _subscriptionId;
    private readonly SubscriptionResource _subscriptionResource;
    private readonly SubscriptionPolicyDefinitionCollection _subscriptionPolicyDefinitions;
    private readonly ILogger<AzureClient> logger;

    public AzureClient(ILogger<AzureClient> logger)
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
        this.logger = logger;
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

    public ResourceIdentifier? ProcessAzureDefinition(AzurePolicyDefinition azurePolicyDefinition)
    {
        var found = _subscriptionPolicyDefinitions.SingleOrDefault(x => x.Data.DisplayName == azurePolicyDefinition.DisplayName);

        if (found is not null)
        {
            logger.LogDebug("{policy} exist.", azurePolicyDefinition.DisplayName);
            return found.Id;
        }
        ResourceIdentifier id = new($"/subscriptions/{_subscriptionId}/providers/Microsoft.Authorization/policyDefinitions/{Guid.NewGuid()}");
        var data = new GenericResourceData(AzureLocation.CentralUS);
        data.Properties = new BinaryData(azurePolicyDefinition.ToString());
        _client.GetGenericResources().CreateOrUpdate(WaitUntil.Completed, id, data);
        logger.LogInformation("Create policy {policy}", id);

        return id;
    }

    public void CreateResourceGroups(List<string> resourceGroupNames, string location, Dictionary<string, string>? tags)
    {
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
}

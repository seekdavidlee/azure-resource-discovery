using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AzureResourceDiscovery.Core;

public class AzurePolicyGenerator
{
    private readonly IAzureClient azureClient;
    private readonly ILogger<AzurePolicyGenerator> logger;
    private Manifest? manifest;

    public AzurePolicyGenerator(IAzureClient azureClient, ILogger<AzurePolicyGenerator> logger)
    {
        this.azureClient = azureClient;
        this.logger = logger;
    }
   
    public bool Apply(string content)
    {
        manifest = JsonSerializer.Deserialize<Manifest>(content);

        if (manifest is null)
        {
            logger.LogError("Manifest cannot be deserialized.");
            return false;
        }

        if (manifest.UniqueResources is not null)
        {
            foreach (var uniqueResource in manifest.UniqueResources)
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

                azureClient.ProcessAzureDefinition(new AzurePolicyDefinition(azurePolicy, $"Enforce ard-resource-id {uniqueResource.Name}", $"Enforce ard-resource-id for {uniqueResource.Name}"));

                CreateResourceGroups(
                   resourceGroupNames: uniqueResource.ResourceGroupNames,
                   location: uniqueResource.ResourceGroupLocation,
                   tags: null);
            }
        }

        if (manifest.GroupResources is not null)
        {
            foreach (var groupResource in manifest.GroupResources)
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

                azureClient.ProcessAzureDefinition(new AzurePolicyDefinition(azurePolicy, $"Enforce ard solution specific tags for {groupResource.Name}", $"Enforce ard solution specific tags for {groupResource.Name}"));
            }
        }

        return true;
    }

    private void CreateResourceGroups(List<string> resourceGroupNames, string? location, Dictionary<string, string>? tags)
    {
        if (location is null)
        {
            location = manifest?.ResourceGroupLocation;
        }

        if (location is null)
        {
            throw new Exception("Unexpected that Resource Group location is null.");
        }

        azureClient.CreateResourceGroups(
            resourceGroupNames: resourceGroupNames,
            location: location,
            tags: tags);
    }
}

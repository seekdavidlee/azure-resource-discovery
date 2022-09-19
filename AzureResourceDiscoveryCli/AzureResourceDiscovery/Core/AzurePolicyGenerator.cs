using System.Text.Json;

namespace AzureResourceDiscovery.Core
{
    public class AzurePolicyResult
    {
        public AzurePolicyResult(AzurePolicy azurePolicy, string name, string displayName, string description, List<string> resourceGroupNames)
        {
            AzurePolicy = azurePolicy;
            Name = name;
            DisplayName = displayName;
            Description = description;
            ResourceGroupNames = resourceGroupNames;
        }

        public AzurePolicy AzurePolicy { get; }

        public string Name { get; }

        public string DisplayName { get; }

        public string Description { get; }
        public List<string> ResourceGroupNames { get; }
    }

    public class AzurePolicyGenerator
    {
        public Manifest? Manifest { get; private set; }

        public bool GenerateFiles(string content, Action<AzurePolicyResult> processAzurePolicyResult)
        {
            Manifest = JsonSerializer.Deserialize<Manifest>(content);

            if (Manifest == null) return false;

            if (Manifest.UniqueResources != null)
            {
                foreach (var uniqueResource in Manifest.UniqueResources)
                {
                    AzurePolicy azurePolicy = new();

                    if (uniqueResource.ResourceGroupNames == null) throw new ApplicationException("ResourceGroupNames cannot be null!");
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

                    processAzurePolicyResult(new AzurePolicyResult(azurePolicy, uniqueResource.Name, "Enforce ard-resource-id", $"Enforce ard-resource-id for {uniqueResource.Name}", uniqueResource.ResourceGroupNames));
                }
            }

            if (Manifest.GroupResources != null)
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

                    processAzurePolicyResult(new AzurePolicyResult(azurePolicy, groupResource.Name, "Enforce ard solution specific tags", $"Enforce ard solution specific tags for {groupResource.Name}", groupResource.ResourceGroupNames));
                }
            }

            return true;
        }
    }
}

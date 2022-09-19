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
                    //groupResource.
                }
            }

            return true;
        }
    }
}

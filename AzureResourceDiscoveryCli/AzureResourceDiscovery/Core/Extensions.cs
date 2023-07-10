using Azure.ResourceManager.Resources;

namespace AzureResourceDiscovery.Core;

public static class Extensions
{
    public static bool ApplyTagsIfMissingOrTagValueIfDifferent(this ResourceGroupData resourceGroupData, Dictionary<string, string> tags)
    {
        bool createOrUpdate = false;
        tags.TryAdd(Constants.ArdInternalSolutionId, Constants.ArdInternalSolutionIdValue);

        foreach (var key in tags.Keys)
        {
            if (resourceGroupData.Tags.TryGetValue(key, out string? tagValue))
            {
                if (tagValue != tags[key])
                {
                    resourceGroupData.Tags[key] = tagValue;
                    createOrUpdate = true;
                }
            }
            else
            {
                resourceGroupData.Tags.Add(key, tags[key]);
                createOrUpdate = true;
            }
        }

        return createOrUpdate;
    }
}

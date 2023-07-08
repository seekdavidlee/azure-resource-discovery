namespace AzureResourceDiscovery.Core;

public class AzurePolicyManifest
{
    public string? ResourceGroupLocation { get; set; }

    public List<AzurePolicyManifestItem> Items { get; set; } = new();

    public void Add(AzurePolicyResult azurePolicyResult, string filePath)
    {
        Items.Add(new AzurePolicyManifestItem
        {
            Name = azurePolicyResult.Name,
            DisplayName = azurePolicyResult.DisplayName,
            Description = azurePolicyResult.Description,
            FilePath = filePath,
            ResourceGroupNames = azurePolicyResult.ResourceGroupNames.ToArray()
        });
    }
}

public class AzurePolicyManifestItem
{
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? FilePath { get; set; }
    public string[]? ResourceGroupNames { get; set; }
}

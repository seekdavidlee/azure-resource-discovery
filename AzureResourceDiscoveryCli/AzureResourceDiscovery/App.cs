using AzureResourceDiscovery.Core;

namespace AzureResourceDiscovery;

public class App
{
    private readonly Options options;
    private readonly AzurePolicyGenerator azurePolicyGenerator;

    public App(Options options, AzurePolicyGenerator azurePolicyGenerator)
    {
        this.options = options;
        this.azurePolicyGenerator = azurePolicyGenerator;
    }

    public void Run()
    {
        if (string.IsNullOrEmpty(options.FilePath) || !File.Exists(options.FilePath))
        {
            throw new Exception("Invalid file path!");
        }

        if (!azurePolicyGenerator.Apply(File.ReadAllText(options.FilePath)))
        {
            throw new Exception("Unable to generate Azure Policy(ies)!");
        }
    }
}

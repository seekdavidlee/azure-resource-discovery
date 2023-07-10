using CommandLine;

namespace AzureResourceDiscovery.Core;

public class Options
{
    [Option('f', "filepath", Required = true, HelpText = "File path to manifest file")]
    public string? FilePath { get; set; }
}

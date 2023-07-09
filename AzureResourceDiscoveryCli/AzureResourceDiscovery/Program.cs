using AzureResourceDiscovery.Core;
using CommandLine;

namespace AzureResourceDiscovery;

internal class Program
{
    public class Options
    {
        [Option('f', "filepath", Required = true, HelpText = "File path to manifest file")]
        public string? FilePath { get; set; }
    }

    static int Main(string[] args)
    {
        bool hasErrors = false;
        Parser.Default.ParseArguments<Options>(args).WithParsed(o =>
        {
            if (!string.IsNullOrEmpty(o.FilePath) && File.Exists(o.FilePath))
            {
                var gen = new AzurePolicyGenerator();

                try
                {
                    if (!gen.Apply(File.ReadAllText(o.FilePath)))
                    {
                        hasErrors = true;
                        using TextWriter errorWriter = Console.Error;
                        errorWriter.WriteLine("Unable to generate Azure Policy(ies)!");
                    }
                    else
                    {
                        if (gen.Manifest == null) throw new ApplicationException("Manifest cannot be null!");
                    }
                }
                catch (Exception e)
                {
                    hasErrors = true;
                    using TextWriter errorWriter = Console.Error;
                    errorWriter.WriteLine(e.Message);
                }
            }
            else
            {
                hasErrors = true;
                using TextWriter errorWriter = Console.Error;
                errorWriter.WriteLine("Invalid file path!");
            }
        });

        return hasErrors ? -1 : 0;
    }
}
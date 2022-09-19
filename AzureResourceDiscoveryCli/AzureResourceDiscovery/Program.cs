using AzureResourceDiscovery.Core;
using CommandLine;
using System.Text;
using System.Text.Json;

namespace AzureResourceDiscovery
{
    internal class Program
    {
        public class Options
        {
            [Option('f', "filepath", Required = true, HelpText = "File path to manifest file")]
            public string? FilePath { get; set; }

            [Option('d', "destination", Required = true, HelpText = "Destination directory of where the policy file(s) will be created")]
            public string? DestinationDirectory { get; set; }

            [Option('o', "outputfile", Required = true, HelpText = "Output file which contains info related to all the policy file(s) generated")]
            public string? OutputFilePath { get; set; }
        }

        private static int _counter = 0;
        private static string? _directoryPath;
        private static readonly AzurePolicyManifest _manifest = new();

        private static void ProcessAzurePolicy(AzurePolicyResult azurePolicyResult)
        {
            string filePath = $"{_directoryPath}\\{_counter}.json";
            var content = azurePolicyResult.AzurePolicy.ToString();
            File.WriteAllText(filePath, content);
            _counter += 1;
            _manifest.Add(azurePolicyResult, filePath);

            Console.WriteLine($"Created {filePath}");
        }

        static int Main(string[] args)
        {
            bool hasErrors = false;
            Parser.Default.ParseArguments<Options>(args).WithParsed(o =>
            {
                if (string.IsNullOrEmpty(o.DestinationDirectory) || !Directory.Exists(o.DestinationDirectory))
                {
                    hasErrors = true;
                    using TextWriter errorWriter = Console.Error;
                    errorWriter.WriteLine("Invalid destination directory!");
                    return;
                }

                if (string.IsNullOrEmpty(o.OutputFilePath))
                {
                    hasErrors = true;
                    using TextWriter errorWriter = Console.Error;
                    errorWriter.WriteLine("Invalid output file path!");
                    return;
                }

                if (!o.DestinationDirectory.EndsWith("\\"))
                {
                    o.DestinationDirectory += "\\";
                }

                var directoryPath = $"{o.DestinationDirectory}{DateTime.Now.ToString("MMddHHmmss")}";

                if (!string.IsNullOrEmpty(o.FilePath) && File.Exists(o.FilePath))
                {
                    var gen = new AzurePolicyGenerator();

                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    _directoryPath = directoryPath;

                    try
                    {
                        if (!gen.GenerateFiles(File.ReadAllText(o.FilePath), ProcessAzurePolicy))
                        {
                            hasErrors = true;
                            using TextWriter errorWriter = Console.Error;
                            errorWriter.WriteLine("Unable to generate Azure Policy(ies)!");
                        }
                        else
                        {
                            if (gen.Manifest == null) throw new ApplicationException("Manifest cannot be null!");

                            _manifest.ResourceGroupLocation = gen.Manifest.ResourceGroupLocation;

                            File.WriteAllText(o.OutputFilePath, JsonSerializer.Serialize(_manifest));
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
}
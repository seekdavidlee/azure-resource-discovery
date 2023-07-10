using AzureResourceDiscovery.Core;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureResourceDiscovery;

public partial class Program
{
    static int Main(string[] args)
    {
        return Parser.Default.ParseArguments<Options>(args).MapResult(o =>
        {
            var services = new ServiceCollection();
            services.AddSingleton(sp => o);
            services.AddSingleton<AzurePolicyGenerator>();
            services.AddSingleton<IAzureClient, AzureClient>();
            services.AddLogging(cfg =>
            {
                cfg.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
            });

            var serviceProvider = services.BuildServiceProvider();
            var app = serviceProvider.GetService<App>();

            if (app is null)
            {
                throw new Exception("App cannot be null. This is not expected.");
            }

            app.Run();

            return 0;
        }, (ers) =>
        {
            using TextWriter errorWriter = Console.Error;
            foreach (var er in ers)
            {
                errorWriter.WriteLine(er);
            }

            return -1;
        });
    }
}
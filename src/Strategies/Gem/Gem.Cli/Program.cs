using Gem.Cli.Configuration;
using Gem.Cli.Execution;

namespace Gem.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                string workingDirectory = Directory.GetCurrentDirectory();
                string configPath = Path.Combine(workingDirectory, "config", "gem", "gem.cli.json");

                GemCliConfiguration configuration;

                // Load and validate configuration.
                try
                {
                    var configLoader = new GemConfigLoader(configPath);
                    configuration = configLoader.Load();
                }
                catch (FileNotFoundException ex)
                {
                    Console.Error.WriteLine("Configuration file not found.");
                    Console.Error.WriteLine(ex.Message);
                    return 1;
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine("Configuration error.");
                    Console.Error.WriteLine(ex.Message);
                    return 1;
                }

                // Execute GEM runner with the loaded configuration.
                var runner = new GemRunner(configuration);
                int signalCount = runner.Run();

                Console.WriteLine("StrategyNotifier - GEM CLI");
                Console.WriteLine($"Configuration file       : {configPath}");
                Console.WriteLine($"Data directory           : {configuration.DataDirectory}");
                Console.WriteLine($"Output file              : {configuration.OutputSignalsFile}");
                Console.WriteLine($"Lookback window (months) : {configuration.LookbackMonths}");
                Console.WriteLine($"Generated signals        : {signalCount}");

                return 0;
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine("Input data file not found.");
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "An unexpected error occurred while running the GEM CLI.");

                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}

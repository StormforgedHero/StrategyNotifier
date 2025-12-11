using Gem.Cli.IO;
using Gem.Domain.Core;
using Gem.Domain.Engine;
using Gem.Domain.Model;

namespace Gem.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                string currentDirectory = Directory.GetCurrentDirectory();
                string sampleDataDirectory = Path.Combine(currentDirectory, "data", "gem", "sample");

                if (!Directory.Exists(sampleDataDirectory))
                {
                    Console.Error.WriteLine(
                        $"Sample data directory not found: '{sampleDataDirectory}'.");

                    Console.Error.WriteLine(
                        "Ensure you run the application from the repository root.");

                    return 1;
                }

                var loader = new GemCsvInputLoader(sampleDataDirectory);
                GemInputData input = loader.Load();

                GemParameters parameters = GemParameters.Default;
                var engine = new GemEngine();

                IReadOnlyList<GemSignal> signals = engine.GenerateSignals(input, parameters);

                Console.WriteLine("StrategyNotifier - GEM sample run");
                Console.WriteLine($"Sample data directory   : {sampleDataDirectory}");
                Console.WriteLine($"Lookback window (months): {parameters.LookbackMonths}");
                Console.WriteLine($"Generated signals       : {signals.Count}");
                Console.WriteLine();

                foreach (GemSignal signal in signals)
                {
                    YearMonth period = signal.Period;
                    Console.WriteLine($"{period.Year:D4}-{period.Month:D2}: {signal.Position}");
                }

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "An unexpected error occurred while running the GEM sample.");

                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }
    }
}

using Gem.Cli.Configuration;
using Gem.Cli.Contracts;
using Gem.Cli.IO;
using Gem.Domain.Engine;
using Gem.Domain.Model;
using System.Text.Json;

namespace Gem.Cli.Execution
{
    /// <summary>
    /// Orchestrates loading input data, running the GEM engine
    /// and writing the resulting signals to a JSON file.
    /// </summary>
    public sealed class GemRunner
    {
        private readonly GemCliConfiguration _configuration;

        public GemRunner(GemCliConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Executes the GEM strategy according to the configured settings.
        /// </summary>
        /// <returns>The number of generated signals.</returns>
        public int Run()
        {
            // Load input data from CSV files.
            var csvLoader = new GemCsvInputLoader(_configuration.DataDirectory);

            GemInputData input = csvLoader.Load(
                _configuration.UsEquityFile,
                _configuration.ExUsEquityFile,
                _configuration.SafeAssetFile);

            // Configure GEM engine.
            var parameters = new GemParameters(_configuration.LookbackMonths);
            var engine = new GemEngine();

            IReadOnlyList<GemSignal> signals = engine.GenerateSignals(input, parameters);

            // Serialize signals to JSON file.
            WriteSignals(signals);

            return signals.Count;
        }

        private void WriteSignals(IReadOnlyList<GemSignal> signals)
        {
            var outputs = new List<GemSignalOutput>(signals.Count);

            foreach (GemSignal signal in signals)
            {
                string period = $"{signal.Period.Year:D4}-{signal.Period.Month:D2}";
                string position = signal.Position.ToString();

                outputs.Add(new GemSignalOutput(period, position));
            }

            string outputPath = _configuration.OutputSignalsFile;

            string? directory = Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(outputs, options);

            File.WriteAllText(outputPath, json);
        }
    }
}

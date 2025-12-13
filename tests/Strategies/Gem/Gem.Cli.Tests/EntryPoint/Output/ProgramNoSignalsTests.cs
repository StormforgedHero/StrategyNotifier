using Gem.Cli.Contracts;
using System.Text;
using System.Text.Json;

namespace Gem.Cli.Tests.EntryPoint.Output
{
    public class ProgramNoSignalsTests
    {
        [Fact]
        public void Run_WhenNoSignalsAreGenerated_ReturnsZeroAndWritesEmptySignalsFile()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                // Lookback intentionally exceeds available history => 0 signals is expected and valid.
                CreateValidConfiguration(rootDirectory, lookbackMonths: 12);

                string dataDirectory = Path.Combine(rootDirectory, "data", "gem", "sample");
                Directory.CreateDirectory(dataDirectory);

                // Only 3 months of data.
                WriteCsv(
                    dataDirectory,
                    "us-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.02
                    2025,2,0.03
                    2025,3,-0.01
                    """);

                WriteCsv(
                    dataDirectory,
                    "exus-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.01
                    2025,2,0.02
                    2025,3,0.00
                    """);

                WriteCsv(
                    dataDirectory,
                    "safe-asset.csv",
                    """
                    Year,Month,Return
                    2025,1,0.002
                    2025,2,0.002
                    2025,3,0.002
                    """);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(0, exitCode);
                Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

                string outputPath = Path.Combine(rootDirectory, "dist", "gem", "signals.json");
                Assert.True(File.Exists(outputPath));

                string json = File.ReadAllText(outputPath, Encoding.UTF8);

                GemSignalOutput[]? signals =
                    JsonSerializer.Deserialize<GemSignalOutput[]>(json);

                Assert.NotNull(signals);
                Assert.Empty(signals!);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenNoSignalsAreGenerated_WritesSummaryWithZeroSignals()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 12);

                string dataDirectory = Path.Combine(rootDirectory, "data", "gem", "sample");
                Directory.CreateDirectory(dataDirectory);

                WriteCsv(
                    dataDirectory,
                    "us-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.02
                    2025,2,0.03
                    2025,3,-0.01
                    """);

                WriteCsv(
                    dataDirectory,
                    "exus-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.01
                    2025,2,0.02
                    2025,3,0.00
                    """);

                WriteCsv(
                    dataDirectory,
                    "safe-asset.csv",
                    """
                    Year,Month,Return
                    2025,1,0.002
                    2025,2,0.002
                    2025,3,0.002
                    """);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(0, exitCode);

                string output = outWriter.ToString();
                Assert.Contains("StrategyNotifier - GEM CLI", output, StringComparison.Ordinal);
                Assert.Contains("Generated signals", output, StringComparison.Ordinal);
                Assert.Contains("Generated signals        : 0", output, StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        private static void CreateValidConfiguration(string rootDirectory, int lookbackMonths)
        {
            string configDirectory = Path.Combine(rootDirectory, "config", "gem");
            Directory.CreateDirectory(configDirectory);

            string configPath = Path.Combine(configDirectory, "gem.cli.json");

            string jsonConfig = $@"{{
  ""dataDirectory"": ""data/gem/sample"",
  ""usEquityFile"": ""us-equity.csv"",
  ""exUsEquityFile"": ""exus-equity.csv"",
  ""safeAssetFile"": ""safe-asset.csv"",
  ""outputSignalsFile"": ""dist/gem/signals.json"",
  ""lookbackMonths"": {lookbackMonths}
}}";

            File.WriteAllText(
                configPath,
                jsonConfig,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void WriteCsv(string directory, string fileName, string content)
        {
            Directory.CreateDirectory(directory);

            string fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(
                fullPath,
                content.Trim() + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static void DeleteDirectoryIfExists(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            if (!Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }
}

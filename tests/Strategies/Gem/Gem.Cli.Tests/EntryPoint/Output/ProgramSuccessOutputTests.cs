using System.Text;

namespace Gem.Cli.Tests.EntryPoint.Output
{
    public class ProgramSuccessOutputTests
    {
        [Fact]
        public void Run_WhenSuccessful_WritesExpectedSummaryToOutput_AndDoesNotWriteToError()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 2);
                CreateSampleDataFiles(rootDirectory);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(0, exitCode);
                Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

                string output = outWriter.ToString();

                string expectedConfigPath = Path.Combine(rootDirectory, "config", "gem", "gem.cli.json");
                string expectedDataDirectory = Path.Combine(rootDirectory, "data", "gem", "sample");
                string expectedOutputFile = Path.Combine(rootDirectory, "dist", "gem", "signals.json");

                Assert.Contains("StrategyNotifier - GEM CLI", output, StringComparison.Ordinal);
                Assert.Contains($"Configuration file       : {expectedConfigPath}", output, StringComparison.Ordinal);
                Assert.Contains($"Data directory           : {expectedDataDirectory}", output, StringComparison.Ordinal);
                Assert.Contains($"Output file              : {expectedOutputFile}", output, StringComparison.Ordinal);
                Assert.Contains("Lookback window (months) : 2", output, StringComparison.Ordinal);
                Assert.Contains("Generated signals        : 2", output, StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenSuccessful_PrintsAbsolutePaths_ForDataDirectoryAndOutputFile()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 2);
                CreateSampleDataFiles(rootDirectory);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(0, exitCode);

                string output = outWriter.ToString();

                string dataPrefix = "Data directory           : ";
                string outputPrefix = "Output file              : ";

                string dataLine = FindLineStartingWith(output, dataPrefix);
                string outputLine = FindLineStartingWith(output, outputPrefix);

                string dataPath = dataLine.Substring(dataPrefix.Length).Trim();
                string outputPath = outputLine.Substring(outputPrefix.Length).Trim();

                Assert.True(Path.IsPathRooted(dataPath));
                Assert.True(Path.IsPathRooted(outputPath));
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenSuccessful_WritesSummaryLinesInExpectedOrder()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 2);
                CreateSampleDataFiles(rootDirectory);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(0, exitCode);

                string output = outWriter.ToString();

                int headerIndex = output.IndexOf("StrategyNotifier - GEM CLI", StringComparison.Ordinal);
                int configIndex = output.IndexOf("Configuration file", StringComparison.Ordinal);
                int dataIndex = output.IndexOf("Data directory", StringComparison.Ordinal);
                int outIndex = output.IndexOf("Output file", StringComparison.Ordinal);
                int lookbackIndex = output.IndexOf("Lookback window (months)", StringComparison.Ordinal);
                int signalsIndex = output.IndexOf("Generated signals", StringComparison.Ordinal);

                Assert.True(headerIndex >= 0);
                Assert.True(configIndex > headerIndex);
                Assert.True(dataIndex > configIndex);
                Assert.True(outIndex > dataIndex);
                Assert.True(lookbackIndex > outIndex);
                Assert.True(signalsIndex > lookbackIndex);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        private static string FindLineStartingWith(string text, string prefix)
        {
            using var reader = new StringReader(text);

            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return line;
                }
            }

            throw new InvalidOperationException($"Expected a line starting with '{prefix}'.");
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

        private static void CreateSampleDataFiles(string rootDirectory)
        {
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

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
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

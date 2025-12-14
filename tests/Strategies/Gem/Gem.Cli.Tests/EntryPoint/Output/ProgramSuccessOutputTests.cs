using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.EntryPoint.Output
{
    public class ProgramSuccessOutputTests
    {
        [Fact]
        public void Run_WhenSuccessful_WritesExpectedSummaryToOutput_AndDoesNotWriteToError()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);
            CreateSampleDataFiles(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

            string output = outWriter.ToString();

            string expectedConfigPath = workspace.GetPath("config", "gem", "gem.cli.json");
            string expectedDataDirectory = workspace.GetPath("data", "gem", "sample");
            string expectedOutputFile = workspace.GetPath("dist", "gem", "signals.json");

            Assert.Contains("StrategyNotifier - GEM CLI", output, StringComparison.Ordinal);
            Assert.Contains($"Configuration file       : {expectedConfigPath}", output, StringComparison.Ordinal);
            Assert.Contains($"Data directory           : {expectedDataDirectory}", output, StringComparison.Ordinal);
            Assert.Contains($"Output file              : {expectedOutputFile}", output, StringComparison.Ordinal);
            Assert.Contains("Lookback window (months) : 2", output, StringComparison.Ordinal);
            Assert.Contains("Generated signals        : 2", output, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WhenSuccessful_PrintsAbsolutePaths_ForDataDirectoryAndOutputFile()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);
            CreateSampleDataFiles(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

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

        [Fact]
        public void Run_WhenSuccessful_WritesSummaryLinesInExpectedOrder()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);
            CreateSampleDataFiles(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

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

        private static void CreateValidConfiguration(TemporaryWorkspace workspace, int lookbackMonths)
        {
            string jsonConfig = $@"{{
  ""dataDirectory"": ""data/gem/sample"",
  ""usEquityFile"": ""us-equity.csv"",
  ""exUsEquityFile"": ""exus-equity.csv"",
  ""safeAssetFile"": ""safe-asset.csv"",
  ""outputSignalsFile"": ""dist/gem/signals.json"",
  ""lookbackMonths"": {lookbackMonths}
}}";

            workspace.WriteText(jsonConfig, "config", "gem", "gem.cli.json");
        }

        private static void CreateSampleDataFiles(TemporaryWorkspace workspace)
        {
            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.02
                2025,2,0.03
                2025,3,-0.01
                """,
                "data", "gem", "sample", "us-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.01
                2025,2,0.02
                2025,3,0.00
                """,
                "data", "gem", "sample", "exus-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.002
                2025,2,0.002
                2025,3,0.002
                """,
                "data", "gem", "sample", "safe-asset.csv");
        }
    }
}

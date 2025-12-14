using Gem.Cli.Contracts;
using Gem.Cli.Tests.TestSupport;
using System.Text.Json;

namespace Gem.Cli.Tests.EntryPoint.Output
{
    public class ProgramNoSignalsTests
    {
        [Fact]
        public void Run_WhenNoSignalsAreGenerated_ReturnsZeroAndWritesEmptySignalsFile()
        {
            using var workspace = new TemporaryWorkspace();

            // Lookback intentionally exceeds available history => 0 signals expected.
            CreateValidConfiguration(workspace, lookbackMonths: 12);

            WriteThreeMonthSampleData(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

            string outputPath = workspace.GetPath("dist", "gem", "signals.json");
            Assert.True(File.Exists(outputPath));

            string json = File.ReadAllText(outputPath, Utf8TestEncoding.Utf8NoBom);

            GemSignalOutput[]? signals =
                JsonSerializer.Deserialize<GemSignalOutput[]>(json);

            Assert.NotNull(signals);
            Assert.Empty(signals!);
        }

        [Fact]
        public void Run_WhenNoSignalsAreGenerated_WritesSummaryWithZeroSignals()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 12);
            WriteThreeMonthSampleData(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(0, exitCode);

            string output = outWriter.ToString();
            Assert.Contains("StrategyNotifier - GEM CLI", output, StringComparison.Ordinal);
            Assert.Contains("Generated signals", output, StringComparison.Ordinal);
            Assert.Contains("Generated signals        : 0", output, StringComparison.Ordinal);
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

        private static void WriteThreeMonthSampleData(TemporaryWorkspace workspace)
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

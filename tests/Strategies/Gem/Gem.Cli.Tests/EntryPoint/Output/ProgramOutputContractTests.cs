using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.EntryPoint.Output
{
    public class ProgramOutputContractTests
    {
        [Fact]
        public void Run_OnSuccess_WritesExpectedSummaryLinesToStdout()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);
            CreateSampleDataFiles(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

            string stdout = outWriter.ToString();
            Assert.False(string.IsNullOrWhiteSpace(stdout));

            string[] lines = SplitNonEmptyLines(stdout);

            Assert.Equal("StrategyNotifier - GEM CLI", lines[0]);

            string configPath = GetValue(lines, "Configuration file");
            string dataDirectory = GetValue(lines, "Data directory");
            string outputFile = GetValue(lines, "Output file");
            string lookback = GetValue(lines, "Lookback window (months)");
            string generated = GetValue(lines, "Generated signals");

            Assert.True(Path.IsPathRooted(configPath));
            Assert.True(Path.IsPathRooted(dataDirectory));
            Assert.True(Path.IsPathRooted(outputFile));

            Assert.Contains(
                Path.Combine("config", "gem", "gem.cli.json"),
                configPath,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                Path.Combine("data", "gem", "sample"),
                dataDirectory,
                StringComparison.OrdinalIgnoreCase);

            Assert.Contains(
                Path.Combine("dist", "gem", "signals.json"),
                outputFile,
                StringComparison.OrdinalIgnoreCase);

            Assert.Equal("2", lookback);

            Assert.True(int.TryParse(generated, out int signalCount));
            Assert.True(signalCount > 0);
        }

        [Fact]
        public void Run_OnSuccess_DoesNotWriteErrorsToStderr()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);
            CreateSampleDataFiles(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));
        }

        private static string[] SplitNonEmptyLines(string text)
        {
            return text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToArray();
        }

        private static string GetValue(string[] lines, string label)
        {
            foreach (string line in lines)
            {
                int colonIndex = line.IndexOf(':', StringComparison.Ordinal);
                if (colonIndex < 0)
                {
                    continue;
                }

                string left = line[..colonIndex].Trim();
                if (!string.Equals(left, label, StringComparison.Ordinal))
                {
                    continue;
                }

                string value = line[(colonIndex + 1)..].Trim();
                Assert.False(string.IsNullOrWhiteSpace(value));

                return value;
            }

            throw new InvalidOperationException($"Expected output line with label '{label}' was not found.");
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

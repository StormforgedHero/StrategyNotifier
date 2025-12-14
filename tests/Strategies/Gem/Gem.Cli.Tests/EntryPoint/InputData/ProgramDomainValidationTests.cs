using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.EntryPoint.InputData
{
    public class ProgramDomainValidationTests
    {
        [Fact]
        public void Run_WhenCsvContainsDuplicatePeriods_ReturnsNonZeroAndWritesInputDataValidationError()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.02
                2025,1,0.01
                2025,2,0.03
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

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.NotEqual(0, exitCode);

            string error = errWriter.ToString();
            Assert.Contains("Input data validation error", error, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(error));
        }

        [Fact]
        public void Run_WhenCsvContainsDuplicatePeriods_DoesNotCreateSignalsFile()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.02
                2025,1,0.01
                2025,2,0.03
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

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.NotEqual(0, exitCode);

            string outputPath = workspace.GetPath("dist", "gem", "signals.json");
            Assert.False(File.Exists(outputPath));
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
    }
}

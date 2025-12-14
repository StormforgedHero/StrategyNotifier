using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.EntryPoint.InputData
{
    public class ProgramInputDataFormatTests
    {
        [Fact]
        public void Main_WithInvalidCsvReturnValue_ReturnsNonZeroAndWritesInputDataFormatError()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,not-a-number
                """,
                "data", "gem", "sample", "us-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.01
                """,
                "data", "gem", "sample", "exus-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.002
                """,
                "data", "gem", "sample", "safe-asset.csv");

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.NotEqual(0, exitCode);

            string error = errWriter.ToString();
            Assert.Contains("Input data format error", error, StringComparison.Ordinal);
            Assert.Contains("Invalid return value", error, StringComparison.Ordinal);
        }

        [Fact]
        public void Main_WithInvalidCsvHeader_ReturnsNonZeroAndWritesInputDataFormatError()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);

            workspace.WriteCsv(
                """
                BadHeader1,BadHeader2,BadHeader3
                2025,1,0.02
                """,
                "data", "gem", "sample", "us-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.01
                """,
                "data", "gem", "sample", "exus-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.002
                """,
                "data", "gem", "sample", "safe-asset.csv");

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.NotEqual(0, exitCode);

            string error = errWriter.ToString();
            Assert.Contains("Input data format error", error, StringComparison.Ordinal);
            Assert.Contains("Invalid CSV header", error, StringComparison.Ordinal);
        }

        [Fact]
        public void Main_WithMonthOutOfRange_ReturnsNonZeroAndWritesInputDataFormatError()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,13,0.02
                """,
                "data", "gem", "sample", "us-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.01
                """,
                "data", "gem", "sample", "exus-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.002
                """,
                "data", "gem", "sample", "safe-asset.csv");

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.NotEqual(0, exitCode);

            string error = errWriter.ToString();
            Assert.Contains("Input data format error", error, StringComparison.Ordinal);
            Assert.Contains("Invalid month value", error, StringComparison.Ordinal);
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

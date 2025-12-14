using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.EntryPoint
{
    public class ProgramOutputWriteErrorTests
    {
        [Fact]
        public void Run_WhenSignalsJsonCannotBeWritten_ReturnsOutputWriteExitCode()
        {
            using var workspace = new TemporaryWorkspace();

            string jsonConfig =
                """
                {
                  "dataDirectory": "data/gem/sample",
                  "usEquityFile": "us-equity.csv",
                  "exUsEquityFile": "exus-equity.csv",
                  "safeAssetFile": "safe-asset.csv",
                  "outputSignalsFile": "dist/gem/signals.json",
                  "lookbackMonths": 2
                }
                """;

            workspace.WriteText(jsonConfig, "config", "gem", "gem.cli.json");

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

            // Force output write failure deterministically by creating a directory at the target file path.
            string outputPathAsDirectory = workspace.GetPath("dist", "gem", "signals.json");
            Directory.CreateDirectory(outputPathAsDirectory);

            using var outputWriter = new StringWriter();
            using var errorWriter = new StringWriter();

            int exitCode = global::Gem.Cli.Program.Run(
                workingDirectory: workspace.Root,
                output: outputWriter,
                error: errorWriter);

            Assert.Equal(30, exitCode);

            string error = errorWriter.ToString();
            Assert.Contains("Output write error", error, StringComparison.Ordinal);
            Assert.Contains("signals", error, StringComparison.OrdinalIgnoreCase);
        }
    }
}

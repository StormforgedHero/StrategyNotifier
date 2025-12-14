using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.EntryPoint.Errors
{
    public class ProgramIoErrorTests
    {
        [Fact]
        public void Run_WhenInputFileIsLocked_ReturnsNonZeroAndWritesIoError()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);
            CreateSampleDataFiles(workspace);

            string lockedInputPath = workspace.GetPath("data", "gem", "sample", "us-equity.csv");

            using var lockStream = new FileStream(
                lockedInputPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(Cli.Program.ExitCodeIoError, exitCode);

            string error = errWriter.ToString();
            Assert.Contains("I/O error", error, StringComparison.Ordinal);
            Assert.Contains("us-equity.csv", error, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Output write error", error, StringComparison.Ordinal);

            string outputPath = workspace.GetPath("dist", "gem", "signals.json");
            Assert.False(File.Exists(outputPath));
        }

        [Fact]
        public void Run_WhenOutputSignalsFileIsLocked_ReturnsNonZeroAndWritesOutputWriteError()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);
            CreateSampleDataFiles(workspace);

            string outputDirectory = workspace.GetPath("dist", "gem");
            Directory.CreateDirectory(outputDirectory);

            string outputPath = Path.Combine(outputDirectory, "signals.json");

            File.WriteAllText(
                outputPath,
                "[]\n",
                Utf8TestEncoding.Utf8NoBom);

            using var lockStream = new FileStream(
                outputPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(Cli.Program.ExitCodeOutputWriteError, exitCode);

            string error = errWriter.ToString();
            Assert.Contains("Output write error", error, StringComparison.Ordinal);
            Assert.Contains("signals.json", error, StringComparison.OrdinalIgnoreCase);
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

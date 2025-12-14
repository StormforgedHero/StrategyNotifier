using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.EntryPoint.Output
{
    public class ProgramOutputWriteTests
    {
        [Fact]
        public void Run_WhenOutputSignalsFilePathIsDirectory_ReturnsNonZeroAndWritesOutputWriteError()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);
            CreateSampleDataFiles(workspace);

            // Force output path to be a DIRECTORY, not a file.
            string outputPathAsDirectory = workspace.GetPath("dist", "gem", "signals.json");
            Directory.CreateDirectory(outputPathAsDirectory);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.NotEqual(0, exitCode);

            string error = errWriter.ToString();
            Assert.Contains("Output write error", error, StringComparison.Ordinal);
            Assert.Contains("signals.json", error, StringComparison.OrdinalIgnoreCase);

            Assert.True(Directory.Exists(outputPathAsDirectory));
            Assert.False(File.Exists(outputPathAsDirectory));
        }

        [Fact]
        public void SignalsJson_IsWrittenAsUtf8WithoutBom()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);
            CreateSampleDataFiles(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

            string outputPath = workspace.GetPath("dist", "gem", "signals.json");
            Assert.True(File.Exists(outputPath));

            byte[] bytes = File.ReadAllBytes(outputPath);

            bool hasBom = bytes.Length >= 3
                && bytes[0] == 0xEF
                && bytes[1] == 0xBB
                && bytes[2] == 0xBF;

            Assert.False(hasBom, "signals.json should be written as UTF-8 without BOM.");
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

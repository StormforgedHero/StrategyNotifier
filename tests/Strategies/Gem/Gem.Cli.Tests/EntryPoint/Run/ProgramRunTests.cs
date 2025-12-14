using Gem.Cli.Contracts;
using Gem.Cli.Tests.TestSupport;
using System.Text.Json;

namespace Gem.Cli.Tests.EntryPoint.Flow
{
    public class ProgramRunTests
    {
        [Fact]
        public void Run_WhenWorkingDirectoryIsNull_ThrowsArgumentNullException()
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            var exception = Assert.Throws<ArgumentNullException>(
                () => Cli.Program.Run(workingDirectory: null!, outWriter, errWriter));

            Assert.Equal("workingDirectory", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Run_WhenWorkingDirectoryIsEmptyOrWhitespace_ThrowsArgumentException(string workingDirectory)
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            var exception = Assert.Throws<ArgumentException>(
                () => Cli.Program.Run(workingDirectory, outWriter, errWriter));

            Assert.Equal("workingDirectory", exception.ParamName);
        }

        [Fact]
        public void Run_WhenOutputWriterIsNull_ThrowsArgumentNullException()
        {
            using var errWriter = new StringWriter();
            using var workspace = new TemporaryWorkspace();

            var exception = Assert.Throws<ArgumentNullException>(
                () => Cli.Program.Run(workspace.Root, output: null!, error: errWriter));

            Assert.Equal("output", exception.ParamName);
        }

        [Fact]
        public void Run_WhenErrorWriterIsNull_ThrowsArgumentNullException()
        {
            using var outWriter = new StringWriter();
            using var workspace = new TemporaryWorkspace();

            var exception = Assert.Throws<ArgumentNullException>(
                () => Cli.Program.Run(workspace.Root, output: outWriter, error: null!));

            Assert.Equal("error", exception.ParamName);
        }

        [Fact]
        public void Run_WhenConfigurationFileIsMissing_ReturnsNonZeroAndWritesConfigurationFileNotFound()
        {
            using var workspace = new TemporaryWorkspace();

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.NotEqual(0, exitCode);
            Assert.Contains("Configuration file not found", errWriter.ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void Run_WhenConfigurationJsonIsInvalid_ReturnsNonZeroAndWritesConfigurationError()
        {
            using var workspace = new TemporaryWorkspace();

            WriteConfigurationFile(
                workspace,
                """
                {
                  "dataDirectory": "data/gem/sample"
                """); // Missing closing brace -> invalid JSON.

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.NotEqual(0, exitCode);

            string error = errWriter.ToString();
            Assert.Contains("Configuration error", error, StringComparison.Ordinal);
            Assert.Contains("invalid JSON", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Run_DoesNotChangeProcessCurrentDirectory_OnSuccessfulRun()
        {
            using var runnerWorkspace = new TemporaryWorkspace();
            using var rootWorkspace = new TemporaryWorkspace();

            using (new CurrentDirectoryScope(runnerWorkspace.Root))
            {
                CreateConfigurationFile(rootWorkspace, lookbackMonths: 2);
                CreateSampleDataFiles(rootWorkspace);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootWorkspace.Root, outWriter, errWriter);

                Assert.Equal(0, exitCode);
                Assert.Equal(runnerWorkspace.Root, Directory.GetCurrentDirectory());
            }
        }

        [Fact]
        public void Run_WithRelativePaths_NormalizesAndCreatesSignalsUnderWorkingDirectory()
        {
            using var workspace = new TemporaryWorkspace();

            CreateConfigurationFile(workspace, lookbackMonths: 2);
            CreateSampleDataFiles(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

            string expectedOutputPath = workspace.GetPath("dist", "gem", "signals.json");
            Assert.True(File.Exists(expectedOutputPath));

            string json = File.ReadAllText(expectedOutputPath, Utf8TestEncoding.Utf8NoBom);

            GemSignalOutput[]? signals =
                JsonSerializer.Deserialize<GemSignalOutput[]>(json);

            Assert.NotNull(signals);
            Assert.NotEmpty(signals!);
        }

        [Fact]
        public void Run_WhenInputDataFileIsMissing_ReturnsNonZeroAndWritesInputDataFileNotFound()
        {
            using var workspace = new TemporaryWorkspace();

            CreateConfigurationFile(workspace, lookbackMonths: 2);

            string dataDirectory = workspace.GetPath("data", "gem", "sample");
            Directory.CreateDirectory(dataDirectory);

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.02
                """,
                "data", "gem", "sample", "us-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.01
                """,
                "data", "gem", "sample", "exus-equity.csv");

            // safe-asset.csv intentionally missing.

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.NotEqual(0, exitCode);

            string error = errWriter.ToString();
            Assert.Contains("Input data file not found", error, StringComparison.Ordinal);
            Assert.Contains("safe-asset.csv", error, StringComparison.OrdinalIgnoreCase);

            string outputPath = workspace.GetPath("dist", "gem", "signals.json");
            Assert.False(File.Exists(outputPath));
        }

        [Fact]
        public void Run_WhenLookbackExceedsAvailableData_ReturnsZeroAndWritesEmptySignalsFile()
        {
            using var workspace = new TemporaryWorkspace();

            CreateConfigurationFile(workspace, lookbackMonths: 12);
            CreateSampleDataFiles(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

            string output = outWriter.ToString();
            Assert.Contains("Generated signals", output, StringComparison.Ordinal);
            Assert.Contains("0", output, StringComparison.Ordinal);

            string outputPath = workspace.GetPath("dist", "gem", "signals.json");
            Assert.True(File.Exists(outputPath));

            string json = File.ReadAllText(outputPath, Utf8TestEncoding.Utf8NoBom);
            Assert.Equal("[]", json.Trim());
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

        private static void WriteConfigurationFile(TemporaryWorkspace workspace, string jsonConfig)
        {
            workspace.WriteText(jsonConfig, "config", "gem", "gem.cli.json");
        }

        private static void CreateConfigurationFile(TemporaryWorkspace workspace, int lookbackMonths)
        {
            // Intentionally relative paths to verify normalization inside Program.Run.
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

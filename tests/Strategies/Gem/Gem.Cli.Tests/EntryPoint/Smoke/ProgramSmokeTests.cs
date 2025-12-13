using Gem.Cli.Contracts;
using Gem.Cli.Tests.TestSupport;
using System.Text.Json;

namespace Gem.Cli.Tests.EntryPoint.Smoke
{
    public class ProgramSmokeTests
    {
        [Fact]
        public void Main_WhenConfigurationFileDoesNotExist_ReturnsNonZeroAndWritesError()
        {
            using var context = ProgramTestContext.Create();

            ProgramRunResult result = context.Run();

            Assert.NotEqual(0, result.ExitCode);

            Assert.Contains(
                "Configuration file not found",
                result.Error,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Main_WithValidConfigurationAndData_ReturnsZeroAndCreatesSignalsFile()
        {
            using var context = ProgramTestContext.Create();
            context.WriteValidConfig(lookbackMonths: 2);
            context.WriteDefaultSampleData();

            ProgramRunResult result = context.Run();

            Assert.Equal(0, result.ExitCode);

            Assert.True(string.IsNullOrWhiteSpace(result.Error));
            Assert.Contains("StrategyNotifier - GEM CLI", result.Output, StringComparison.Ordinal);
            Assert.Contains("Generated signals", result.Output, StringComparison.Ordinal);

            Assert.True(File.Exists(context.OutputPath));

            string json = TestFileSystem.ReadAllText(context.OutputPath);
            GemSignalOutput[]? signals =
                JsonSerializer.Deserialize<GemSignalOutput[]>(json);

            Assert.NotNull(signals);
            Assert.NotEmpty(signals!);
        }

        [Fact]
        public void Main_WithInvalidConfiguration_ReturnsNonZeroAndWritesConfigurationError()
        {
            using var context = ProgramTestContext.Create();

            // lookbackMonths = 0 -> should fail validation in config loader
            context.WriteValidConfig(lookbackMonths: 0);

            ProgramRunResult result = context.Run();

            Assert.NotEqual(0, result.ExitCode);

            Assert.Contains("Configuration error", result.Error, StringComparison.Ordinal);
            Assert.Contains("lookbackMonths", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public void Main_WithInvalidConfigurationJson_ReturnsNonZeroAndWritesConfigurationError()
        {
            using var context = ProgramTestContext.Create();

            context.WriteInvalidJsonConfig("""
            {
              "dataDirectory": "data/gem/sample"
            """);

            ProgramRunResult result = context.Run();

            Assert.NotEqual(0, result.ExitCode);

            Assert.Contains("Configuration error", result.Error, StringComparison.Ordinal);
            Assert.Contains("invalid JSON", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Main_WithMissingInputDataFile_ReturnsNonZeroAndWritesDataFileError()
        {
            using var context = ProgramTestContext.Create();

            context.WriteValidConfig(lookbackMonths: 2);
            context.WriteDataMissingSafeAssetFile();

            ProgramRunResult result = context.Run();

            Assert.NotEqual(0, result.ExitCode);

            Assert.Contains("Input data file not found", result.Error, StringComparison.Ordinal);
            Assert.Contains("safe-asset.csv", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ProgramTestContext : IDisposable
        {
            private const string DefaultDataDirectoryRelative = "data/gem/sample";
            private const string DefaultOutputSignalsFileRelative = "dist/gem/signals.json";

            public string RootDirectory { get; }
            public string DataDirectory { get; }
            public string OutputPath { get; }

            private ProgramTestContext(string rootDirectory)
            {
                RootDirectory = rootDirectory;
                DataDirectory = Path.Combine(rootDirectory, "data", "gem", "sample");
                OutputPath = Path.Combine(rootDirectory, "dist", "gem", "signals.json");
            }

            public static ProgramTestContext Create()
            {
                string root = TestFileSystem.CreateTemporaryDirectory();
                return new ProgramTestContext(root);
            }

            public ProgramRunResult Run()
            {
                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Program.Run(RootDirectory, outWriter, errWriter);

                return new ProgramRunResult(
                    exitCode,
                    outWriter.ToString(),
                    errWriter.ToString());
            }

            public void WriteValidConfig(int lookbackMonths)
            {
                GemCliTestData.WriteGemCliJsonConfig(
                    RootDirectory,
                    lookbackMonths: lookbackMonths,
                    dataDirectory: DefaultDataDirectoryRelative,
                    outputSignalsFile: DefaultOutputSignalsFileRelative);
            }

            public void WriteInvalidJsonConfig(string jsonContent)
            {
                string configDirectory = Path.Combine(RootDirectory, "config", "gem");
                Directory.CreateDirectory(configDirectory);

                TestFileSystem.WriteTextFile(configDirectory, "gem.cli.json", jsonContent);
            }

            public void WriteDefaultSampleData()
            {
                GemCliTestData.WriteDefaultSampleCsvs(DataDirectory);
            }

            public void WriteDataMissingSafeAssetFile()
            {
                GemCliTestData.WriteDefaultSampleCsvs(DataDirectory);

                string safeAssetPath = Path.Combine(DataDirectory, "safe-asset.csv");
                File.Delete(safeAssetPath);
            }

            public void Dispose()
            {
                TestFileSystem.DeleteDirectoryIfExists(RootDirectory);
            }
        }

        private sealed record ProgramRunResult(int ExitCode, string Output, string Error);
    }
}

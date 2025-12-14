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

            string json = File.ReadAllText(context.OutputPath, Utf8TestEncoding.Utf8NoBom);
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

    }
}

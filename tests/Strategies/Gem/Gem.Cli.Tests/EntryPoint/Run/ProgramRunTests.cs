using Gem.Cli.Contracts;
using System.Text;
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

            var exception = Assert.Throws<ArgumentNullException>(
                () => Cli.Program.Run(CreateTemporaryDirectory(), output: null!, error: errWriter));

            Assert.Equal("output", exception.ParamName);
        }

        [Fact]
        public void Run_WhenErrorWriterIsNull_ThrowsArgumentNullException()
        {
            using var outWriter = new StringWriter();

            var exception = Assert.Throws<ArgumentNullException>(
                () => Cli.Program.Run(CreateTemporaryDirectory(), output: outWriter, error: null!));

            Assert.Equal("error", exception.ParamName);
        }

        [Fact]
        public void Run_WhenConfigurationFileIsMissing_ReturnsNonZeroAndWritesConfigurationFileNotFound()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.NotEqual(0, exitCode);
                Assert.Contains("Configuration file not found", errWriter.ToString(), StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenConfigurationJsonIsInvalid_ReturnsNonZeroAndWritesConfigurationError()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                WriteConfigurationFile(
                    rootDirectory,
                    """
                    {
                      "dataDirectory": "data/gem/sample"
                    """); // Missing closing brace -> invalid JSON.

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.NotEqual(0, exitCode);

                string error = errWriter.ToString();
                Assert.Contains("Configuration error", error, StringComparison.Ordinal);
                Assert.Contains("invalid JSON", error, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_DoesNotChangeProcessCurrentDirectory_OnSuccessfulRun()
        {
            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            string runnerCurrentDirectory = CreateTemporaryDirectory();
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                Directory.SetCurrentDirectory(runnerCurrentDirectory);

                CreateConfigurationFile(rootDirectory, lookbackMonths: 2);
                CreateSampleDataFiles(rootDirectory);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(0, exitCode);
                Assert.Equal(runnerCurrentDirectory, Directory.GetCurrentDirectory());
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);
                DeleteDirectoryIfExists(runnerCurrentDirectory);
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WithRelativePaths_NormalizesAndCreatesSignalsUnderWorkingDirectory()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateConfigurationFile(rootDirectory, lookbackMonths: 2);
                CreateSampleDataFiles(rootDirectory);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(0, exitCode);
                Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

                string expectedOutputPath = Path.Combine(rootDirectory, "dist", "gem", "signals.json");
                Assert.True(File.Exists(expectedOutputPath));

                string json = File.ReadAllText(expectedOutputPath, Encoding.UTF8);

                GemSignalOutput[]? signals =
                    JsonSerializer.Deserialize<GemSignalOutput[]>(json);

                Assert.NotNull(signals);
                Assert.NotEmpty(signals!);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenInputDataFileIsMissing_ReturnsNonZeroAndWritesInputDataFileNotFound()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateConfigurationFile(rootDirectory, lookbackMonths: 2);

                string dataDirectory = Path.Combine(rootDirectory, "data", "gem", "sample");
                Directory.CreateDirectory(dataDirectory);

                WriteCsv(
                    dataDirectory,
                    "us-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.02
                    """);

                WriteCsv(
                    dataDirectory,
                    "exus-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.01
                    """);

                // safe-asset.csv intentionally missing.

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.NotEqual(0, exitCode);

                string error = errWriter.ToString();
                Assert.Contains("Input data file not found", error, StringComparison.Ordinal);
                Assert.Contains("safe-asset.csv", error, StringComparison.OrdinalIgnoreCase);

                string outputPath = Path.Combine(rootDirectory, "dist", "gem", "signals.json");
                Assert.False(File.Exists(outputPath));
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenLookbackExceedsAvailableData_ReturnsZeroAndWritesEmptySignalsFile()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateConfigurationFile(rootDirectory, lookbackMonths: 12);
                CreateSampleDataFiles(rootDirectory);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(0, exitCode);
                Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

                string output = outWriter.ToString();
                Assert.Contains("Generated signals", output, StringComparison.Ordinal);
                Assert.Contains("0", output, StringComparison.Ordinal);

                string outputPath = Path.Combine(rootDirectory, "dist", "gem", "signals.json");
                Assert.True(File.Exists(outputPath));

                string json = File.ReadAllText(outputPath, Encoding.UTF8);
                Assert.Equal("[]", json.Trim());
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        private static void CreateSampleDataFiles(string rootDirectory)
        {
            string dataDirectory = Path.Combine(rootDirectory, "data", "gem", "sample");
            Directory.CreateDirectory(dataDirectory);

            WriteCsv(
                dataDirectory,
                "us-equity.csv",
                """
                Year,Month,Return
                2025,1,0.02
                2025,2,0.03
                2025,3,-0.01
                """);

            WriteCsv(
                dataDirectory,
                "exus-equity.csv",
                """
                Year,Month,Return
                2025,1,0.01
                2025,2,0.02
                2025,3,0.00
                """);

            WriteCsv(
                dataDirectory,
                "safe-asset.csv",
                """
                Year,Month,Return
                2025,1,0.002
                2025,2,0.002
                2025,3,0.002
                """);
        }

        private static void WriteConfigurationFile(string rootDirectory, string jsonConfig)
        {
            string configDirectory = Path.Combine(rootDirectory, "config", "gem");
            Directory.CreateDirectory(configDirectory);

            string configPath = Path.Combine(configDirectory, "gem.cli.json");

            File.WriteAllText(
                configPath,
                jsonConfig.Trim() + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static void CreateConfigurationFile(string rootDirectory, int lookbackMonths)
        {
            string configDirectory = Path.Combine(rootDirectory, "config", "gem");
            Directory.CreateDirectory(configDirectory);

            string configPath = Path.Combine(configDirectory, "gem.cli.json");

            // Intentionally relative paths to verify normalization inside Program.Run.
            string jsonConfig = $@"{{
  ""dataDirectory"": ""data/gem/sample"",
  ""usEquityFile"": ""us-equity.csv"",
  ""exUsEquityFile"": ""exus-equity.csv"",
  ""safeAssetFile"": ""safe-asset.csv"",
  ""outputSignalsFile"": ""dist/gem/signals.json"",
  ""lookbackMonths"": {lookbackMonths}
}}";

            File.WriteAllText(
                configPath,
                jsonConfig,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void WriteCsv(string directory, string fileName, string content)
        {
            Directory.CreateDirectory(directory);

            string fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(fullPath, content.Trim() + Environment.NewLine, Encoding.UTF8);
        }

        private static void DeleteDirectoryIfExists(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            if (!Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }
}

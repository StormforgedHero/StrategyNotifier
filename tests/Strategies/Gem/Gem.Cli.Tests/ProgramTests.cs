using Gem.Cli.Contracts;
using System.Text.Json;

namespace Gem.Cli.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void Main_WhenConfigurationFileDoesNotExist_ReturnsNonZeroAndWritesError()
        {
            // Arrange
            string originalDirectory = Directory.GetCurrentDirectory();
            TextWriter originalError = Console.Error;

            string tempDirectory = CreateTemporaryDirectory();

            try
            {
                Directory.SetCurrentDirectory(tempDirectory);

                using var errorWriter = new StringWriter();
                Console.SetError(errorWriter);

                // Act
                int exitCode = Program.Main(Array.Empty<string>());

                // Assert
                Assert.NotEqual(0, exitCode);

                string error = errorWriter.ToString();
                Assert.Contains("Configuration file not found", error);
            }
            finally
            {
                Console.SetError(originalError);
                Directory.SetCurrentDirectory(originalDirectory);
                DeleteDirectoryIfExists(tempDirectory);
            }
        }

        [Fact]
        public void Main_WithValidConfigurationAndData_ReturnsZeroAndCreatesSignalsFile()
        {
            // Arrange
            string originalDirectory = Directory.GetCurrentDirectory();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;

            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                // Prepare configuration file.
                string configDirectory = Path.Combine(rootDirectory, "config", "gem");
                Directory.CreateDirectory(configDirectory);

                string configPath = Path.Combine(configDirectory, "gem.cli.json");

                string jsonConfig = """
                {
                  "dataDirectory": "data/gem/sample",
                  "usEquityFile": "us-equity.csv",
                  "exUsEquityFile": "exus-equity.csv",
                  "safeAssetFile": "safe-asset.csv",
                  "outputSignalsFile": "dist/gem/signals.json",
                  "lookbackMonths": 2
                }
                """;

                File.WriteAllText(configPath, jsonConfig);

                // Prepare sample data.
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

                string outputPath = Path.Combine(rootDirectory, "dist", "gem", "signals.json");

                Directory.SetCurrentDirectory(rootDirectory);

                using var outputWriter = new StringWriter();
                using var errorWriter = new StringWriter();

                Console.SetOut(outputWriter);
                Console.SetError(errorWriter);

                // Act
                int exitCode = Program.Main(Array.Empty<string>());

                // Assert
                Assert.Equal(0, exitCode);

                string output = outputWriter.ToString();
                string error = errorWriter.ToString();

                Assert.True(string.IsNullOrWhiteSpace(error));
                Assert.Contains("StrategyNotifier - GEM CLI", output);
                Assert.Contains("Generated signals", output);

                Assert.True(File.Exists(outputPath));

                string json = File.ReadAllText(outputPath);
                GemSignalOutput[]? signals =
                    JsonSerializer.Deserialize<GemSignalOutput[]>(json);

                Assert.NotNull(signals);
                Assert.NotEmpty(signals!);
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
                Directory.SetCurrentDirectory(originalDirectory);
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Main_WithInvalidConfiguration_ReturnsNonZeroAndWritesConfigurationError()
        {
            // Arrange
            string originalDirectory = Directory.GetCurrentDirectory();
            TextWriter originalError = Console.Error;

            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                // Prepare configuration file with invalid lookbackMonths (0).
                string configDirectory = Path.Combine(rootDirectory, "config", "gem");
                Directory.CreateDirectory(configDirectory);

                string configPath = Path.Combine(configDirectory, "gem.cli.json");

                string jsonConfig = """
                {
                  "dataDirectory": "data/gem/sample",
                  "usEquityFile": "us-equity.csv",
                  "exUsEquityFile": "exus-equity.csv",
                  "safeAssetFile": "safe-asset.csv",
                  "outputSignalsFile": "dist/gem/signals.json",
                  "lookbackMonths": 0
                }
                """;

                File.WriteAllText(configPath, jsonConfig);

                Directory.SetCurrentDirectory(rootDirectory);

                using var errorWriter = new StringWriter();
                Console.SetError(errorWriter);

                // Act
                int exitCode = Program.Main(Array.Empty<string>());

                // Assert
                Assert.NotEqual(0, exitCode);

                string error = errorWriter.ToString();
                Assert.Contains("Configuration error", error);
                Assert.Contains("lookbackMonths", error);
            }
            finally
            {
                Console.SetError(originalError);
                Directory.SetCurrentDirectory(originalDirectory);
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Main_WithInvalidConfigurationJson_ReturnsNonZeroAndWritesConfigurationError()
        {
            // Arrange
            string originalDirectory = Directory.GetCurrentDirectory();
            TextWriter originalError = Console.Error;

            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                string configDirectory = Path.Combine(rootDirectory, "config", "gem");
                Directory.CreateDirectory(configDirectory);

                string configPath = Path.Combine(configDirectory, "gem.cli.json");

                // Invalid JSON (missing closing brace).
                string jsonConfig = """
                {
                  "dataDirectory": "data/gem/sample"
                """;

                File.WriteAllText(configPath, jsonConfig);

                Directory.SetCurrentDirectory(rootDirectory);

                using var errorWriter = new StringWriter();
                Console.SetError(errorWriter);

                // Act
                int exitCode = Program.Main(Array.Empty<string>());

                // Assert
                Assert.NotEqual(0, exitCode);

                string error = errorWriter.ToString();
                Assert.Contains("Configuration error", error);
                Assert.Contains("invalid JSON", error, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Console.SetError(originalError);
                Directory.SetCurrentDirectory(originalDirectory);
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Main_WithMissingInputDataFile_ReturnsNonZeroAndWritesDataFileError()
        {
            // Arrange
            string originalDirectory = Directory.GetCurrentDirectory();
            TextWriter originalError = Console.Error;

            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                // Prepare valid configuration pointing to sample data directory.
                string configDirectory = Path.Combine(rootDirectory, "config", "gem");
                Directory.CreateDirectory(configDirectory);

                string configPath = Path.Combine(configDirectory, "gem.cli.json");

                string jsonConfig = """
                {
                  "dataDirectory": "data/gem/sample",
                  "usEquityFile": "us-equity.csv",
                  "exUsEquityFile": "exus-equity.csv",
                  "safeAssetFile": "safe-asset.csv",
                  "outputSignalsFile": "dist/gem/signals.json",
                  "lookbackMonths": 2
                }
                """;

                File.WriteAllText(configPath, jsonConfig);

                // Prepare data directory with missing safe-asset.csv.
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

                Directory.SetCurrentDirectory(rootDirectory);

                using var errorWriter = new StringWriter();
                Console.SetError(errorWriter);

                // Act
                int exitCode = Program.Main(Array.Empty<string>());

                // Assert
                Assert.NotEqual(0, exitCode);

                string error = errorWriter.ToString();
                Assert.Contains("Input data file not found", error);
                Assert.Contains("safe-asset.csv", error);
            }
            finally
            {
                Console.SetError(originalError);
                Directory.SetCurrentDirectory(originalDirectory);
                DeleteDirectoryIfExists(rootDirectory);
            }
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
            File.WriteAllText(fullPath, content.Trim() + Environment.NewLine);
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

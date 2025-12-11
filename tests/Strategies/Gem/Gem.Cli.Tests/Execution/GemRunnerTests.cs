using Gem.Cli.Configuration;
using Gem.Cli.Contracts;
using Gem.Cli.Execution;
using System.Text.Json;

namespace Gem.Cli.Tests.Execution
{
    public class GemRunnerTests
    {
        [Fact]
        public void Run_WithValidConfiguration_ProducesSignalsFile()
        {
            // Arrange
            string rootDirectory = CreateTemporaryDirectory();

            try
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

                string outputPath = Path.Combine(rootDirectory, "dist", "gem", "signals.json");

                var configuration = new GemCliConfiguration
                {
                    DataDirectory = dataDirectory,
                    UsEquityFile = "us-equity.csv",
                    ExUsEquityFile = "exus-equity.csv",
                    SafeAssetFile = "safe-asset.csv",
                    OutputSignalsFile = outputPath,
                    LookbackMonths = 2
                };

                configuration.Validate();

                var runner = new GemRunner(configuration);

                // Act
                int signalCount = runner.Run();

                // Assert
                Assert.True(signalCount > 0);
                Assert.True(File.Exists(outputPath));

                string json = File.ReadAllText(outputPath);

                List<GemSignalOutput>? outputs =
                    JsonSerializer.Deserialize<List<GemSignalOutput>>(json);

                Assert.NotNull(outputs);
                Assert.Equal(signalCount, outputs!.Count);

                foreach (GemSignalOutput output in outputs)
                {
                    Assert.False(string.IsNullOrWhiteSpace(output.Period));
                    Assert.False(string.IsNullOrWhiteSpace(output.Position));
                }
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WithValidConfiguration_ProducesExpectedSignalContent()
        {
            // Arrange
            string rootDirectory = CreateTemporaryDirectory();

            try
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

                string outputPath = Path.Combine(rootDirectory, "dist", "gem", "signals.json");

                var configuration = new GemCliConfiguration
                {
                    DataDirectory = dataDirectory,
                    UsEquityFile = "us-equity.csv",
                    ExUsEquityFile = "exus-equity.csv",
                    SafeAssetFile = "safe-asset.csv",
                    OutputSignalsFile = outputPath,
                    LookbackMonths = 2
                };

                configuration.Validate();

                var runner = new GemRunner(configuration);

                // Act
                int signalCount = runner.Run();

                // Assert
                Assert.Equal(2, signalCount);
                Assert.True(File.Exists(outputPath));

                string json = File.ReadAllText(outputPath);

                List<GemSignalOutput>? outputs =
                    JsonSerializer.Deserialize<List<GemSignalOutput>>(json);

                Assert.NotNull(outputs);
                Assert.Equal(signalCount, outputs!.Count);

                // Expected:
                // 2025-02: UsEquity
                // 2025-03: UsEquity
                Assert.Equal("2025-02", outputs[0].Period);
                Assert.Equal("UsEquity", outputs[0].Position);

                Assert.Equal("2025-03", outputs[1].Period);
                Assert.Equal("UsEquity", outputs[1].Position);
            }
            finally
            {
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

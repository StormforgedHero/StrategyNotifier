using Gem.Cli.Configuration;

namespace Gem.Cli.Tests.Configuration
{
    public class GemConfigLoaderTests
    {
        [Fact]
        public void Load_WithMissingFile_ThrowsFileNotFoundException()
        {
            // Arrange
            string tempDirectory = CreateTemporaryDirectory();

            try
            {
                string configPath = Path.Combine(tempDirectory, "config", "gem", "gem.cli.json");
                var loader = new GemConfigLoader(configPath);

                // Act & Assert
                Assert.Throws<FileNotFoundException>(() => loader.Load());
            }
            finally
            {
                DeleteDirectoryIfExists(tempDirectory);
            }
        }

        [Fact]
        public void Load_WithValidConfigurationFile_ReturnsValidatedConfiguration()
        {
            // Arrange
            string tempDirectory = CreateTemporaryDirectory();
            try
            {
                string configDirectory = Path.Combine(tempDirectory, "config", "gem");
                Directory.CreateDirectory(configDirectory);

                string configPath = Path.Combine(configDirectory, "gem.cli.json");

                string json = """
                {
                  "dataDirectory": "data/gem/sample",
                  "usEquityFile": "us-equity.csv",
                  "exUsEquityFile": "exus-equity.csv",
                  "safeAssetFile": "safe-asset.csv",
                  "outputSignalsFile": "dist/gem/signals.json",
                  "lookbackMonths": 12
                }
                """;

                File.WriteAllText(configPath, json);

                var loader = new GemConfigLoader(configPath);

                // Act
                GemCliConfiguration configuration = loader.Load();

                // Assert
                Assert.Equal("data/gem/sample", configuration.DataDirectory);
                Assert.Equal("us-equity.csv", configuration.UsEquityFile);
                Assert.Equal("exus-equity.csv", configuration.ExUsEquityFile);
                Assert.Equal("safe-asset.csv", configuration.SafeAssetFile);
                Assert.Equal("dist/gem/signals.json", configuration.OutputSignalsFile);
                Assert.Equal(12, configuration.LookbackMonths);
            }
            finally
            {
                DeleteDirectoryIfExists(tempDirectory);
            }
        }

        [Fact]
        public void Load_WithInvalidJson_ThrowsInvalidOperationException()
        {
            // Arrange
            string tempDirectory = CreateTemporaryDirectory();
            try
            {
                string configDirectory = Path.Combine(tempDirectory, "config", "gem");
                Directory.CreateDirectory(configDirectory);

                string configPath = Path.Combine(configDirectory, "gem.cli.json");

                // Invalid JSON (missing closing brace).
                string json = """
                {
                  "dataDirectory": "data/gem/sample"
                """;

                File.WriteAllText(configPath, json);

                var loader = new GemConfigLoader(configPath);

                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => loader.Load());
            }
            finally
            {
                DeleteDirectoryIfExists(tempDirectory);
            }
        }

        [Fact]
        public void Load_WithInvalidLookbackMonths_ThrowsInvalidOperationException()
        {
            // Arrange
            string tempDirectory = CreateTemporaryDirectory();
            try
            {
                string configDirectory = Path.Combine(tempDirectory, "config", "gem");
                Directory.CreateDirectory(configDirectory);

                string configPath = Path.Combine(configDirectory, "gem.cli.json");

                string json = """
                {
                  "dataDirectory": "data/gem/sample",
                  "usEquityFile": "us-equity.csv",
                  "exUsEquityFile": "exus-equity.csv",
                  "safeAssetFile": "safe-asset.csv",
                  "outputSignalsFile": "dist/gem/signals.json",
                  "lookbackMonths": 0
                }
                """;

                File.WriteAllText(configPath, json);

                var loader = new GemConfigLoader(configPath);

                // Act & Assert
                Assert.Throws<InvalidOperationException>(() => loader.Load());
            }
            finally
            {
                DeleteDirectoryIfExists(tempDirectory);
            }
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
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

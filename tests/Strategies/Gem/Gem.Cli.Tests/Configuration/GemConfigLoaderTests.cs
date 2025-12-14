using Gem.Cli.Configuration;
using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.Configuration
{
    public class GemConfigLoaderTests
    {
        [Fact]
        public void Constructor_WithNullPath_ThrowsArgumentNullException()
        {
            ArgumentNullException ex =
                Assert.Throws<ArgumentNullException>(() => new GemConfigLoader(null!));

            Assert.Equal("configFilePath", ex.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithEmptyOrWhitespacePath_ThrowsArgumentException(string configFilePath)
        {
            ArgumentException ex =
                Assert.Throws<ArgumentException>(() => new GemConfigLoader(configFilePath));

            Assert.Equal("configFilePath", ex.ParamName);
        }

        [Fact]
        public void Load_WithMissingFile_ThrowsFileNotFoundException()
        {
            using var context = GemConfigLoaderTestContext.Create();

            FileNotFoundException ex =
                Assert.Throws<FileNotFoundException>(() => context.Load());

            Assert.Equal(context.ConfigPath, ex.FileName);
        }

        [Fact]
        public void Load_WithEmptyFile_ThrowsInvalidOperationException()
        {
            using var context = GemConfigLoaderTestContext.Create();

            context.WriteConfig(string.Empty);

            InvalidOperationException ex =
                Assert.Throws<InvalidOperationException>(() => context.Load());

            Assert.Contains("is empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Load_WithWhitespaceOnlyFile_ThrowsInvalidOperationException()
        {
            using var context = GemConfigLoaderTestContext.Create();

            context.WriteConfig("   \n");

            InvalidOperationException ex =
                Assert.Throws<InvalidOperationException>(() => context.Load());

            Assert.Contains("is empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Load_WithValidConfigurationFile_ReturnsValidatedConfiguration()
        {
            using var context = GemConfigLoaderTestContext.Create();

            context.WriteConfig("""
            {
              "dataDirectory": "data/gem/sample",
              "usEquityFile": "us-equity.csv",
              "exUsEquityFile": "exus-equity.csv",
              "safeAssetFile": "safe-asset.csv",
              "outputSignalsFile": "dist/gem/signals.json",
              "lookbackMonths": 12
            }
            """);

            GemCliConfiguration configuration = context.Load();

            Assert.Equal("data/gem/sample", configuration.DataDirectory);
            Assert.Equal("us-equity.csv", configuration.UsEquityFile);
            Assert.Equal("exus-equity.csv", configuration.ExUsEquityFile);
            Assert.Equal("safe-asset.csv", configuration.SafeAssetFile);
            Assert.Equal("dist/gem/signals.json", configuration.OutputSignalsFile);
            Assert.Equal(12, configuration.LookbackMonths);
        }

        [Fact]
        public void Load_WithInvalidJson_ThrowsInvalidOperationException()
        {
            using var context = GemConfigLoaderTestContext.Create();

            context.WriteConfig("""
            {
              "dataDirectory": "data/gem/sample"
            """);

            InvalidOperationException ex =
                Assert.Throws<InvalidOperationException>(() => context.Load());

            Assert.Contains("invalid JSON", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Load_WithInvalidLookbackMonths_ThrowsInvalidOperationException()
        {
            using var context = GemConfigLoaderTestContext.Create();

            context.WriteConfig("""
            {
              "dataDirectory": "data/gem/sample",
              "usEquityFile": "us-equity.csv",
              "exUsEquityFile": "exus-equity.csv",
              "safeAssetFile": "safe-asset.csv",
              "outputSignalsFile": "dist/gem/signals.json",
              "lookbackMonths": 0
            }
            """);

            InvalidOperationException ex =
                Assert.Throws<InvalidOperationException>(() => context.Load());

            Assert.Contains("lookbackMonths", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Load_WithMissingRequiredConfigurationValues_ThrowsInvalidOperationException()
        {
            using var context = GemConfigLoaderTestContext.Create();

            context.WriteConfig("""
            {
              "lookbackMonths": 12,
              "outputSignalsFile": "dist/gem/signals.json"
            }
            """);

            InvalidOperationException ex =
                Assert.Throws<InvalidOperationException>(() => context.Load());

            Assert.Contains("dataDirectory", ex.Message, StringComparison.Ordinal);
        }

    }
}

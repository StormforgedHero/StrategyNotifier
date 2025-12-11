using Gem.Cli.Configuration;

namespace Gem.Cli.Tests.Configuration
{
    public class GemCliConfigurationTests
    {
        [Fact]
        public void Validate_WithValidValues_DoesNotThrow()
        {
            // Arrange
            var configuration = new GemCliConfiguration
            {
                DataDirectory = "data/gem/sample",
                UsEquityFile = "us-equity.csv",
                ExUsEquityFile = "exus-equity.csv",
                SafeAssetFile = "safe-asset.csv",
                OutputSignalsFile = "dist/gem/signals.json",
                LookbackMonths = 12
            };

            // Act & Assert
            configuration.Validate();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithInvalidDataDirectory_ThrowsInvalidOperationException(string? dataDirectory)
        {
            // Arrange
            var configuration = new GemCliConfiguration
            {
                DataDirectory = dataDirectory!,
                UsEquityFile = "us-equity.csv",
                ExUsEquityFile = "exus-equity.csv",
                SafeAssetFile = "safe-asset.csv",
                OutputSignalsFile = "dist/gem/signals.json",
                LookbackMonths = 12
            };

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => configuration.Validate());

            // Assert
            Assert.Contains("dataDirectory", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithInvalidOutputSignalsFile_ThrowsInvalidOperationException(string? outputSignalsFile)
        {
            // Arrange
            var configuration = new GemCliConfiguration
            {
                DataDirectory = "data/gem/sample",
                UsEquityFile = "us-equity.csv",
                ExUsEquityFile = "exus-equity.csv",
                SafeAssetFile = "safe-asset.csv",
                OutputSignalsFile = outputSignalsFile!,
                LookbackMonths = 12
            };

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => configuration.Validate());

            // Assert
            Assert.Contains("outputSignalsFile", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-12)]
        public void Validate_WithLookbackMonthsLessThanOne_ThrowsInvalidOperationException(int lookbackMonths)
        {
            // Arrange
            var configuration = new GemCliConfiguration
            {
                DataDirectory = "data/gem/sample",
                UsEquityFile = "us-equity.csv",
                ExUsEquityFile = "exus-equity.csv",
                SafeAssetFile = "safe-asset.csv",
                OutputSignalsFile = "dist/gem/signals.json",
                LookbackMonths = lookbackMonths
            };

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => configuration.Validate());

            // Assert
            Assert.Contains("lookbackMonths", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithInvalidUsEquityFile_ThrowsInvalidOperationException(string? usEquityFile)
        {
            // Arrange
            var configuration = new GemCliConfiguration
            {
                DataDirectory = "data/gem/sample",
                UsEquityFile = usEquityFile!,
                ExUsEquityFile = "exus-equity.csv",
                SafeAssetFile = "safe-asset.csv",
                OutputSignalsFile = "dist/gem/signals.json",
                LookbackMonths = 12
            };

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => configuration.Validate());

            // Assert
            Assert.Contains("usEquityFile", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithInvalidExUsEquityFile_ThrowsInvalidOperationException(string? exUsEquityFile)
        {
            // Arrange
            var configuration = new GemCliConfiguration
            {
                DataDirectory = "data/gem/sample",
                UsEquityFile = "us-equity.csv",
                ExUsEquityFile = exUsEquityFile!,
                SafeAssetFile = "safe-asset.csv",
                OutputSignalsFile = "dist/gem/signals.json",
                LookbackMonths = 12
            };

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => configuration.Validate());

            // Assert
            Assert.Contains("exUsEquityFile", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_WithInvalidSafeAssetFile_ThrowsInvalidOperationException(string? safeAssetFile)
        {
            // Arrange
            var configuration = new GemCliConfiguration
            {
                DataDirectory = "data/gem/sample",
                UsEquityFile = "us-equity.csv",
                ExUsEquityFile = "exus-equity.csv",
                SafeAssetFile = safeAssetFile!,
                OutputSignalsFile = "dist/gem/signals.json",
                LookbackMonths = 12
            };

            // Act
            var exception = Assert.Throws<InvalidOperationException>(() => configuration.Validate());

            // Assert
            Assert.Contains("safeAssetFile", exception.Message, StringComparison.Ordinal);
        }
    }
}

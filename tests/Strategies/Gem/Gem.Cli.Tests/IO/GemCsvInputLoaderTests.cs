using Gem.Cli.IO;
using Gem.Domain.Core;
using Gem.Domain.Model;

namespace Gem.Cli.Tests.IO
{
    public class GemCsvInputLoaderTests
    {
        [Fact]
        public void Load_WithValidCsvFiles_BuildsExpectedGemInputData()
        {
            // Arrange
            string tempDirectory = CreateTemporaryDirectory();

            try
            {
                WriteCsv(
                    tempDirectory,
                    "us-equity.csv",
                    """
                Year,Month,Return
                2025,1,0.02
                2025,2,0.03
                2025,3,-0.01
                """);

                WriteCsv(
                    tempDirectory,
                    "exus-equity.csv",
                    """
                Year,Month,Return
                2025,1,0.01
                2025,2,0.02
                2025,3,0.00
                """);

                WriteCsv(
                    tempDirectory,
                    "safe-asset.csv",
                    """
                Year,Month,Return
                2025,1,0.002
                2025,2,0.002
                2025,3,0.002
                """);

                var loader = new GemCsvInputLoader(tempDirectory);

                // Act
                GemInputData input = loader.Load();

                // Assert
                Assert.NotNull(input);

                Assert.Equal(AssetKind.UsEquity, input.UsEquity.AssetKind);
                Assert.Equal(3, input.UsEquity.Returns.Count);
                Assert.Equal(new YearMonth(2025, 1), input.UsEquity.Returns[0].Period);
                Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);

                Assert.Equal(AssetKind.ExUsEquity, input.ExUsEquity.AssetKind);
                Assert.Equal(3, input.ExUsEquity.Returns.Count);
                Assert.Equal(new YearMonth(2025, 3), input.ExUsEquity.Returns[2].Period);
                Assert.Equal(0.00m, input.ExUsEquity.Returns[2].Rate);

                Assert.Equal(AssetKind.SafeAsset, input.SafeAsset.AssetKind);
                Assert.Equal(3, input.SafeAsset.Returns.Count);
            }
            finally
            {
                DeleteDirectoryIfExists(tempDirectory);
            }
        }

        [Fact]
        public void Load_WhenSafeAssetFileIsMissing_ThrowsFileNotFoundException()
        {
            // Arrange
            string tempDirectory = CreateTemporaryDirectory();

            try
            {
                // Only US and ex-US files are present.
                WriteCsv(
                    tempDirectory,
                    "us-equity.csv",
                    """
                Year,Month,Return
                2025,1,0.02
                """);

                WriteCsv(
                    tempDirectory,
                    "exus-equity.csv",
                    """
                Year,Month,Return
                2025,1,0.01
                """);

                var loader = new GemCsvInputLoader(tempDirectory);

                // Act
                FileNotFoundException exception = Assert.Throws<FileNotFoundException>(
                    () => loader.Load());

                // Assert
                Assert.NotNull(exception.FileName);
                Assert.EndsWith("safe-asset.csv", exception.FileName!, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                DeleteDirectoryIfExists(tempDirectory);
            }
        }

        [Fact]
        public void Load_WhenCsvContainsInvalidReturnValue_ThrowsFormatException()
        {
            // Arrange
            string tempDirectory = CreateTemporaryDirectory();

            try
            {
                // Invalid return value in US equity file.
                WriteCsv(
                    tempDirectory,
                    "us-equity.csv",
                    """
                Year,Month,Return
                2025,1,not-a-number
                """);

                WriteCsv(
                    tempDirectory,
                    "exus-equity.csv",
                    """
                Year,Month,Return
                2025,1,0.01
                """);

                WriteCsv(
                    tempDirectory,
                    "safe-asset.csv",
                    """
                Year,Month,Return
                2025,1,0.002
                """);

                var loader = new GemCsvInputLoader(tempDirectory);

                // Act & Assert
                Assert.Throws<FormatException>(() => loader.Load());
            }
            finally
            {
                DeleteDirectoryIfExists(tempDirectory);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithNullOrWhitespaceBaseDirectory_ThrowsArgumentException(string? baseDirectory)
        {
            // Act
            var exception = Assert.Throws<ArgumentException>(
                () => new GemCsvInputLoader(baseDirectory!));

            // Assert
            Assert.Equal("baseDirectory", exception.ParamName);
        }

        [Fact]
        public void Load_WhenCsvFilesContainOnlyHeader_ReturnsEmptySeries()
        {
            // Arrange
            string tempDirectory = CreateTemporaryDirectory();

            try
            {
                WriteCsv(
                    tempDirectory,
                    "us-equity.csv",
                    "Year,Month,Return");

                WriteCsv(
                    tempDirectory,
                    "exus-equity.csv",
                    "Year,Month,Return");

                WriteCsv(
                    tempDirectory,
                    "safe-asset.csv",
                    "Year,Month,Return");

                var loader = new GemCsvInputLoader(tempDirectory);

                // Act
                GemInputData input = loader.Load();

                // Assert
                Assert.Empty(input.UsEquity.Returns);
                Assert.Empty(input.ExUsEquity.Returns);
                Assert.Empty(input.SafeAsset.Returns);
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

        private static void WriteCsv(string directory, string fileName, string content)
        {
            Directory.CreateDirectory(directory);

            string fullPath = Path.Combine(directory, fileName);
            // Trim to avoid leading/trailing blank lines, then ensure newline at the end.
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

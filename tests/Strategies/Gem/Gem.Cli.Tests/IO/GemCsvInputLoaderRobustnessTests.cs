using Gem.Cli.IO;
using Gem.Domain.Core;
using Gem.Domain.Model;
using System.Text;

namespace Gem.Cli.Tests.IO
{
    public class GemCsvInputLoaderRobustnessTests
    {
        [Fact]
        public void Load_WhenFilesAreUtf8WithBom_ParsesSuccessfully()
        {
            string tempDirectory = CreateTemporaryDirectory();

            try
            {
                WriteCsvWithBom(
                    tempDirectory,
                    "us-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.02
                    """);

                WriteCsvWithBom(
                    tempDirectory,
                    "exus-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.01
                    """);

                WriteCsvWithBom(
                    tempDirectory,
                    "safe-asset.csv",
                    """
                    Year,Month,Return
                    2025,1,0.002
                    """);

                var loader = new GemCsvInputLoader(tempDirectory);

                GemInputData input = loader.Load();

                Assert.Equal(AssetKind.UsEquity, input.UsEquity.AssetKind);
                Assert.Single(input.UsEquity.Returns);
                Assert.Equal(new YearMonth(2025, 1), input.UsEquity.Returns[0].Period);
                Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);
            }
            finally
            {
                DeleteDirectoryIfExists(tempDirectory);
            }
        }

        [Fact]
        public void Load_WhenColumnsContainWhitespace_TrimsAndParsesSuccessfully()
        {
            string tempDirectory = CreateTemporaryDirectory();

            try
            {
                WriteCsv(
                    tempDirectory,
                    "us-equity.csv",
                    """
                    Year,Month,Return
                    2025 , 1 , 0.02
                    2025 , 2 , 0.03
                    """);

                WriteCsv(
                    tempDirectory,
                    "exus-equity.csv",
                    """
                    Year,Month,Return
                    2025 , 1 , 0.01
                    2025 , 2 , 0.02
                    """);

                WriteCsv(
                    tempDirectory,
                    "safe-asset.csv",
                    """
                    Year,Month,Return
                    2025 , 1 , 0.002
                    2025 , 2 , 0.002
                    """);

                var loader = new GemCsvInputLoader(tempDirectory);

                GemInputData input = loader.Load();

                Assert.Equal(2, input.UsEquity.Returns.Count);
                Assert.Equal(new YearMonth(2025, 2), input.UsEquity.Returns[1].Period);
                Assert.Equal(0.03m, input.UsEquity.Returns[1].Rate);
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
            File.WriteAllText(fullPath, content.Trim() + Environment.NewLine, Encoding.UTF8);
        }

        private static void WriteCsvWithBom(string directory, string fileName, string content)
        {
            Directory.CreateDirectory(directory);

            string fullPath = Path.Combine(directory, fileName);
            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

            File.WriteAllText(fullPath, content.Trim() + Environment.NewLine, utf8WithBom);
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

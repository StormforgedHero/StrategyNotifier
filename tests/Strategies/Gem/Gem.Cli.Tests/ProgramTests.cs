namespace Gem.Cli.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void Main_WhenSampleDirectoryDoesNotExist_ReturnsNonZeroAndWritesError()
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
                Assert.Contains("Sample data directory not found", error);
            }
            finally
            {
                Console.SetError(originalError);
                Directory.SetCurrentDirectory(originalDirectory);
                DeleteDirectoryIfExists(tempDirectory);
            }
        }

        [Fact]
        public void Main_WithValidSampleDataDirectory_ReturnsZeroAndWritesSummary()
        {
            // Arrange
            string originalDirectory = Directory.GetCurrentDirectory();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;

            string tempDirectory = CreateTemporaryDirectory();

            try
            {
                string sampleDirectory = Path.Combine(tempDirectory, "data", "gem", "sample");
                Directory.CreateDirectory(sampleDirectory);

                WriteCsv(
                    sampleDirectory,
                    "us-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.02
                    2025,2,0.03
                    2025,3,-0.01
                    """);

                WriteCsv(
                    sampleDirectory,
                    "exus-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.01
                    2025,2,0.02
                    2025,3,0.00
                    """);

                WriteCsv(
                    sampleDirectory,
                    "safe-asset.csv",
                    """
                    Year,Month,Return
                    2025,1,0.002
                    2025,2,0.002
                    2025,3,0.002
                    """);

                Directory.SetCurrentDirectory(tempDirectory);

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

                Assert.Contains("StrategyNotifier - GEM sample run", output);
                Assert.Contains("Sample data directory", output);
                Assert.Contains("Generated signals", output);
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
                Directory.SetCurrentDirectory(originalDirectory);
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

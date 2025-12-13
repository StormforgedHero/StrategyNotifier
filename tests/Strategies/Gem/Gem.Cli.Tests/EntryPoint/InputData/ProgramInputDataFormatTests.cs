using System.Text;

namespace Gem.Cli.Tests.EntryPoint.InputData
{
    public class ProgramInputDataFormatTests
    {
        [Fact]
        public void Main_WithInvalidCsvReturnValue_ReturnsNonZeroAndWritesInputDataFormatError()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 2);

                string dataDirectory = Path.Combine(rootDirectory, "data", "gem", "sample");
                Directory.CreateDirectory(dataDirectory);

                // Invalid return in US equity file.
                WriteCsv(
                    dataDirectory,
                    "us-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,not-a-number
                    """);

                // Other files are valid.
                WriteCsv(
                    dataDirectory,
                    "exus-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.01
                    """);

                WriteCsv(
                    dataDirectory,
                    "safe-asset.csv",
                    """
                    Year,Month,Return
                    2025,1,0.002
                    """);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.NotEqual(0, exitCode);

                string error = errWriter.ToString();
                Assert.Contains("Input data format error", error, StringComparison.Ordinal);
                Assert.Contains("Invalid return value", error, StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Main_WithInvalidCsvHeader_ReturnsNonZeroAndWritesInputDataFormatError()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 2);

                string dataDirectory = Path.Combine(rootDirectory, "data", "gem", "sample");
                Directory.CreateDirectory(dataDirectory);

                // Invalid header in US equity file.
                WriteCsv(
                    dataDirectory,
                    "us-equity.csv",
                    """
                    BadHeader1,BadHeader2,BadHeader3
                    2025,1,0.02
                    """);

                // Other files are valid.
                WriteCsv(
                    dataDirectory,
                    "exus-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.01
                    """);

                WriteCsv(
                    dataDirectory,
                    "safe-asset.csv",
                    """
                    Year,Month,Return
                    2025,1,0.002
                    """);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.NotEqual(0, exitCode);

                string error = errWriter.ToString();
                Assert.Contains("Input data format error", error, StringComparison.Ordinal);
                Assert.Contains("Invalid CSV header", error, StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Main_WithMonthOutOfRange_ReturnsNonZeroAndWritesInputDataFormatError()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 2);

                string dataDirectory = Path.Combine(rootDirectory, "data", "gem", "sample");
                Directory.CreateDirectory(dataDirectory);

                // Month=13 is out of range.
                WriteCsv(
                    dataDirectory,
                    "us-equity.csv",
                    """
                    Year,Month,Return
                    2025,13,0.02
                    """);

                WriteCsv(
                    dataDirectory,
                    "exus-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.01
                    """);

                WriteCsv(
                    dataDirectory,
                    "safe-asset.csv",
                    """
                    Year,Month,Return
                    2025,1,0.002
                    """);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.NotEqual(0, exitCode);

                string error = errWriter.ToString();
                Assert.Contains("Input data format error", error, StringComparison.Ordinal);
                Assert.Contains("Invalid month value", error, StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        private static void CreateValidConfiguration(string rootDirectory, int lookbackMonths)
        {
            string configDirectory = Path.Combine(rootDirectory, "config", "gem");
            Directory.CreateDirectory(configDirectory);

            string configPath = Path.Combine(configDirectory, "gem.cli.json");

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

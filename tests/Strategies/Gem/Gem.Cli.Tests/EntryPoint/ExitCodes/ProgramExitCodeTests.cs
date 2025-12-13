using System.Text;

namespace Gem.Cli.Tests.EntryPoint.ExitCodes
{
    public class ProgramExitCodeTests
    {
        [Fact]
        public void Run_WhenConfigurationFileIsMissing_ReturnsConfigurationFileNotFoundExitCode()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(Cli.Program.ExitCodeConfigurationFileNotFound, exitCode);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenConfigurationJsonIsInvalid_ReturnsConfigurationErrorExitCode()
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

                Assert.Equal(Cli.Program.ExitCodeConfigurationError, exitCode);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenInputDataFileIsMissing_ReturnsInputDataFileNotFoundExitCode()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 2);

                string dataDirectory = Path.Combine(rootDirectory, "data", "gem", "sample");
                Directory.CreateDirectory(dataDirectory);

                // Missing safe-asset.csv on purpose.
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

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(Cli.Program.ExitCodeInputDataFileNotFound, exitCode);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenInputDataHasInvalidNumber_ReturnsInputDataFormatErrorExitCode()
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

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(Cli.Program.ExitCodeInputDataFormatError, exitCode);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenInputDataContainsDuplicatePeriods_ReturnsInputDataValidationErrorExitCode()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 2);

                string dataDirectory = Path.Combine(rootDirectory, "data", "gem", "sample");
                Directory.CreateDirectory(dataDirectory);

                // Duplicate period in US equity file: 2025-01 twice.
                WriteCsv(
                    dataDirectory,
                    "us-equity.csv",
                    """
                    Year,Month,Return
                    2025,1,0.02
                    2025,1,0.03
                    2025,2,0.01
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

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(Cli.Program.ExitCodeInputDataValidationError, exitCode);
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

        private static void WriteCsv(string directory, string fileName, string content)
        {
            Directory.CreateDirectory(directory);

            string fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(
                fullPath,
                content.Trim() + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

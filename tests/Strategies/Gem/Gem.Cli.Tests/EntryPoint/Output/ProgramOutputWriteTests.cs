using System.Text;

namespace Gem.Cli.Tests.EntryPoint.Output
{
    public class ProgramOutputWriteTests
    {
        [Fact]
        public void Run_WhenOutputSignalsFilePathIsDirectory_ReturnsNonZeroAndWritesOutputWriteError()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 2);

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

                // Force output path to be a DIRECTORY, not a file.
                string outputPathAsDirectory = Path.Combine(rootDirectory, "dist", "gem", "signals.json");
                Directory.CreateDirectory(outputPathAsDirectory);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.NotEqual(0, exitCode);

                string error = errWriter.ToString();
                Assert.Contains("Output write error", error, StringComparison.Ordinal);
                Assert.Contains("signals.json", error, StringComparison.OrdinalIgnoreCase);

                // Still a directory; no file could have been created at that path.
                Assert.True(Directory.Exists(outputPathAsDirectory));
                Assert.False(File.Exists(outputPathAsDirectory));
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
            File.WriteAllText(
                fullPath,
                content.Trim() + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

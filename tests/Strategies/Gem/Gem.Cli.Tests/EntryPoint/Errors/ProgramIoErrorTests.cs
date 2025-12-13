using System.Text;

namespace Gem.Cli.Tests.EntryPoint.Errors
{
    public class ProgramIoErrorTests
    {
        [Fact]
        public void Run_WhenInputFileIsLocked_ReturnsNonZeroAndWritesIoError()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 2);
                CreateSampleDataFiles(rootDirectory);

                string lockedInputPath = Path.Combine(rootDirectory, "data", "gem", "sample", "us-equity.csv");

                using var lockStream = new FileStream(
                    lockedInputPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(Cli.Program.ExitCodeIoError, exitCode);

                string error = errWriter.ToString();
                Assert.Contains("I/O error", error, StringComparison.Ordinal);
                Assert.Contains("us-equity.csv", error, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Output write error", error, StringComparison.Ordinal);

                string outputPath = Path.Combine(rootDirectory, "dist", "gem", "signals.json");
                Assert.False(File.Exists(outputPath));
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenOutputSignalsFileIsLocked_ReturnsNonZeroAndWritesOutputWriteError()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateValidConfiguration(rootDirectory, lookbackMonths: 2);
                CreateSampleDataFiles(rootDirectory);

                string outputDirectory = Path.Combine(rootDirectory, "dist", "gem");
                Directory.CreateDirectory(outputDirectory);

                string outputPath = Path.Combine(outputDirectory, "signals.json");

                File.WriteAllText(
                    outputPath,
                    "[]\n",
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                using var lockStream = new FileStream(
                    outputPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Cli.Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(Cli.Program.ExitCodeOutputWriteError, exitCode);

                string error = errWriter.ToString();
                Assert.Contains("Output write error", error, StringComparison.Ordinal);
                Assert.Contains("signals.json", error, StringComparison.OrdinalIgnoreCase);
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

        private static void CreateSampleDataFiles(string rootDirectory)
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

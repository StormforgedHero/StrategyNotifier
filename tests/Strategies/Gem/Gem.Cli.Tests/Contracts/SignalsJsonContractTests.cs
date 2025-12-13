using Gem.Cli.Contracts;
using System.Text;
using System.Text.Json;

namespace Gem.Cli.Tests.Contracts
{
    public class SignalsJsonContractTests
    {
        private static readonly HashSet<string> AllowedPositions =
            new(StringComparer.Ordinal)
            {
                "UsEquity",
                "ExUsEquity",
                "SafeAsset"
            };

        [Fact]
        public void Run_WritesSignalsJson_WithValidPeriodFormatAndAllowedPositions()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateConfigurationFile(rootDirectory, lookbackMonths: 2);
                CreateSampleCsvData(rootDirectory);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(0, exitCode);
                Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

                GemSignalOutput[] signals = ReadSignals(rootDirectory);
                Assert.NotEmpty(signals);

                foreach (GemSignalOutput signal in signals)
                {
                    Assert.True(IsValidYearMonth(signal.Period), $"Invalid period: '{signal.Period}'.");
                    Assert.True(AllowedPositions.Contains(signal.Position), $"Invalid position: '{signal.Position}'.");
                }
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WritesSignalsJson_SortedByPeriodAscending_AndNoDuplicatePeriods()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                CreateConfigurationFile(rootDirectory, lookbackMonths: 2);
                CreateSampleCsvData(rootDirectory);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(0, exitCode);
                Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

                GemSignalOutput[] signals = ReadSignals(rootDirectory);
                Assert.NotEmpty(signals);

                var seen = new HashSet<string>(StringComparer.Ordinal);

                int? previousIndex = null;

                foreach (GemSignalOutput signal in signals)
                {
                    Assert.True(seen.Add(signal.Period), $"Duplicate period detected: '{signal.Period}'.");

                    Assert.True(
                        TryParseYearMonth(signal.Period, out int year, out int month),
                        $"Invalid period: '{signal.Period}'.");

                    int currentIndex = (year * 12) + month;

                    if (previousIndex.HasValue)
                    {
                        Assert.True(
                            currentIndex > previousIndex.Value,
                            "Signals are not strictly increasing by period.");
                    }

                    previousIndex = currentIndex;
                }
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        [Fact]
        public void Run_WhenNoSignalsAreGenerated_StillWritesValidEmptyJsonArray()
        {
            string rootDirectory = CreateTemporaryDirectory();

            try
            {
                // Lookback larger than available history -> zero signals expected.
                CreateConfigurationFile(rootDirectory, lookbackMonths: 12);
                CreateSampleCsvData(rootDirectory);

                using var outWriter = new StringWriter();
                using var errWriter = new StringWriter();

                int exitCode = Program.Run(rootDirectory, outWriter, errWriter);

                Assert.Equal(0, exitCode);
                Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

                string outputPath = GetSignalsPath(rootDirectory);
                Assert.True(File.Exists(outputPath));

                string json = File.ReadAllText(outputPath, Encoding.UTF8);

                GemSignalOutput[]? signals =
                    JsonSerializer.Deserialize<GemSignalOutput[]>(json);

                Assert.NotNull(signals);
                Assert.Empty(signals!);
            }
            finally
            {
                DeleteDirectoryIfExists(rootDirectory);
            }
        }

        private static GemSignalOutput[] ReadSignals(string rootDirectory)
        {
            string outputPath = GetSignalsPath(rootDirectory);
            Assert.True(File.Exists(outputPath));

            string json = File.ReadAllText(outputPath, Encoding.UTF8);

            GemSignalOutput[]? signals =
                JsonSerializer.Deserialize<GemSignalOutput[]>(json);

            Assert.NotNull(signals);

            return signals!;
        }

        private static string GetSignalsPath(string rootDirectory)
        {
            return Path.Combine(rootDirectory, "dist", "gem", "signals.json");
        }

        private static bool IsValidYearMonth(string value)
        {
            return TryParseYearMonth(value, out int year, out int month)
                   && year >= 0
                   && month is >= 1 and <= 12;
        }

        private static bool TryParseYearMonth(string value, out int year, out int month)
        {
            year = 0;
            month = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            // Expected: YYYY-MM
            if (value.Length != 7)
            {
                return false;
            }

            if (value[4] != '-')
            {
                return false;
            }

            string yearPart = value[..4];
            string monthPart = value.Substring(5, 2);

            if (!int.TryParse(yearPart, out year))
            {
                return false;
            }

            if (!int.TryParse(monthPart, out month))
            {
                return false;
            }

            return true;
        }

        private static void CreateConfigurationFile(string rootDirectory, int lookbackMonths)
        {
            string configDirectory = Path.Combine(rootDirectory, "config", "gem");
            Directory.CreateDirectory(configDirectory);

            string configPath = Path.Combine(configDirectory, "gem.cli.json");

            // Intentionally relative paths (verified by earlier ProgramRunTests).
            string jsonConfig =
                "{\n" +
                "  \"dataDirectory\": \"data/gem/sample\",\n" +
                "  \"usEquityFile\": \"us-equity.csv\",\n" +
                "  \"exUsEquityFile\": \"exus-equity.csv\",\n" +
                "  \"safeAssetFile\": \"safe-asset.csv\",\n" +
                "  \"outputSignalsFile\": \"dist/gem/signals.json\",\n" +
                $"  \"lookbackMonths\": {lookbackMonths}\n" +
                "}\n";

            File.WriteAllText(
                configPath,
                jsonConfig,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static void CreateSampleCsvData(string rootDirectory)
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
            File.WriteAllText(fullPath, content.Trim() + Environment.NewLine, Encoding.UTF8);
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

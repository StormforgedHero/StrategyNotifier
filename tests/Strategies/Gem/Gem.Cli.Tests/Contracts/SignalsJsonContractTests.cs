using Gem.Cli.Contracts;
using Gem.Cli.Tests.TestSupport;
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
            using var workspace = new TemporaryWorkspace();

            CreateConfigurationFile(workspace, lookbackMonths: 2);
            CreateSampleCsvData(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

            GemSignalOutput[] signals = ReadSignals(workspace);
            Assert.NotEmpty(signals);

            foreach (GemSignalOutput signal in signals)
            {
                Assert.True(IsValidYearMonth(signal.Period), $"Invalid period: '{signal.Period}'.");
                Assert.True(AllowedPositions.Contains(signal.Position), $"Invalid position: '{signal.Position}'.");
            }
        }

        [Fact]
        public void Run_WritesSignalsJson_SortedByPeriodAscending_AndNoDuplicatePeriods()
        {
            using var workspace = new TemporaryWorkspace();

            CreateConfigurationFile(workspace, lookbackMonths: 2);
            CreateSampleCsvData(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

            GemSignalOutput[] signals = ReadSignals(workspace);
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

        [Fact]
        public void Run_WhenNoSignalsAreGenerated_StillWritesValidEmptyJsonArray()
        {
            using var workspace = new TemporaryWorkspace();

            // Lookback larger than available history -> zero signals expected.
            CreateConfigurationFile(workspace, lookbackMonths: 12);
            CreateSampleCsvData(workspace);

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(0, exitCode);
            Assert.True(string.IsNullOrWhiteSpace(errWriter.ToString()));

            string outputPath = GetSignalsPath(workspace);
            Assert.True(File.Exists(outputPath));

            string json = File.ReadAllText(outputPath, Utf8TestEncoding.Utf8NoBom);

            GemSignalOutput[]? signals =
                JsonSerializer.Deserialize<GemSignalOutput[]>(json);

            Assert.NotNull(signals);
            Assert.Empty(signals!);
        }

        private static GemSignalOutput[] ReadSignals(TemporaryWorkspace workspace)
        {
            string outputPath = GetSignalsPath(workspace);
            Assert.True(File.Exists(outputPath));

            string json = File.ReadAllText(outputPath, Utf8TestEncoding.Utf8NoBom);

            GemSignalOutput[]? signals =
                JsonSerializer.Deserialize<GemSignalOutput[]>(json);

            Assert.NotNull(signals);

            return signals!;
        }

        private static string GetSignalsPath(TemporaryWorkspace workspace)
        {
            return workspace.GetPath("dist", "gem", "signals.json");
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

        private static void CreateConfigurationFile(TemporaryWorkspace workspace, int lookbackMonths)
        {
            string jsonConfig =
                "{\n" +
                "  \"dataDirectory\": \"data/gem/sample\",\n" +
                "  \"usEquityFile\": \"us-equity.csv\",\n" +
                "  \"exUsEquityFile\": \"exus-equity.csv\",\n" +
                "  \"safeAssetFile\": \"safe-asset.csv\",\n" +
                "  \"outputSignalsFile\": \"dist/gem/signals.json\",\n" +
                $"  \"lookbackMonths\": {lookbackMonths}\n" +
                "}\n";

            workspace.WriteText(jsonConfig, "config", "gem", "gem.cli.json");
        }

        private static void CreateSampleCsvData(TemporaryWorkspace workspace)
        {
            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.02
                2025,2,0.03
                2025,3,-0.01
                """,
                "data", "gem", "sample", "us-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.01
                2025,2,0.02
                2025,3,0.00
                """,
                "data", "gem", "sample", "exus-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.002
                2025,2,0.002
                2025,3,0.002
                """,
                "data", "gem", "sample", "safe-asset.csv");
        }
    }
}

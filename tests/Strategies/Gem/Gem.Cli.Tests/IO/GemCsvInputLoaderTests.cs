using Gem.Cli.IO;
using Gem.Cli.Tests.TestSupport;
using Gem.Domain.Core;
using Gem.Domain.Model;

namespace Gem.Cli.Tests.IO
{
    public class GemCsvInputLoaderTests
    {
        [Fact]
        public void Load_WithValidCsvFiles_BuildsExpectedGemInputData()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteDefaultSampleCsvs();

            GemInputData input = context.Load();

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

        [Fact]
        public void Load_WhenCsvContainsInvalidReturnValue_ThrowsFormatException()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteDefaultSampleCsvs();

            context.Workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,not-a-number
                """,
                "us-equity.csv");

            Assert.Throws<FormatException>(() => context.Load());
        }

        [Fact]
        public void Load_WhenSafeAssetFileIsMissing_ThrowsFileNotFoundException()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteDefaultSampleCsvs();
            context.DeleteSafeAssetCsv();

            FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => context.Load());

            Assert.NotNull(exception.FileName);
            Assert.EndsWith("safe-asset.csv", exception.FileName!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Load_WhenCsvFilesContainOnlyHeader_ReturnsEmptySeries()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteHeaderOnlyCsvs();

            GemInputData input = context.Load();

            Assert.Empty(input.UsEquity.Returns);
            Assert.Empty(input.ExUsEquity.Returns);
            Assert.Empty(input.SafeAsset.Returns);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WithNullOrWhitespaceBaseDirectory_ThrowsArgumentException(string? baseDirectory)
        {
            var exception = Assert.Throws<ArgumentException>(() => new GemCsvInputLoader(baseDirectory!));
            Assert.Equal("baseDirectory", exception.ParamName);
        }

        [Fact]
        public void Load_WhenCsvContainsWhitespaceAroundValues_ParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                Year,Month,Return
                 2025 ,  1 ,  0.02
                 2025 ,  2 ,  0.03
                """,
                exUsEquityCsv: """
                Year,Month,Return
                 2025 ,  1 ,  0.01
                 2025 ,  2 ,  0.02
                """,
                safeAssetCsv: """
                Year,Month,Return
                 2025 ,  1 ,  0.002
                 2025 ,  2 ,  0.002
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 1), input.UsEquity.Returns[0].Period);
            Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);

            Assert.Equal(2, input.ExUsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.ExUsEquity.Returns[1].Period);
            Assert.Equal(0.02m, input.ExUsEquity.Returns[1].Rate);

            Assert.Equal(2, input.SafeAsset.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.SafeAsset.Returns[1].Period);
            Assert.Equal(0.002m, input.SafeAsset.Returns[1].Rate);
        }

        [Fact]
        public void Load_WhenCsvContainsCommentLines_IgnoresThemAndParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                Year,Month,Return
                # Source: synthetic
                2025,1,0.02
                # another comment
                2025,2,0.03
                """,
                exUsEquityCsv: """
                Year,Month,Return
                # comment line
                2025,1,0.01
                2025,2,0.02
                """,
                safeAssetCsv: """
                Year,Month,Return
                # comment line
                2025,1,0.002
                2025,2,0.002
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 1), input.UsEquity.Returns[0].Period);
            Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);

            Assert.Equal(2, input.ExUsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.ExUsEquity.Returns[1].Period);
            Assert.Equal(0.02m, input.ExUsEquity.Returns[1].Rate);

            Assert.Equal(2, input.SafeAsset.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.SafeAsset.Returns[1].Period);
            Assert.Equal(0.002m, input.SafeAsset.Returns[1].Rate);
        }

        [Fact]
        public void Load_WhenCsvContainsLeadingCommentsBeforeHeader_IgnoresThemAndParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                # Exported from some tool
                # Any metadata here

                Year,Month,Return
                2025,1,0.02
                2025,2,0.03
                """,
                exUsEquityCsv: """
                # Exported from some tool

                Year,Month,Return
                2025,1,0.01
                2025,2,0.02
                """,
                safeAssetCsv: """
                # Exported from some tool

                Year,Month,Return
                2025,1,0.002
                2025,2,0.002
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 1), input.UsEquity.Returns[0].Period);
            Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);

            Assert.Equal(2, input.ExUsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.ExUsEquity.Returns[1].Period);
            Assert.Equal(0.02m, input.ExUsEquity.Returns[1].Rate);

            Assert.Equal(2, input.SafeAsset.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.SafeAsset.Returns[1].Period);
            Assert.Equal(0.002m, input.SafeAsset.Returns[1].Rate);
        }

        [Fact]
        public void Load_WhenCsvUsesSemicolonDelimiter_ParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                Year;Month;Return
                2025;1;0.02
                2025;2;0.03
                """,
                exUsEquityCsv: """
                Year;Month;Return
                2025;1;0.01
                2025;2;0.02
                """,
                safeAssetCsv: """
                Year;Month;Return
                2025;1;0.002
                2025;2;0.002
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 1), input.UsEquity.Returns[0].Period);
            Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);

            Assert.Equal(2, input.ExUsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.ExUsEquity.Returns[1].Period);
            Assert.Equal(0.02m, input.ExUsEquity.Returns[1].Rate);

            Assert.Equal(2, input.SafeAsset.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.SafeAsset.Returns[1].Period);
            Assert.Equal(0.002m, input.SafeAsset.Returns[1].Rate);
        }

        [Fact]
        public void Load_WhenSemicolonDelimiterAndCommaDecimalSeparator_ParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                Year;Month;Return
                2025;1;0,02
                2025;2;0,03
                """,
                exUsEquityCsv: """
                Year;Month;Return
                2025;1;0,01
                2025;2;0,02
                """,
                safeAssetCsv: """
                Year;Month;Return
                2025;1;0,002
                2025;2;0,002
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 1), input.UsEquity.Returns[0].Period);
            Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);

            Assert.Equal(2, input.ExUsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.ExUsEquity.Returns[1].Period);
            Assert.Equal(0.02m, input.ExUsEquity.Returns[1].Rate);

            Assert.Equal(2, input.SafeAsset.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.SafeAsset.Returns[1].Period);
            Assert.Equal(0.002m, input.SafeAsset.Returns[1].Rate);
        }

        [Fact]
        public void Load_WhenCsvContainsAdditionalColumns_IgnoresExtrasAndParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                Year,Month,Return,Close,Volume
                2025,1,0.02,100.0,1000
                2025,2,0.03,101.0,1100
                """,
                exUsEquityCsv: """
                Year,Month,Return,Close
                2025,1,0.01,200.0
                2025,2,0.02,201.0
                """,
                safeAssetCsv: """
                Year,Month,Return,Note
                2025,1,0.002,synthetic
                2025,2,0.002,synthetic
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 1), input.UsEquity.Returns[0].Period);
            Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);

            Assert.Equal(2, input.ExUsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.ExUsEquity.Returns[1].Period);
            Assert.Equal(0.02m, input.ExUsEquity.Returns[1].Rate);

            Assert.Equal(2, input.SafeAsset.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.SafeAsset.Returns[1].Period);
            Assert.Equal(0.002m, input.SafeAsset.Returns[1].Rate);
        }

        [Fact]
        public void Load_WhenReturnHasTrailingHashComment_ParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                Year,Month,Return
                2025,1,0.02 # corrected
                2025,2,0.03 # corrected
                """,
                exUsEquityCsv: """
                Year,Month,Return
                2025,1,0.01 # note
                2025,2,0.02 # note
                """,
                safeAssetCsv: """
                Year,Month,Return
                2025,1,0.002 # ok
                2025,2,0.002 # ok
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 1), input.UsEquity.Returns[0].Period);
            Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);
        }

        [Fact]
        public void Load_WhenReturnHasTrailingDoubleSlashComment_ParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                Year,Month,Return
                2025,1,0.02 // corrected
                2025,2,0.03 // corrected
                """,
                exUsEquityCsv: """
                Year,Month,Return
                2025,1,0.01 // note
                2025,2,0.02 // note
                """,
                safeAssetCsv: """
                Year,Month,Return
                2025,1,0.002 // ok
                2025,2,0.002 // ok
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.SafeAsset.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.SafeAsset.Returns[1].Period);
            Assert.Equal(0.002m, input.SafeAsset.Returns[1].Rate);
        }

        [Fact]
        public void Load_WhenSemicolonDelimiterAndCommaDecimalAndTrailingComment_ParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                Year;Month;Return
                2025;1;0,02 # corrected
                2025;2;0,03 # corrected
                """,
                exUsEquityCsv: """
                Year;Month;Return
                2025;1;0,01 // note
                2025;2;0,02 // note
                """,
                safeAssetCsv: """
                Year;Month;Return
                2025;1;0,002 # ok
                2025;2;0,002 # ok
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);
            Assert.Equal(0.03m, input.UsEquity.Returns[1].Rate);
        }

        [Fact]
        public void Load_WhenHeaderHasUtf8Bom_ParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: "\uFEFFYear,Month,Return\n2025,1,0.02\n2025,2,0.03\n",
                exUsEquityCsv: """
                Year,Month,Return
                2025,1,0.01
                2025,2,0.02
                """,
                safeAssetCsv: """
                Year,Month,Return
                2025,1,0.002
                2025,2,0.002
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 1), input.UsEquity.Returns[0].Period);
            Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);
        }

        [Fact]
        public void Load_WhenValuesAreQuotedWithCommaDelimiter_ParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                Year,Month,Return
                "2025","1","0.02"
                "2025","2","0.03"
                """,
                exUsEquityCsv: """
                Year,Month,Return
                2025,1,"0.01"
                2025,2,"0.02"
                """,
                safeAssetCsv: """
                Year,Month,Return
                2025,1,0.002
                2025,2,0.002
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.UsEquity.Returns[1].Period);
            Assert.Equal(0.03m, input.UsEquity.Returns[1].Rate);
        }

        [Fact]
        public void Load_WhenSemicolonDelimiterAndCommaDecimalAndQuotedReturn_ParsesSuccessfully()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                Year;Month;Return
                2025;1;"0,02"
                2025;2;"0,03"
                """,
                exUsEquityCsv: """
                Year;Month;Return
                2025;1;"0,01"
                2025;2;"0,02"
                """,
                safeAssetCsv: """
                Year;Month;Return
                2025;1;"0,002"
                2025;2;"0,002"
                """);

            GemInputData input = context.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);
            Assert.Equal(0.03m, input.UsEquity.Returns[1].Rate);
        }

        [Fact]
        public void Load_WhenCsvHeaderIsInvalid_ThrowsFormatsFormatException()
        {
            using var context = CsvInputLoaderTestContext.Create();

            context.WriteCsvs(
                usEquityCsv: """
                Year,Month,Rate
                2025,1,0.02
                """,
                exUsEquityCsv: """
                Year,Month,Return
                2025,1,0.01
                """,
                safeAssetCsv: """
                Year,Month,Return
                2025,1,0.002
                """);

            FormatException exception = Assert.Throws<FormatException>(() => context.Load());

            Assert.Contains("Invalid CSV header", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Year,Month,Return", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class CsvInputLoaderTestContext : IDisposable
        {
            public TemporaryWorkspace Workspace { get; }
            public string DirectoryPath => Workspace.Root;

            private CsvInputLoaderTestContext(TemporaryWorkspace workspace)
            {
                Workspace = workspace;
            }

            public static CsvInputLoaderTestContext Create()
            {
                return new CsvInputLoaderTestContext(new TemporaryWorkspace());
            }

            public GemInputData Load()
            {
                var loader = new GemCsvInputLoader(DirectoryPath);
                return loader.Load();
            }

            public void WriteDefaultSampleCsvs()
            {
                GemCliTestData.WriteDefaultSampleCsvs(DirectoryPath);
            }

            public void WriteCsvs(string usEquityCsv, string exUsEquityCsv, string safeAssetCsv)
            {
                Workspace.WriteCsv(usEquityCsv, "us-equity.csv");
                Workspace.WriteCsv(exUsEquityCsv, "exus-equity.csv");
                Workspace.WriteCsv(safeAssetCsv, "safe-asset.csv");
            }

            public void WriteHeaderOnlyCsvs()
            {
                Workspace.WriteCsv("Year,Month,Return", "us-equity.csv");
                Workspace.WriteCsv("Year,Month,Return", "exus-equity.csv");
                Workspace.WriteCsv("Year,Month,Return", "safe-asset.csv");
            }

            public void DeleteSafeAssetCsv()
            {
                string safePath = Workspace.GetPath("safe-asset.csv");
                if (File.Exists(safePath))
                {
                    File.Delete(safePath);
                }
            }

            public void Dispose()
            {
                Workspace.Dispose();
            }
        }
    }
}

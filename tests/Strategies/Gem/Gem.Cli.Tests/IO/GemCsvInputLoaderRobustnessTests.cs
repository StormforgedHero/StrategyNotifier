using Gem.Cli.IO;
using Gem.Cli.Tests.TestSupport;
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
            using var workspace = new TemporaryWorkspace();

            WriteCsvWithBom(
                workspace,
                "us-equity.csv",
                """
                Year,Month,Return
                2025,1,0.02
                """);

            WriteCsvWithBom(
                workspace,
                "exus-equity.csv",
                """
                Year,Month,Return
                2025,1,0.01
                """);

            WriteCsvWithBom(
                workspace,
                "safe-asset.csv",
                """
                Year,Month,Return
                2025,1,0.002
                """);

            var loader = new GemCsvInputLoader(workspace.Root);

            GemInputData input = loader.Load();

            Assert.Equal(AssetKind.UsEquity, input.UsEquity.AssetKind);
            Assert.Single(input.UsEquity.Returns);
            Assert.Equal(new YearMonth(2025, 1), input.UsEquity.Returns[0].Period);
            Assert.Equal(0.02m, input.UsEquity.Returns[0].Rate);
        }

        [Fact]
        public void Load_WhenColumnsContainWhitespace_TrimsAndParsesSuccessfully()
        {
            using var workspace = new TemporaryWorkspace();

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025 , 1 , 0.02
                2025 , 2 , 0.03
                """,
                "us-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025 , 1 , 0.01
                2025 , 2 , 0.02
                """,
                "exus-equity.csv");

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025 , 1 , 0.002
                2025 , 2 , 0.002
                """,
                "safe-asset.csv");

            var loader = new GemCsvInputLoader(workspace.Root);

            GemInputData input = loader.Load();

            Assert.Equal(2, input.UsEquity.Returns.Count);
            Assert.Equal(new YearMonth(2025, 2), input.UsEquity.Returns[1].Period);
            Assert.Equal(0.03m, input.UsEquity.Returns[1].Rate);
        }

        private static void WriteCsvWithBom(
            TemporaryWorkspace workspace,
            string fileName,
            string content)
        {
            string fullPath = workspace.GetPath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

            File.WriteAllText(fullPath, content.Trim() + "\n", utf8WithBom);
        }
    }
}

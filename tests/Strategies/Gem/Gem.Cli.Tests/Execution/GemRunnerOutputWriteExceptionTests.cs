using Gem.Cli.Configuration;
using Gem.Cli.Execution;
using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.Execution
{
    public class GemRunnerOutputWriteExceptionTests
    {
        [Fact]
        public void Run_WhenOutputPathIsAnExistingDirectory_ThrowsGemOutputWriteException()
        {
            using var workspace = new TemporaryWorkspace();

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

            string outputPath = workspace.GetPath("dist", "gem", "signals.json");

            // Create a directory where the file should be written -> deterministic failure.
            Directory.CreateDirectory(outputPath);

            var configuration = new GemCliConfiguration
            {
                DataDirectory = workspace.GetPath("data", "gem", "sample"),
                UsEquityFile = "us-equity.csv",
                ExUsEquityFile = "exus-equity.csv",
                SafeAssetFile = "safe-asset.csv",
                OutputSignalsFile = outputPath,
                LookbackMonths = 2
            };

            configuration.Validate();

            var runner = new GemRunner(configuration);

            GemOutputWriteException exception =
                Assert.Throws<GemOutputWriteException>(() => runner.Run());

            Assert.Equal(outputPath, exception.OutputPath);
        }
    }
}

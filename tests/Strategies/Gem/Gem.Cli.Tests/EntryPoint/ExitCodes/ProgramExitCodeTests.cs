using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.EntryPoint.ExitCodes
{
    public class ProgramExitCodeTests
    {
        [Fact]
        public void Run_WhenConfigurationFileIsMissing_ReturnsConfigurationFileNotFoundExitCode()
        {
            using var workspace = new TemporaryWorkspace();

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(Cli.Program.ExitCodeConfigurationFileNotFound, exitCode);
        }

        [Fact]
        public void Run_WhenConfigurationJsonIsInvalid_ReturnsConfigurationErrorExitCode()
        {
            using var workspace = new TemporaryWorkspace();

            WriteConfigurationFile(
                workspace,
                """
                {
                  "dataDirectory": "data/gem/sample"
                """); // Missing closing brace -> invalid JSON.

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(Cli.Program.ExitCodeConfigurationError, exitCode);
        }

        [Fact]
        public void Run_WhenInputDataFileIsMissing_ReturnsInputDataFileNotFoundExitCode()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);

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

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(Cli.Program.ExitCodeInputDataFileNotFound, exitCode);
        }

        [Fact]
        public void Run_WhenInputDataHasInvalidNumber_ReturnsInputDataFormatErrorExitCode()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,not-a-number
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

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(Cli.Program.ExitCodeInputDataFormatError, exitCode);
        }

        [Fact]
        public void Run_WhenInputDataContainsDuplicatePeriods_ReturnsInputDataValidationErrorExitCode()
        {
            using var workspace = new TemporaryWorkspace();

            CreateValidConfiguration(workspace, lookbackMonths: 2);

            workspace.WriteCsv(
                """
                Year,Month,Return
                2025,1,0.02
                2025,1,0.03
                2025,2,0.01
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

            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Cli.Program.Run(workspace.Root, outWriter, errWriter);

            Assert.Equal(Cli.Program.ExitCodeInputDataValidationError, exitCode);
        }

        private static void CreateValidConfiguration(TemporaryWorkspace workspace, int lookbackMonths)
        {
            string jsonConfig = $@"{{
  ""dataDirectory"": ""data/gem/sample"",
  ""usEquityFile"": ""us-equity.csv"",
  ""exUsEquityFile"": ""exus-equity.csv"",
  ""safeAssetFile"": ""safe-asset.csv"",
  ""outputSignalsFile"": ""dist/gem/signals.json"",
  ""lookbackMonths"": {lookbackMonths}
}}";

            workspace.WriteText(jsonConfig, "config", "gem", "gem.cli.json");
        }

        private static void WriteConfigurationFile(TemporaryWorkspace workspace, string jsonConfig)
        {
            workspace.WriteText(jsonConfig, "config", "gem", "gem.cli.json");
        }
    }
}

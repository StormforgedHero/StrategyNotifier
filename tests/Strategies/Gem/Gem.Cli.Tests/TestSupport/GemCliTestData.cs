using Gem.Cli.Configuration;

namespace Gem.Cli.Tests.TestSupport
{
    internal static class GemCliTestData
    {
        public static void WriteDefaultSampleCsvs(string dataDirectory)
        {
            TestFileSystem.WriteCsv(
                dataDirectory,
                "us-equity.csv",
                """
                Year,Month,Return
                2025,1,0.02
                2025,2,0.03
                2025,3,-0.01
                """);

            TestFileSystem.WriteCsv(
                dataDirectory,
                "exus-equity.csv",
                """
                Year,Month,Return
                2025,1,0.01
                2025,2,0.02
                2025,3,0.00
                """);

            TestFileSystem.WriteCsv(
                dataDirectory,
                "safe-asset.csv",
                """
                Year,Month,Return
                2025,1,0.002
                2025,2,0.002
                2025,3,0.002
                """);
        }

        public static GemCliConfiguration CreateValidConfiguration(
            string dataDirectory,
            string outputPath,
            int lookbackMonths)
        {
            var configuration = new GemCliConfiguration
            {
                DataDirectory = dataDirectory,
                UsEquityFile = "us-equity.csv",
                ExUsEquityFile = "exus-equity.csv",
                SafeAssetFile = "safe-asset.csv",
                OutputSignalsFile = outputPath,
                LookbackMonths = lookbackMonths
            };

            configuration.Validate();

            return configuration;
        }

        public static void WriteGemCliJsonConfig(
            string rootDirectory,
            int lookbackMonths = 2,
            string dataDirectory = "data/gem/sample",
            string outputSignalsFile = "dist/gem/signals.json")
        {
            string configDirectory = Path.Combine(rootDirectory, "config", "gem");
            Directory.CreateDirectory(configDirectory);

            string jsonConfig = $$"""
            {
              "dataDirectory": "{{dataDirectory}}",
              "usEquityFile": "us-equity.csv",
              "exUsEquityFile": "exus-equity.csv",
              "safeAssetFile": "safe-asset.csv",
              "outputSignalsFile": "{{outputSignalsFile}}",
              "lookbackMonths": {{lookbackMonths}}
            }
            """;

            TestFileSystem.WriteTextFile(configDirectory, "gem.cli.json", jsonConfig);
        }
    }
}

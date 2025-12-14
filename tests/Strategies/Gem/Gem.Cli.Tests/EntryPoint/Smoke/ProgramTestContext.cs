using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests.EntryPoint.Smoke
{
    internal sealed class ProgramTestContext : IDisposable
    {
        private const string DefaultDataDirectoryRelative = "data/gem/sample";
        private const string DefaultOutputSignalsFileRelative = "dist/gem/signals.json";

        public TemporaryWorkspace Workspace { get; }
        public string RootDirectory => Workspace.Root;
        public string DataDirectory => Workspace.GetPath("data", "gem", "sample");
        public string OutputPath => Workspace.GetPath("dist", "gem", "signals.json");

        private ProgramTestContext(TemporaryWorkspace workspace)
        {
            Workspace = workspace;
        }

        public static ProgramTestContext Create()
        {
            return new ProgramTestContext(new TemporaryWorkspace());
        }

        public ProgramRunResult Run()
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();

            int exitCode = Program.Run(RootDirectory, outWriter, errWriter);

            return new ProgramRunResult(
                exitCode,
                outWriter.ToString(),
                errWriter.ToString());
        }

        public void WriteValidConfig(int lookbackMonths)
        {
            GemCliTestData.WriteGemCliJsonConfig(
                RootDirectory,
                lookbackMonths: lookbackMonths,
                dataDirectory: DefaultDataDirectoryRelative,
                outputSignalsFile: DefaultOutputSignalsFileRelative);
        }

        public void WriteInvalidJsonConfig(string jsonContent)
        {
            Workspace.WriteText(jsonContent, "config", "gem", "gem.cli.json");
        }

        public void WriteDefaultSampleData()
        {
            GemCliTestData.WriteDefaultSampleCsvs(DataDirectory);
        }

        public void WriteDataMissingSafeAssetFile()
        {
            GemCliTestData.WriteDefaultSampleCsvs(DataDirectory);

            string safeAssetPath = Workspace.GetPath("data", "gem", "sample", "safe-asset.csv");
            File.Delete(safeAssetPath);
        }

        public void Dispose()
        {
            Workspace.Dispose();
        }
    }
}

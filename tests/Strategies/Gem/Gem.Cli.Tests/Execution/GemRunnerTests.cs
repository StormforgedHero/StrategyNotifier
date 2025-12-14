using Gem.Cli.Configuration;
using Gem.Cli.Contracts;
using Gem.Cli.Execution;
using Gem.Cli.Tests.TestSupport;
using Gem.Domain.Core;
using System.Text.Json;

namespace Gem.Cli.Tests.Execution
{
    public class GemRunnerTests
    {
        [Fact]
        public void Run_WithValidConfiguration_ProducesSignalsFile()
        {
            using var context = RunnerTestContext.Create(lookbackMonths: 2);

            int signalCount = context.Runner.Run();

            Assert.True(signalCount > 0);
            Assert.True(File.Exists(context.OutputPath));

            string json = ReadJson(context.OutputPath, context.Workspace);
            List<GemSignalOutput> outputs = DeserializeOutputs(json);

            Assert.Equal(signalCount, outputs.Count);

            foreach (GemSignalOutput output in outputs)
            {
                Assert.False(string.IsNullOrWhiteSpace(output.Period));
                Assert.False(string.IsNullOrWhiteSpace(output.Position));
            }
        }

        [Fact]
        public void Run_WithValidConfiguration_ProducesExpectedSignalContent()
        {
            using var context = RunnerTestContext.Create(lookbackMonths: 2);

            int signalCount = context.Runner.Run();

            Assert.Equal(2, signalCount);
            Assert.True(File.Exists(context.OutputPath));

            string json = ReadJson(context.OutputPath, context.Workspace);
            List<GemSignalOutput> outputs = DeserializeOutputs(json);

            Assert.Equal(signalCount, outputs.Count);

            // Expected:
            // 2025-02: UsEquity
            // 2025-03: UsEquity
            Assert.Equal("2025-02", outputs[0].Period);
            Assert.Equal("UsEquity", outputs[0].Position);

            Assert.Equal("2025-03", outputs[1].Period);
            Assert.Equal("UsEquity", outputs[1].Position);
        }

        [Fact]
        public void Run_WithValidConfiguration_WritesSignalsWithValidContract()
        {
            using var context = RunnerTestContext.Create(lookbackMonths: 2);

            int signalCount = context.Runner.Run();

            Assert.Equal(2, signalCount);
            Assert.True(File.Exists(context.OutputPath));

            string json = ReadJson(context.OutputPath, context.Workspace);
            List<GemSignalOutput> outputs = DeserializeOutputs(json);

            Assert.Equal(signalCount, outputs.Count);

            var seenPeriods = new HashSet<string>(StringComparer.Ordinal);
            YearMonth? previous = null;

            foreach (GemSignalOutput output in outputs)
            {
                Assert.False(string.IsNullOrWhiteSpace(output.Period));
                Assert.False(string.IsNullOrWhiteSpace(output.Position));

                // Period must be in "YYYY-MM" format.
                Assert.Equal(7, output.Period.Length);
                Assert.Equal('-', output.Period[4]);

                bool yearParsed = int.TryParse(output.Period.AsSpan(0, 4), out int year);
                bool monthParsed = int.TryParse(output.Period.AsSpan(5, 2), out int month);

                Assert.True(yearParsed);
                Assert.True(monthParsed);
                Assert.InRange(month, 1, 12);

                // No duplicate periods.
                Assert.True(seenPeriods.Add(output.Period));

                // Periods must be strictly increasing (sorted ascending).
                var current = new YearMonth(year, month);

                if (previous is not null)
                {
                    Assert.True(previous.Value < current);
                }

                previous = current;

                // Position must be a valid AssetKind name.
                bool positionParsed = Enum.TryParse<AssetKind>(output.Position, ignoreCase: false, out _);
                Assert.True(positionParsed);
            }
        }

        [Fact]
        public void Run_WithValidConfiguration_WritesSignalsFileAsUtf8WithoutBom()
        {
            using var context = RunnerTestContext.Create(lookbackMonths: 2);

            int signalCount = context.Runner.Run();

            Assert.Equal(2, signalCount);
            Assert.True(File.Exists(context.OutputPath));

            byte[] bytes = context.Workspace.ReadAllBytes("dist", "gem", "signals.json");

            // UTF-8 BOM: EF BB BF
            bool hasBom = bytes.Length >= 3
                && bytes[0] == 0xEF
                && bytes[1] == 0xBB
                && bytes[2] == 0xBF;

            Assert.False(hasBom);
        }

        [Fact]
        public void Run_WhenOutputDirectoryDoesNotExist_CreatesItAndWritesSignalsFile()
        {
            using var context = RunnerTestContext.Create(lookbackMonths: 2);

            Assert.False(Directory.Exists(context.DistDirectory));

            int signalCount = context.Runner.Run();

            Assert.Equal(2, signalCount);
            Assert.True(Directory.Exists(context.DistDirectory));
            Assert.True(File.Exists(context.OutputPath));
        }

        [Fact]
        public void Run_WhenOutputFileAlreadyExists_OverwritesItWithValidJson()
        {
            using var context = RunnerTestContext.Create(
                lookbackMonths: 2,
                createOutputDirectory: true,
                createExistingOutputFile: true);

            int signalCount = context.Runner.Run();

            Assert.Equal(2, signalCount);
            Assert.True(File.Exists(context.OutputPath));

            string json = ReadJson(context.OutputPath, context.Workspace);
            Assert.DoesNotContain("OLD_CONTENT_SHOULD_BE_OVERWRITTEN", json, StringComparison.Ordinal);

            List<GemSignalOutput> outputs = DeserializeOutputs(json);
            Assert.Equal(signalCount, outputs.Count);
        }

        [Fact]
        public void Run_WhenRunTwiceWithSameInputs_ProducesIdenticalJson()
        {
            using var context = RunnerTestContext.Create(lookbackMonths: 2);

            Assert.Equal(2, context.Runner.Run());

            string json1 = ReadJson(context.OutputPath, context.Workspace);

            Assert.Equal(2, context.Runner.Run());

            string json2 = ReadJson(context.OutputPath, context.Workspace);

            Assert.Equal(json1, json2);
        }

        [Fact]
        public void Run_WritesIndentedJson()
        {
            using var context = RunnerTestContext.Create(lookbackMonths: 2);

            Assert.Equal(2, context.Runner.Run());

            string json = ReadJson(context.OutputPath, context.Workspace);

            bool looksIndented = json.Contains("\n  {", StringComparison.Ordinal)
                || json.Contains("\r\n  {", StringComparison.Ordinal);

            Assert.True(looksIndented);
        }

        private static string ReadJson(string path, TemporaryWorkspace workspace)
        {
            return workspace.ReadAllText("dist", "gem", "signals.json");
        }

        private static List<GemSignalOutput> DeserializeOutputs(string json)
        {
            List<GemSignalOutput>? outputs =
                JsonSerializer.Deserialize<List<GemSignalOutput>>(json);

            Assert.NotNull(outputs);
            return outputs!;
        }

        private sealed class RunnerTestContext : IDisposable
        {
            public TemporaryWorkspace Workspace { get; }
            public string RootDirectory { get; }
            public string DataDirectory { get; }
            public string OutputPath { get; }
            public string DistDirectory { get; }
            public GemCliConfiguration Configuration { get; }
            public GemRunner Runner { get; }

            private RunnerTestContext(
                TemporaryWorkspace workspace,
                string dataDirectory,
                string outputPath,
                GemCliConfiguration configuration,
                GemRunner runner)
            {
                Workspace = workspace;
                RootDirectory = workspace.Root;
                DataDirectory = dataDirectory;
                OutputPath = outputPath;
                DistDirectory = Path.GetDirectoryName(outputPath)!;
                Configuration = configuration;
                Runner = runner;
            }

            public static RunnerTestContext Create(
                int lookbackMonths,
                bool createOutputDirectory = false,
                bool createExistingOutputFile = false)
            {
                var workspace = new TemporaryWorkspace();

                string dataDirectory = workspace.GetPath("data", "gem", "sample");
                GemCliTestData.WriteDefaultSampleCsvs(dataDirectory);

                string outputPath = workspace.GetPath("dist", "gem", "signals.json");

                if (createOutputDirectory)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                }

                if (createExistingOutputFile)
                {
                    workspace.WriteText("OLD_CONTENT_SHOULD_BE_OVERWRITTEN", "dist", "gem", "signals.json");
                }

                GemCliConfiguration configuration =
                    GemCliTestData.CreateValidConfiguration(dataDirectory, outputPath, lookbackMonths);

                var runner = new GemRunner(configuration);

                return new RunnerTestContext(workspace, dataDirectory, outputPath, configuration, runner);
            }

            public void Dispose()
            {
                Workspace.Dispose();
            }
        }
    }
}

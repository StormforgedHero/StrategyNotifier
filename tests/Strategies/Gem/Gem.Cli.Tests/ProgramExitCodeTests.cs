using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests;

public sealed class ProgramExitCodeTests
{
    [Fact]
    public void MissingConfig_ReturnsConfigurationFileNotFound()
    {
        using var workspace = new TemporaryWorkspace();
        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), Array.Empty<string>());
        Assert.Equal(Program.ExitCodeConfigurationFileNotFound, exitCode);
    }

    [Fact]
    public void InvalidJson_ReturnsConfigurationError()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteText("{ invalid json", "config", "gem", "gem.config.json");

        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), Array.Empty<string>());

        Assert.Equal(Program.ExitCodeConfigurationError, exitCode);
    }

    [Fact]
    public void MissingCsv_ReturnsInputDataFileNotFound()
    {
        using var workspace = new TemporaryWorkspace();
        string dataDir = workspace.GetPath("data");
        string cacheDir = workspace.GetPath("cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        string json = $$"""
        {
          "windowMonths": 3,
          "rankingMode": "Top1",
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "{{dataDir.Replace("\\", "\\\\")}}",
          "cacheDirectory": "{{cacheDir.Replace("\\", "\\\\")}}",
          "outputPath": "{{outputPath.Replace("\\", "\\\\")}}",
          "update": { "autoUpdateEnabled": false, "maxAgeDays": 2 }
        }
        """;

        workspace.WriteText(json, "config", "gem", "gem.config.json");

        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), new[] { "--no-update" });

        Assert.Equal(Program.ExitCodeInputDataFileNotFound, exitCode);
    }

    [Fact]
    public void OutputWriteError_ReturnsSpecificExitCode()
    {
        using var workspace = new TemporaryWorkspace();
        string dataDir = workspace.GetPath("data");
        Directory.CreateDirectory(dataDir);
        string cacheDir = workspace.GetPath("cache");

        WritePriceFile(dataDir, "voo.us.csv");
        WritePriceFile(dataDir, "veu.us.csv");
        WritePriceFile(dataDir, "agg.us.csv");

        string outputPath = workspace.Root;

        string json = $$"""
        {
          "windowMonths": 3,
          "rankingMode": "Top1",
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "{{dataDir.Replace("\\", "\\\\")}}",
          "cacheDirectory": "{{cacheDir.Replace("\\", "\\\\")}}",
          "outputPath": "{{outputPath.Replace("\\", "\\\\")}}",
          "update": { "autoUpdateEnabled": false, "maxAgeDays": 2 }
        }
        """;

        workspace.WriteText(json, "config", "gem", "gem.config.json");

        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), new[] { "--no-update" });

        Assert.Equal(Program.ExitCodeOutputWriteError, exitCode);
    }

    private static void WritePriceFile(string directory, string fileName)
    {
        File.WriteAllText(
            Path.Combine(directory, fileName),
            """
            Date,Close
            2024-01-31,100
            2024-02-29,101
            2024-03-29,102
            """);
    }
}

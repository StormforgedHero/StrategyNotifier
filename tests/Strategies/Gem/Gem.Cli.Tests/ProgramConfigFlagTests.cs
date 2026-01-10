using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests;

public sealed class ProgramConfigFlagTests
{
    [Fact]
    public void Program_ConfigFlag_LoadsCustomConfigAndWritesOutput()
    {
        using var workspace = new TemporaryWorkspace();
        string dataDir = workspace.GetPath("data", "gem", "custom");
        string cacheDir = workspace.GetPath("cache");
        string outputPath = workspace.GetPath("dist", "gem", "us", "signals.json");
        string configPath = workspace.GetPath("configs", "custom.gem.config.json");

        WritePriceFile(dataDir, "voo.us.csv");
        WritePriceFile(dataDir, "veu.us.csv");
        WritePriceFile(dataDir, "agg.us.csv");

        string json = $$"""
        {
          "windowMonths": 2,
          "rankingMode": "Top1",
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "US Equity", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "Ex-US Equity", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "Bonds", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "{{dataDir.Replace("\\", "\\\\")}}",
          "cacheDirectory": "{{cacheDir.Replace("\\", "\\\\")}}",
          "outputPath": "{{outputPath.Replace("\\", "\\\\")}}",
          "update": { "autoUpdateEnabled": false, "maxAgeDays": 2, "minMinutesBetweenAttempts": 30 }
        }
        """;

        workspace.WriteText(json, "configs", "custom.gem.config.json");

        var output = new StringWriter();
        var error = new StringWriter();

        string relativeConfig = Path.GetRelativePath(workspace.Root, configPath);

        int exitCode = Program.Run(workspace.Root, output, error, new[] { "--config", relativeConfig, "--no-update" });

        Assert.Equal(Program.ExitCodeSuccess, exitCode);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void Program_ConfigFlag_MissingValue_ReturnsConfigError()
    {
        using var workspace = new TemporaryWorkspace();
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = Program.Run(workspace.Root, output, error, new[] { "--config" });

        Assert.Equal(Program.ExitCodeConfigurationError, exitCode);
        Assert.Contains("--config requires a file path", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_ConfigFlag_StopsWhenNextArgumentIsFlag()
    {
        using var workspace = new TemporaryWorkspace();
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = Program.Run(workspace.Root, output, error, new[] { "--config", "--no-update" });

        Assert.Equal(Program.ExitCodeConfigurationError, exitCode);
        Assert.Contains("--config requires a file path", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_ProfileFlag_MissingValue_ReturnsConfigError()
    {
        using var workspace = new TemporaryWorkspace();
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = Program.Run(workspace.Root, output, error, new[] { "--profile", "--no-update" });

        Assert.Equal(Program.ExitCodeConfigurationError, exitCode);
        Assert.Contains("--profile requires an id", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static void WritePriceFile(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, fileName),
            """
            Date,Close
            2024-01-31,100
            2024-02-29,101
            2024-03-29,102
            2024-04-30,103
            2024-05-31,104
            """);
    }
}

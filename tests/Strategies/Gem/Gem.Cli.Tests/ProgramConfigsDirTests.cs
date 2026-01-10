using Gem.Cli.Tests.TestSupport;
using System.Text.Json;

namespace Gem.Cli.Tests;

public sealed class ProgramConfigsDirTests
{
    [Fact]
    public void Program_ConfigsDir_RunsMultipleProfilesAndWritesManifest()
    {
        using var workspace = new TemporaryWorkspace();
        string profilesDir = workspace.GetPath("profiles");
        string cacheDir = workspace.GetPath("cache");
        string dataDir = workspace.GetPath("data");

        WriteCommonPrices(dataDir);

        string outputUs = workspace.GetPath("dist", "gem", "us.signals.json");
        string outputEu = workspace.GetPath("dist", "gem", "eu.signals.json");

        WriteConfig(workspace, Path.Combine("profiles", "us.profile.json"), dataDir, cacheDir, outputUs, "VOO.US", "VEU.US", "AGG.US");
        WriteConfig(workspace, Path.Combine("profiles", "eu.profile.json"), dataDir, cacheDir, outputEu, "SPPW.DE", "IS3N.DE", "EUNA.DE");

        Assert.True(File.Exists(Path.Combine(profilesDir, "us.profile.json")), "US config missing");
        Assert.True(File.Exists(Path.Combine(profilesDir, "eu.profile.json")), "EU config missing");

        var output = new StringWriter();
        var error = new StringWriter();

        string relativeProfiles = Path.GetRelativePath(workspace.Root, profilesDir);

        int exitCode = Program.Run(workspace.Root, output, error, new[] { "--configs-dir", relativeProfiles, "--no-update" });

        Assert.True(exitCode == Program.ExitCodeSuccess, error.ToString());
        Assert.True(File.Exists(outputUs));
        Assert.True(File.Exists(outputEu));

        string manifestPath = workspace.GetPath("dist", "gem", "profiles.json");
        Assert.True(File.Exists(manifestPath));

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement profilesElement = manifest.RootElement.GetProperty("profiles");
        Assert.Equal(JsonValueKind.Array, profilesElement.ValueKind);
        Assert.Equal(2, profilesElement.GetArrayLength());

        var ids = profilesElement.EnumerateArray().Select(p => p.GetProperty("id").GetString()).ToArray();
        Assert.Contains("eu", ids);
        Assert.Contains("us", ids);

        JsonElement usProfile = profilesElement.EnumerateArray().First(p => p.GetProperty("id").GetString() == "us");
        Assert.Equal("us.signals.json", usProfile.GetProperty("signalsPath").GetString());
        Assert.False(string.IsNullOrWhiteSpace(usProfile.GetProperty("lastSignalDate").GetString()));
        Assert.True(usProfile.GetProperty("isRiskOn").GetBoolean());
    }

    [Fact]
    public void DefaultRun_UsesProfilesDirectory_WhenExists()
    {
        using var workspace = new TemporaryWorkspace();
        string profilesDir = workspace.GetPath("config", "gem", "profiles");
        string cacheDir = workspace.GetPath("cache");
        string dataDir = workspace.GetPath("data");

        WriteCommonPrices(dataDir);

        string outputUs = workspace.GetPath("dist", "gem", "us.signals.json");
        string outputEu = workspace.GetPath("dist", "gem", "eu.signals.json");

        WriteConfig(workspace, Path.Combine("config", "gem", "profiles", "us.profile.json"), dataDir, cacheDir, outputUs, "VOO.US", "VEU.US", "AGG.US");
        WriteConfig(workspace, Path.Combine("config", "gem", "profiles", "eu.profile.json"), dataDir, cacheDir, outputEu, "SPPW.DE", "IS3N.DE", "EUNA.DE");

        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), new[] { "--no-update" });

        Assert.Equal(Program.ExitCodeSuccess, exitCode);
        Assert.True(File.Exists(outputUs));
        Assert.True(File.Exists(outputEu));

        string manifestPath = workspace.GetPath("dist", "gem", "profiles.json");
        Assert.True(File.Exists(manifestPath));

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement profilesElement = manifest.RootElement.GetProperty("profiles");
        Assert.Equal(2, profilesElement.GetArrayLength());
    }

    [Fact]
    public void SingleRun_StillWorks_WhenExplicitConfigProvided()
    {
        using var workspace = new TemporaryWorkspace();
        string dataDir = workspace.GetPath("data");
        string cacheDir = workspace.GetPath("cache");
        string outputPath = workspace.GetPath("dist", "gem", "single", "signals.json");

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
          "update": { "autoUpdateEnabled": false, "maxAgeDays": 2, "minMinutesBetweenAttempts": 30, "saveUpdatedDataToStore": true }
        }
        """;

        workspace.WriteText(json, "config", "gem", "gem.config.json");

        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), new[] { "--config", "config/gem/gem.config.json", "--no-update" });

        Assert.Equal(Program.ExitCodeSuccess, exitCode);
        Assert.True(File.Exists(outputPath));

        string manifestPath = workspace.GetPath("dist", "gem", "profiles.json");
        Assert.False(File.Exists(manifestPath));
    }

    [Fact]
    public void ProfileFlag_RunsSingleProfile()
    {
        using var workspace = new TemporaryWorkspace();
        string profilesDir = workspace.GetPath("config", "gem", "profiles");
        string cacheDir = workspace.GetPath("cache");
        string dataDir = workspace.GetPath("data");

        WriteCommonPrices(dataDir);

        string outputUs = workspace.GetPath("dist", "gem", "us.signals.json");
        string outputEu = workspace.GetPath("dist", "gem", "eu.signals.json");

        WriteConfig(workspace, Path.Combine("config", "gem", "profiles", "us.profile.json"), dataDir, cacheDir, outputUs, "VOO.US", "VEU.US", "AGG.US");
        WriteConfig(workspace, Path.Combine("config", "gem", "profiles", "eu.profile.json"), dataDir, cacheDir, outputEu, "SPPW.DE", "IS3N.DE", "EUNA.DE");

        int exitCode = Program.Run(workspace.Root, new StringWriter(), new StringWriter(), new[] { "--profile", "us", "--no-update" });

        Assert.Equal(Program.ExitCodeSuccess, exitCode);
        Assert.True(File.Exists(outputUs));
        Assert.False(File.Exists(outputEu));

        string manifestPath = workspace.GetPath("dist", "gem", "profiles.json");
        Assert.False(File.Exists(manifestPath));
    }

    private static void WriteConfig(
        TemporaryWorkspace workspace,
        string relativePath,
        string storeDirectory,
        string cacheDirectory,
        string outputPath,
        string usTicker,
        string exUsTicker,
        string safeTicker)
    {
        string json = $$"""
        {
          "windowMonths": 2,
          "rankingMode": "Top1",
          "instruments": {
            "usEquity": { "ticker": "{{usTicker}}", "name": "{{usTicker}}", "sourceSymbol": "{{usTicker.ToLowerInvariant()}}" },
            "exUsEquity": { "ticker": "{{exUsTicker}}", "name": "{{exUsTicker}}", "sourceSymbol": "{{exUsTicker.ToLowerInvariant()}}" },
            "safeAsset": { "ticker": "{{safeTicker}}", "name": "{{safeTicker}}", "sourceSymbol": "{{safeTicker.ToLowerInvariant()}}" }
          },
          "storeDirectory": "{{storeDirectory.Replace("\\", "\\\\")}}",
          "cacheDirectory": "{{cacheDirectory.Replace("\\", "\\\\")}}",
          "outputPath": "{{outputPath.Replace("\\", "\\\\")}}",
          "update": { "autoUpdateEnabled": false, "maxAgeDays": 2, "minMinutesBetweenAttempts": 30, "saveUpdatedDataToStore": true }
        }
        """;

        workspace.WriteText(json, relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static void WriteCommonPrices(string directory)
    {
        WritePriceFile(directory, "voo.us.csv");
        WritePriceFile(directory, "veu.us.csv");
        WritePriceFile(directory, "agg.us.csv");
        WritePriceFile(directory, "sppw.de.csv");
        WritePriceFile(directory, "is3n.de.csv");
        WritePriceFile(directory, "euna.de.csv");
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

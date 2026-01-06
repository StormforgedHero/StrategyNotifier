using Gem.Cli.Configuration;
using Gem.Domain.Model;
using Gem.Cli.Tests.TestSupport;

namespace Gem.Cli.Tests;

public sealed class GemConfigLoaderTests
{
    [Fact]
    public void Load_WithValidConfig_UsesDefaultsAndValues()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 6,
          "rankingMode": "Top2",
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "sourceSymbol": "agg.us" }
          }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        GemCliConfiguration configuration = loader.Load();

        Assert.Equal("data/gem/sample", configuration.DataDirectory);
        Assert.Equal("dist/gem/signals.json", configuration.OutputSignalsFile);
        Assert.True(configuration.Update.Enabled);
        Assert.Equal(2, configuration.Update.FreshnessDays);
        Assert.Equal(1, configuration.Update.MinDelaySeconds);

        Assert.Equal(2, configuration.Portfolio.RiskOnInstruments.Count);
        Assert.Equal("AGG.US", configuration.Portfolio.RiskOffInstrument.Ticker);
        Assert.Equal(6, configuration.Portfolio.Momentum.WindowMonths);
        Assert.Equal(RankingMode.Top2, configuration.Portfolio.Momentum.RankingMode);
        Assert.True(configuration.Portfolio.Momentum.UseAbsoluteMomentum);
        Assert.Equal(0m, configuration.Portfolio.Momentum.AbsoluteThreshold);
    }

    [Fact]
    public void Load_WithInvalidJson_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, "{ this is not valid }");

        var loader = new GemConfigLoader(configPath);

        Assert.Throws<InvalidOperationException>(() => loader.Load());
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config", "gem", "gem.config.json");
        var loader = new GemConfigLoader(missingPath);

        Assert.Throws<FileNotFoundException>(() => loader.Load());
    }

    [Fact]
    public void Load_WithRiskOnCollection_UsesAllEntries()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 4,
          "rankingMode": "Top2",
          "instruments": {
            "riskOn": [
              { "ticker": "AAA.DE", "sourceSymbol": "aaa.de" },
              { "ticker": "BBB.DE", "sourceSymbol": "bbb.de" },
              { "ticker": "CCC.DE", "sourceSymbol": "ccc.de" }
            ],
            "safeAsset": { "ticker": "SAFE.DE", "sourceSymbol": "safe.de" }
          }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        GemCliConfiguration configuration = loader.Load();

        Assert.Equal(3, configuration.Portfolio.RiskOnInstruments.Count);
        Assert.All(configuration.Portfolio.RiskOnInstruments, instrument => Assert.False(string.IsNullOrWhiteSpace(instrument.SourceSymbol)));
        Assert.Equal("SAFE.DE", configuration.Portfolio.RiskOffInstrument.Ticker);
        Assert.Equal(RankingMode.Top2, configuration.Portfolio.Momentum.RankingMode);
    }

    [Fact]
    public void Load_Top2WithSingleRiskOn_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "rankingMode": "Top2",
          "instruments": {
            "riskOn": [ { "ticker": "AAA.US" } ],
            "safeAsset": { "ticker": "SAFE.US" }
          }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("RankingMode=Top2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_DuplicateTicker_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "instruments": {
            "riskOn": [ { "ticker": "AAA.US" }, { "ticker": "AAA.US" } ],
            "safeAsset": { "ticker": "SAFE.US" }
          }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("Duplicate instrument identifier", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_RiskOnNullEntry_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "instruments": {
            "riskOn": [ null, { "ticker": "AAA.US" } ],
            "safeAsset": { "ticker": "SAFE.US" }
          }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("riskOn cannot contain null", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateConfigFile(TemporaryWorkspace workspace, string json)
    {
        string path = workspace.GetPath("config", "gem", "gem.config.json");
        workspace.WriteText(json, "config", "gem", "gem.config.json");
        return path;
    }
}

using Gem.Cli.Configuration;
using Gem.Cli.Tests.TestSupport;
using Gem.Domain.Model;

namespace Gem.Cli.Tests;

public sealed class GemConfigLoaderTests
{
    [Fact]
    public void Load_WithValidConfig_UsesValues()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 6,
          "rankingMode": "Top2",
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json",
          "update": {
            "autoUpdateEnabled": true,
            "maxAgeDays": 5,
            "minMinutesBetweenAttempts": 45,
            "saveUpdatedDataToStore": false
          }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        GemCliConfiguration configuration = loader.Load();

        Assert.Equal("data/gem/sample", configuration.StoreDirectory.Replace("\\", "/"));
        Assert.Equal("data/gem/cache", configuration.CacheDirectory);
        Assert.Equal("dist/gem/signals.json", configuration.OutputSignalsFile);
        Assert.True(configuration.Update.AutoUpdateEnabled);
        Assert.False(configuration.Update.SaveUpdatedDataToStore);
        Assert.Equal(5, configuration.Update.MaxAgeDays);
        Assert.Equal(45, configuration.Update.MinMinutesBetweenAttempts);

        Assert.Equal(2, configuration.Portfolio.RiskOnInstruments.Count);
        Assert.Equal("AGG.US", configuration.Portfolio.RiskOffInstrument.Ticker);
        Assert.All(configuration.Portfolio.RiskOnInstruments.Concat(new[] { configuration.Portfolio.RiskOffInstrument }), i => Assert.False(string.IsNullOrWhiteSpace(i.SourceSymbol)));
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
    public void Load_NegativeCooldown_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 6,
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json",
          "update": {
            "minMinutesBetweenAttempts": -1
          }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("cannot be negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_NegativeFreshness_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 6,
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json",
          "update": {
            "maxAgeDays": -2
          }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("cannot be negative", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_Top2WithMissingRiskOn_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "rankingMode": "Top2",
          "instruments": {
            "usEquity": { "ticker": "AAA.US", "name": "AAA", "sourceSymbol": "aaa.us" },
            "safeAsset": { "ticker": "SAFE.US", "name": "SAFE", "sourceSymbol": "safe.us" }
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json"
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("Risk-on instruments must include both", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_DuplicateTicker_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "instruments": {
            "usEquity": { "ticker": "AAA.US", "name": "AAA", "sourceSymbol": "aaa.us" },
            "exUsEquity": { "ticker": "AAA.US", "name": "AAA", "sourceSymbol": "aaa2.us" },
            "safeAsset": { "ticker": "SAFE.US", "name": "SAFE", "sourceSymbol": "safe.us" }
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json"
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("Duplicate instrument identifier", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MissingSourceSymbol_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json"
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("sourceSymbol", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_UsesDefaults_WhenUpdateMissing()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json",
          "update": { }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        GemCliConfiguration configuration = loader.Load();

        Assert.Equal(30, configuration.Update.MinMinutesBetweenAttempts);
        Assert.Equal(2, configuration.Update.MaxAgeDays);
        Assert.True(configuration.Update.SaveUpdatedDataToStore);
    }

    [Fact]
    public void Load_FractionalCooldown_ThrowsWithClearMessage()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "update": {
            "minMinutesBetweenAttempts": 0.01
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json"
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("minMinutesBetweenAttempts must be specified in whole minutes (integer).", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_UnknownField_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "rankingMode": "Top1",
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json",
          "update": { "autoUpdateEnabled": false },
          "unexpected": true
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("unsupported field", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_UnknownNestedField_WithDifferentCasing_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "rankingMode": "Top1",
          "Instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us", "unexpectedNested": true },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json",
          "update": { "autoUpdateEnabled": false }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("unsupported field 'instruments.usEquity.unexpectedNested'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_UnknownUpdateField_WithDifferentCasing_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "rankingMode": "Top1",
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json",
          "Update": { "autoUpdateEnabled": false, "unexpectedUpdateField": true }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("unsupported field 'update.unexpectedUpdateField'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MissingStoreDirectory_Throws()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "rankingMode": "Top1",
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json",
          "update": { "autoUpdateEnabled": false }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("storeDirectory is required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_FractionalCooldown_WithDifferentCasing_ThrowsWithClearMessage()
    {
        using var workspace = new TemporaryWorkspace();
        string configPath = CreateConfigFile(workspace, """
        {
          "windowMonths": 3,
          "rankingMode": "Top1",
          "instruments": {
            "usEquity": { "ticker": "VOO.US", "name": "VOO", "sourceSymbol": "voo.us" },
            "exUsEquity": { "ticker": "VEU.US", "name": "VEU", "sourceSymbol": "veu.us" },
            "safeAsset": { "ticker": "AGG.US", "name": "AGG", "sourceSymbol": "agg.us" }
          },
          "storeDirectory": "data/gem/sample",
          "cacheDirectory": "data/gem/cache",
          "outputPath": "dist/gem/signals.json",
          "update": {
            "MinMinutesBetweenAttempts": 0.01
          }
        }
        """);

        var loader = new GemConfigLoader(configPath);
        var ex = Assert.Throws<InvalidOperationException>(() => loader.Load());
        Assert.Contains("minMinutesBetweenAttempts must be specified in whole minutes (integer).", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateConfigFile(TemporaryWorkspace workspace, string json)
    {
        string path = workspace.GetPath("config", "gem", "gem.config.json");
        workspace.WriteText(json, "config", "gem", "gem.config.json");
        return path;
    }
}

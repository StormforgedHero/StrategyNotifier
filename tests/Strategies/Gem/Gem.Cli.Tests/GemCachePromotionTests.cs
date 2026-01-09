using Gem.Cli.Configuration;
using Gem.Cli.Execution;
using Gem.Cli.Pricing;
using Gem.Cli.Tests.TestSupport;
using Gem.Domain.Model;

namespace Gem.Cli.Tests;

public sealed class GemCachePromotionTests
{
    [Fact]
    public void Run_NoUpdate_DoesNotPromoteCacheEvenWhenNewer()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteSeries(workspace, storeDirectory, "one.us.csv", OlderSeries());
        WriteSeries(workspace, storeDirectory, "safe.us.csv", OlderSeries());
        WriteSeries(workspace, cacheDirectory, "one.us.csv", NewerSeries());
        WriteSeries(workspace, cacheDirectory, "safe.us.csv", NewerSeries());

        byte[] storeBefore = File.ReadAllBytes(Path.Combine(storeDirectory, "one.us.csv"));

        GemCliConfiguration configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath, saveUpdatedDataToStore: true);
        var runner = new GemRunner(configuration, new NoOpPriceDataUpdater(), new LocalCsvPriceDataProvider());

        runner.Run(skipUpdate: true, forceUpdate: false);

        byte[] storeAfter = File.ReadAllBytes(Path.Combine(storeDirectory, "one.us.csv"));
        Assert.Equal(storeBefore, storeAfter);
    }

    [Fact]
    public void Run_SaveDisabled_DoesNotPromote()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteSeries(workspace, storeDirectory, "one.us.csv", OlderSeries());
        WriteSeries(workspace, storeDirectory, "safe.us.csv", OlderSeries());
        WriteSeries(workspace, cacheDirectory, "one.us.csv", NewerSeries());
        WriteSeries(workspace, cacheDirectory, "safe.us.csv", NewerSeries());

        byte[] storeBefore = File.ReadAllBytes(Path.Combine(storeDirectory, "one.us.csv"));

        GemCliConfiguration configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath, saveUpdatedDataToStore: false);
        var runner = new GemRunner(configuration, new NoOpPriceDataUpdater(), new LocalCsvPriceDataProvider());

        runner.Run(skipUpdate: false, forceUpdate: true);

        byte[] storeAfter = File.ReadAllBytes(Path.Combine(storeDirectory, "one.us.csv"));
        Assert.Equal(storeBefore, storeAfter);
    }

    private static GemCliConfiguration BuildConfiguration(
        string storeDirectory,
        string cacheDirectory,
        string outputPath,
        bool saveUpdatedDataToStore)
    {
        var riskOn = new Instrument("ONE.US", "One", "one.us");
        var safe = new Instrument("SAFE.US", "Safe", "safe.us");

        var momentum = new MomentumParameters(1, RankingMode.Top1, useAbsoluteMomentum: true, absoluteThreshold: 0m);
        var portfolio = new PortfolioConfiguration(new[] { riskOn }, safe, momentum);
        var update = new UpdateSettings
        {
            MaxAgeDays = 2,
            MinMinutesBetweenAttempts = 0,
            SaveUpdatedDataToStore = saveUpdatedDataToStore
        };

        return new GemCliConfiguration(portfolio, update, storeDirectory, cacheDirectory, outputPath);
    }

    private static void WriteSeries(
        TemporaryWorkspace workspace,
        string baseDirectory,
        string fileName,
        IReadOnlyList<PricePoint> series)
    {
        string path = Path.Combine(baseDirectory, fileName);
        string csv = "Date,Close\n" + string.Join(
            "\n",
            series.Select(point => $"{point.Date:yyyy-MM-dd},{point.Close}"));

        workspace.WriteText(csv, Path.GetRelativePath(workspace.Root, path));
    }

    private static IReadOnlyList<PricePoint> OlderSeries()
    {
        return new[]
        {
            new PricePoint(new DateOnly(2024, 1, 31), 100m),
            new PricePoint(new DateOnly(2024, 2, 29), 102m),
            new PricePoint(new DateOnly(2024, 3, 29), 104m)
        };
    }

    private static IReadOnlyList<PricePoint> NewerSeries()
    {
        return new[]
        {
            new PricePoint(new DateOnly(2024, 1, 31), 100m),
            new PricePoint(new DateOnly(2024, 2, 29), 103m),
            new PricePoint(new DateOnly(2024, 3, 29), 107m),
            new PricePoint(new DateOnly(2024, 4, 30), 110m)
        };
    }
}

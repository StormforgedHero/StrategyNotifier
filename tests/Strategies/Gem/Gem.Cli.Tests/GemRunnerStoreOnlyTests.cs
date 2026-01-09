using Gem.Cli.Configuration;
using Gem.Cli.Execution;
using Gem.Cli.Pricing;
using Gem.Cli.Tests.TestSupport;
using Gem.Domain.Model;

namespace Gem.Cli.Tests;

public sealed class GemRunnerStoreOnlyTests
{
    [Fact]
    public void Run_SignalsDependOnStoreOnly_EvenWhenCacheChanges()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteSeries(workspace, storeDirectory, "one.us.csv", StoreSeries());
        WriteSeries(workspace, storeDirectory, "safe.us.csv", StoreSeries());

        WriteSeries(workspace, cacheDirectory, "one.us.csv", CacheSeries());
        WriteSeries(workspace, cacheDirectory, "safe.us.csv", CacheSeries());

        GemCliConfiguration configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath);

        IReadOnlyList<Signal> firstRun = RunOnce(configuration);

        Assert.NotEmpty(firstRun);
        Assert.Equal(new DateOnly(2024, 3, 29), firstRun[0].Date);

        WriteSeries(workspace, cacheDirectory, "one.us.csv", DivergentCacheSeries());
        WriteSeries(workspace, cacheDirectory, "safe.us.csv", DivergentCacheSeries());

        IReadOnlyList<Signal> secondRun = RunOnce(configuration);

        Assert.Equal(firstRun.Select(signal => signal.Date), secondRun.Select(signal => signal.Date));
        Assert.Equal(firstRun[0].Allocations[0].Instrument.Ticker, secondRun[0].Allocations[0].Instrument.Ticker);
    }

    private static IReadOnlyList<Signal> RunOnce(GemCliConfiguration configuration)
    {
        var runner = new GemRunner(configuration, new NoOpPriceDataUpdater(), new LocalCsvPriceDataProvider());
        return runner.Run(skipUpdate: true, forceUpdate: false);
    }

    private static GemCliConfiguration BuildConfiguration(
        string storeDirectory,
        string cacheDirectory,
        string outputPath)
    {
        var riskOn = new Instrument("ONE.US", "One", "one.us");
        var safe = new Instrument("SAFE.US", "Safe", "safe.us");

        var momentum = new MomentumParameters(1, RankingMode.Top1, useAbsoluteMomentum: true, absoluteThreshold: 0m);
        var portfolio = new PortfolioConfiguration(new[] { riskOn }, safe, momentum);
        var update = new UpdateSettings
        {
            AutoUpdateEnabled = false,
            MaxAgeDays = 2,
            MinMinutesBetweenAttempts = 0,
            SaveUpdatedDataToStore = true
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

    private static IReadOnlyList<PricePoint> StoreSeries()
    {
        return new[]
        {
            new PricePoint(new DateOnly(2024, 1, 31), 100m),
            new PricePoint(new DateOnly(2024, 2, 29), 102m),
            new PricePoint(new DateOnly(2024, 3, 29), 104m)
        };
    }

    private static IReadOnlyList<PricePoint> CacheSeries()
    {
        return new[]
        {
            new PricePoint(new DateOnly(2024, 1, 31), 100m),
            new PricePoint(new DateOnly(2024, 2, 29), 103m),
            new PricePoint(new DateOnly(2024, 3, 29), 106m),
            new PricePoint(new DateOnly(2024, 4, 30), 110m)
        };
    }

    private static IReadOnlyList<PricePoint> DivergentCacheSeries()
    {
        return new[]
        {
            new PricePoint(new DateOnly(2024, 1, 31), 90m),
            new PricePoint(new DateOnly(2024, 2, 29), 89m),
            new PricePoint(new DateOnly(2024, 3, 29), 88m),
            new PricePoint(new DateOnly(2024, 4, 30), 87m),
            new PricePoint(new DateOnly(2024, 5, 31), 86m)
        };
    }
}

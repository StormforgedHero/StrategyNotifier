using Gem.Cli.Configuration;
using Gem.Cli.Execution;
using Gem.Cli.Pricing;
using Gem.Cli.Tests.TestSupport;
using Gem.Domain.Model;

namespace Gem.Cli.Tests;

public sealed class GemRunnerCacheTests
{
    [Fact]
    public void ForceUpdate_WritesToCache_LeavesStoreUntouched_WhenSaveDisabled()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteSeries(workspace, storeDirectory, "one.us.csv", SampleSeries());
        WriteSeries(workspace, storeDirectory, "safe.us.csv", SampleSeries());

        byte[] storeOneBefore = File.ReadAllBytes(Path.Combine(storeDirectory, "one.us.csv"));
        byte[] storeSafeBefore = File.ReadAllBytes(Path.Combine(storeDirectory, "safe.us.csv"));

        GemCliConfiguration configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath, saveUpdatedDataToStore: false);

        var updater = new RecordingPriceDataUpdater(cacheDirectory, _ => UpdatedSeries(), saveUpdatedDataToStore: false, storeDirectory: storeDirectory);
        var runner = new GemRunner(configuration, updater, new LocalCsvPriceDataProvider());

        IReadOnlyList<Signal> signals = runner.Run(skipUpdate: false, forceUpdate: true);

        Assert.True(updater.WasCalled);
        Assert.True(updater.LastForceUpdate);
        Assert.Equal(1, updater.Calls);
        Assert.Equal(2, updater.WrittenFiles.Count);
        Assert.True(File.Exists(Path.Combine(cacheDirectory, "one.us.csv")));
        Assert.True(File.Exists(Path.Combine(cacheDirectory, "safe.us.csv")));
        Assert.NotEmpty(signals);

        byte[] sampleOneAfter = File.ReadAllBytes(Path.Combine(storeDirectory, "one.us.csv"));
        byte[] sampleSafeAfter = File.ReadAllBytes(Path.Combine(storeDirectory, "safe.us.csv"));

        Assert.Equal(storeOneBefore, sampleOneAfter);
        Assert.Equal(storeSafeBefore, sampleSafeAfter);
    }

    [Fact]
    public void Run_UsesStoreEvenIfCacheNewer()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteSeries(workspace, storeDirectory, "one.us.csv", SampleSeries());
        WriteSeries(workspace, storeDirectory, "safe.us.csv", SampleSeries());

        WriteSeries(workspace, cacheDirectory, "one.us.csv", CachedSeries());
        WriteSeries(workspace, cacheDirectory, "safe.us.csv", CachedSeries());

        GemCliConfiguration configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath, saveUpdatedDataToStore: false);
        var runner = new GemRunner(configuration, new NoOpPriceDataUpdater(), new LocalCsvPriceDataProvider());

        IReadOnlyList<Signal> signals = runner.Run(skipUpdate: true, forceUpdate: false);

        Assert.NotEmpty(signals);
        Assert.Equal(new DateOnly(2024, 3, 29), signals[0].Date);
    }

    [Fact]
    public void Run_IgnoresCacheWhenOlder()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteSeries(workspace, storeDirectory, "one.us.csv", CachedSeries());
        WriteSeries(workspace, storeDirectory, "safe.us.csv", CachedSeries());

        WriteSeries(workspace, cacheDirectory, "one.us.csv", SampleSeries());
        WriteSeries(workspace, cacheDirectory, "safe.us.csv", SampleSeries());

        GemCliConfiguration configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath, saveUpdatedDataToStore: false);
        var runner = new GemRunner(configuration, new NoOpPriceDataUpdater(), new LocalCsvPriceDataProvider());

        IReadOnlyList<Signal> signals = runner.Run(skipUpdate: true, forceUpdate: false);

        Assert.NotEmpty(signals);
        Assert.Equal(new DateOnly(2024, 4, 30), signals[0].Date);
    }

    [Fact]
    public void ForceUpdate_SavesToStore_WhenEnabled()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteSeries(workspace, storeDirectory, "one.us.csv", SampleSeries());
        WriteSeries(workspace, storeDirectory, "safe.us.csv", SampleSeries());

        GemCliConfiguration configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath, saveUpdatedDataToStore: true);
        var updater = new RecordingPriceDataUpdater(cacheDirectory, _ => UpdatedSeries(), saveUpdatedDataToStore: true, storeDirectory: storeDirectory);
        var runner = new GemRunner(configuration, updater, new LocalCsvPriceDataProvider());

        runner.Run(skipUpdate: false, forceUpdate: true);

        string storePath = Path.Combine(storeDirectory, "one.us.csv");
        string storeContent = File.ReadAllText(storePath, Utf8TestEncoding.Utf8NoBom);
        Assert.Contains("2024-03-29", storeContent);
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

    private static IReadOnlyList<PricePoint> SampleSeries()
    {
        return new[]
        {
            new PricePoint(new DateOnly(2024, 1, 31), 100m),
            new PricePoint(new DateOnly(2024, 2, 29), 102m),
            new PricePoint(new DateOnly(2024, 3, 29), 104m)
        };
    }

    private static IReadOnlyList<PricePoint> CachedSeries()
    {
        return new[]
        {
            new PricePoint(new DateOnly(2024, 1, 31), 100m),
            new PricePoint(new DateOnly(2024, 2, 29), 103m),
            new PricePoint(new DateOnly(2024, 3, 29), 106m),
            new PricePoint(new DateOnly(2024, 4, 30), 110m)
        };
    }

    private static IReadOnlyList<PricePoint> UpdatedSeries()
    {
        return new[]
        {
            new PricePoint(new DateOnly(2024, 1, 31), 101m),
            new PricePoint(new DateOnly(2024, 2, 29), 105m),
            new PricePoint(new DateOnly(2024, 3, 29), 107m)
        };
    }
}

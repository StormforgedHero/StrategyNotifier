using Gem.Cli.Configuration;
using Gem.Cli.Execution;
using Gem.Cli.Pricing;
using Gem.Cli.Tests.TestSupport;
using Gem.Domain.Model;

namespace Gem.Cli.Tests;

public sealed class GemPriceDataUpdaterPersistenceTests
{
    [Fact]
    public void ForceUpdate_PersistTrue_WritesStoreAndCache()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteCsv(workspace, storeDirectory, "one.us.csv", OldSeries());
        WriteCsv(workspace, storeDirectory, "safe.us.csv", OldSeries());

        byte[] dataBefore = File.ReadAllBytes(Path.Combine(storeDirectory, "one.us.csv"));

        var configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath, saveUpdatedDataToStore: true);
        var provider = CreateProvider(NewSeries());
        var updater = new PriceDataUpdater(
            provider,
            cacheDirectory,
            storeDirectory,
            saveUpdatedDataToStore: true,
            maxAgeWindow: TimeSpan.FromDays(configuration.Update.MaxAgeDays),
            requestDelay: TimeSpan.Zero,
            attemptCooldown: TimeSpan.Zero);

        var runner = new GemRunner(configuration, updater, new LocalCsvPriceDataProvider());
        runner.Run(skipUpdate: false, forceUpdate: true);

        string storePath = Path.Combine(storeDirectory, "one.us.csv");
        string cachePath = Path.Combine(cacheDirectory, "one.us.csv");

        Assert.True(File.Exists(storePath));
        Assert.True(File.Exists(cachePath));
        Assert.NotEqual(dataBefore, File.ReadAllBytes(storePath));
        Assert.Contains("2024-04-30", File.ReadAllText(storePath, Utf8TestEncoding.Utf8NoBom));
        Assert.Contains("2024-04-30", File.ReadAllText(cachePath, Utf8TestEncoding.Utf8NoBom));
    }

    [Fact]
    public void ForceUpdate_PersistFalse_DoesNotTouchStore()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteCsv(workspace, storeDirectory, "one.us.csv", OldSeries());
        WriteCsv(workspace, storeDirectory, "safe.us.csv", OldSeries());

        byte[] dataBefore = File.ReadAllBytes(Path.Combine(storeDirectory, "one.us.csv"));

        var configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath, saveUpdatedDataToStore: false);
        var provider = CreateProvider(NewSeries());
        var updater = new PriceDataUpdater(
            provider,
            cacheDirectory,
            storeDirectory,
            saveUpdatedDataToStore: false,
            maxAgeWindow: TimeSpan.FromDays(configuration.Update.MaxAgeDays),
            requestDelay: TimeSpan.Zero,
            attemptCooldown: TimeSpan.Zero);

        var runner = new GemRunner(configuration, updater, new LocalCsvPriceDataProvider());
        runner.Run(skipUpdate: false, forceUpdate: true);

        string storePath = Path.Combine(storeDirectory, "one.us.csv");
        string cachePath = Path.Combine(cacheDirectory, "one.us.csv");

        Assert.True(File.Exists(cachePath));
        Assert.Equal(dataBefore, File.ReadAllBytes(storePath));
        Assert.Contains("2024-04-30", File.ReadAllText(cachePath, Utf8TestEncoding.Utf8NoBom));
    }

    [Fact]
    public async Task UpdateAsync_RespectsCooldownBetweenRuns()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");

        Directory.CreateDirectory(storeDirectory);
        Directory.CreateDirectory(cacheDirectory);

        var configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath: workspace.GetPath("dist", "gem", "signals.json"), saveUpdatedDataToStore: false);
        var handler = new FakeStooqHttpMessageHandler(new Dictionary<string, string>
        {
            ["one.us"] = OldSeries()
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://stooq.pl") };
        var provider = new StooqCsvHttpDataProvider(client);
        var updater = new PriceDataUpdater(
            provider,
            cacheDirectory,
            storeDirectory,
            saveUpdatedDataToStore: false,
            maxAgeWindow: TimeSpan.FromDays(1),
            requestDelay: TimeSpan.Zero,
            attemptCooldown: TimeSpan.FromMinutes(60));

        var instruments = new[] { new Instrument("ONE.US", "One", "one.us") };

        await updater.UpdateAsync(instruments, forceUpdate: false, CancellationToken.None);
        int firstRequests = handler.RequestCounts.GetValueOrDefault("one.us");

        await updater.UpdateAsync(instruments, forceUpdate: false, CancellationToken.None);
        int secondRequests = handler.RequestCounts.GetValueOrDefault("one.us");

        Assert.Equal(1, firstRequests);
        Assert.Equal(1, secondRequests);
    }

    [Fact]
    public async Task UpdateAsync_UsesNewestLocalDataToAvoidHttp()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");

        WriteCsv(workspace, storeDirectory, "one.us.csv", OldSeries());
        WriteCsv(workspace, cacheDirectory, "one.us.csv", RecentSeries());

        var handler = new FakeStooqHttpMessageHandler(new Dictionary<string, string>());
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://stooq.pl") };
        var provider = new StooqCsvHttpDataProvider(client);
        var updater = new PriceDataUpdater(
            provider,
            cacheDirectory,
            storeDirectory,
            saveUpdatedDataToStore: false,
            maxAgeWindow: TimeSpan.FromDays(30),
            requestDelay: TimeSpan.Zero,
            attemptCooldown: TimeSpan.Zero);

        var instruments = new[] { new Instrument("ONE.US", "One", "one.us") };

        await updater.UpdateAsync(instruments, forceUpdate: false, CancellationToken.None);

        Assert.Equal(0, handler.RequestCounts.GetValueOrDefault("one.us"));
    }

    [Fact]
    public async Task UpdateAsync_SaveToStore_IgnoresCacheForStaleness()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");

        WriteCsv(workspace, storeDirectory, "one.us.csv", OldSeries());
        WriteCsv(workspace, cacheDirectory, "one.us.csv", RecentSeries());

        var handler = new FakeStooqHttpMessageHandler(new Dictionary<string, string>
        {
            ["one.us"] = RecentSeries()
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://stooq.pl") };
        var provider = new StooqCsvHttpDataProvider(client);
        var updater = new PriceDataUpdater(
            provider,
            cacheDirectory,
            storeDirectory,
            saveUpdatedDataToStore: true,
            maxAgeWindow: TimeSpan.FromDays(30),
            requestDelay: TimeSpan.Zero,
            attemptCooldown: TimeSpan.Zero);

        var instruments = new[] { new Instrument("ONE.US", "One", "one.us") };

        await updater.UpdateAsync(instruments, forceUpdate: false, CancellationToken.None);

        Assert.Equal(1, handler.RequestCounts.GetValueOrDefault("one.us"));
    }

    [Fact]
    public async Task UpdateAsync_WhenAllDataStale_PerformsRequest()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");

        WriteCsv(workspace, storeDirectory, "one.us.csv", OldSeries());
        WriteCsv(workspace, cacheDirectory, "one.us.csv", OldSeries());

        var handler = new FakeStooqHttpMessageHandler(new Dictionary<string, string>
        {
            ["one.us"] = RecentSeries()
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://stooq.pl") };
        var provider = new StooqCsvHttpDataProvider(client);
        var updater = new PriceDataUpdater(
            provider,
            cacheDirectory,
            storeDirectory,
            saveUpdatedDataToStore: false,
            maxAgeWindow: TimeSpan.FromDays(30),
            requestDelay: TimeSpan.Zero,
            attemptCooldown: TimeSpan.Zero);

        var instruments = new[] { new Instrument("ONE.US", "One", "one.us") };

        await updater.UpdateAsync(instruments, forceUpdate: false, CancellationToken.None);

        Assert.Equal(1, handler.RequestCounts.GetValueOrDefault("one.us"));
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

    private static StooqCsvHttpDataProvider CreateProvider(string csv)
    {
        var responses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["one.us"] = csv,
            ["safe.us"] = csv
        };

        var handler = new FakeStooqHttpMessageHandler(responses);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://stooq.pl")
        };

        return new StooqCsvHttpDataProvider(client);
    }

    private static string OldSeries()
    {
        return
            """
            Date,Close
            2024-01-31,100
            2024-02-29,101
            2024-03-29,102
            """;
    }

    private static string NewSeries()
    {
        return
            """
            Date,Close
            2024-01-31,110
            2024-02-29,120
            2024-03-29,130
            2024-04-30,140
            """;
    }

    private static string RecentSeries()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        DateOnly prev = today.AddDays(-30);
        return
            $"""
            Date,Close
            {prev:yyyy-MM-dd},100
            {today:yyyy-MM-dd},101
            """;
    }

    private static void WriteCsv(TemporaryWorkspace workspace, string directory, string fileName, string csv)
    {
        workspace.WriteText(csv, Path.GetRelativePath(workspace.Root, Path.Combine(directory, fileName)));
    }
}

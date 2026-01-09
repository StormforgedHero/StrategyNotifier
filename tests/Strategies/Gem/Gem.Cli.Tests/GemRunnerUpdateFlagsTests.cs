using Gem.Cli.Configuration;
using Gem.Cli.Execution;
using Gem.Cli.Pricing;
using Gem.Cli.Tests.TestSupport;
using Gem.Domain.Model;

namespace Gem.Cli.Tests;

public sealed class GemRunnerUpdateFlagsTests
{
    [Fact]
    public void Run_ForceUpdate_AlwaysInvokesUpdater()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteSamplePrices(workspace, storeDirectory, cacheDirectory);

        GemCliConfiguration configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath, autoUpdate: false);
        var updater = new CapturingPriceDataUpdater();
        var runner = new GemRunner(configuration, updater, new LocalCsvPriceDataProvider());

        IReadOnlyList<Signal> signals = runner.Run(skipUpdate: false, forceUpdate: true);

        Assert.True(updater.WasCalled);
        Assert.Equal(1, updater.Calls);
        Assert.True(updater.LastForceUpdate);
        Assert.Equal(3, updater.LastInstruments.Count);
        Assert.NotEmpty(signals);
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void Run_NoUpdate_SkipsUpdaterEvenWhenAutoUpdateEnabled()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteSamplePrices(workspace, storeDirectory, cacheDirectory);

        GemCliConfiguration configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath, autoUpdate: true);
        var updater = new CapturingPriceDataUpdater();
        var runner = new GemRunner(configuration, updater, new LocalCsvPriceDataProvider());

        runner.Run(skipUpdate: true, forceUpdate: false);

        Assert.False(updater.WasCalled);
    }

    [Fact]
    public void Run_Default_AutoUpdateDisabled_SkipsUpdater()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteSamplePrices(workspace, storeDirectory, cacheDirectory);

        GemCliConfiguration configuration = BuildConfiguration(storeDirectory, cacheDirectory, outputPath, autoUpdate: false);
        var updater = new CapturingPriceDataUpdater();
        var runner = new GemRunner(configuration, updater, new LocalCsvPriceDataProvider());

        runner.Run(skipUpdate: false, forceUpdate: false);

        Assert.False(updater.WasCalled);
    }

    [Fact]
    public void Run_DefaultAutoUpdate_UsesStalenessAndCooldown()
    {
        using var workspace = new TemporaryWorkspace();
        string storeDirectory = workspace.GetPath("data", "gem", "store");
        string cacheDirectory = workspace.GetPath("data", "gem", "cache");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        WriteSamplePrices(workspace, storeDirectory, cacheDirectory, lastDate: new DateOnly(2023, 1, 31));

        var responses = new Dictionary<string, string>
        {
            ["one.us"] = FreshSeriesCsv(),
            ["two.us"] = FreshSeriesCsv(),
            ["safe.us"] = FreshSeriesCsv()
        };

        var handler = new FakeStooqHttpMessageHandler(responses);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://stooq.pl") };
        var provider = new StooqCsvHttpDataProvider(client);

        GemCliConfiguration configuration = BuildConfiguration(
            storeDirectory,
            cacheDirectory,
            outputPath,
            autoUpdate: true,
            maxAgeDays: 1,
            minMinutesBetweenAttempts: 120);

        GemRunner CreateRunner() => new GemRunner(
            configuration,
            new PriceDataUpdater(
                provider,
                cacheDirectory,
                storeDirectory,
                saveUpdatedDataToStore: false,
                maxAgeWindow: TimeSpan.FromDays(configuration.Update.MaxAgeDays),
                requestDelay: TimeSpan.Zero,
                attemptCooldown: TimeSpan.FromMinutes(configuration.Update.MinMinutesBetweenAttempts)),
            new LocalCsvPriceDataProvider());

        WriteAttempt(cacheDirectory, "one.us");
        WriteAttempt(cacheDirectory, "two.us");
        WriteAttempt(cacheDirectory, "safe.us");

        CreateRunner().Run(skipUpdate: false, forceUpdate: false);

        Assert.Equal(0, handler.RequestCounts.GetValueOrDefault("one.us"));
        Assert.Equal(0, handler.RequestCounts.GetValueOrDefault("safe.us"));

        WriteAttempt(cacheDirectory, "one.us", DateTimeOffset.UtcNow.AddHours(-3));
        WriteAttempt(cacheDirectory, "two.us", DateTimeOffset.UtcNow.AddHours(-3));
        WriteAttempt(cacheDirectory, "safe.us", DateTimeOffset.UtcNow.AddHours(-3));

        CreateRunner().Run(skipUpdate: false, forceUpdate: false);

        Assert.Equal(1, handler.RequestCounts.GetValueOrDefault("one.us"));
        Assert.Equal(1, handler.RequestCounts.GetValueOrDefault("two.us"));
        Assert.Equal(1, handler.RequestCounts.GetValueOrDefault("safe.us"));
    }

    private static GemCliConfiguration BuildConfiguration(
        string storeDirectory,
        string cacheDirectory,
        string outputPath,
        bool autoUpdate,
        int maxAgeDays = 2,
        int minMinutesBetweenAttempts = 0)
    {
        var riskOnInstruments = new[]
        {
            new Instrument("ONE.US", "One", "one.us"),
            new Instrument("TWO.US", "Two", "two.us")
        };

        var safe = new Instrument("SAFE.US", "Safe", "safe.us");
        var momentum = new MomentumParameters(1, RankingMode.Top2, useAbsoluteMomentum: true, absoluteThreshold: 0m);
        var portfolio = new PortfolioConfiguration(riskOnInstruments, safe, momentum);
        var update = new UpdateSettings
        {
            AutoUpdateEnabled = autoUpdate,
            MaxAgeDays = maxAgeDays,
            MinMinutesBetweenAttempts = minMinutesBetweenAttempts
        };

        return new GemCliConfiguration(portfolio, update, storeDirectory, cacheDirectory, outputPath);
    }

    private static void WriteSamplePrices(TemporaryWorkspace workspace, string storeDirectory, string cacheDirectory, DateOnly? lastDate = null)
    {
        DateOnly finalDate = lastDate ?? new DateOnly(2024, 3, 29);
        DateOnly secondDate = finalDate.AddMonths(-1);
        DateOnly firstDate = finalDate.AddMonths(-2);

        WriteSeries(workspace, storeDirectory, "one.us.csv", firstDate, secondDate, finalDate, 100m);
        WriteSeries(workspace, storeDirectory, "two.us.csv", firstDate, secondDate, finalDate, 102m);
        WriteSeries(workspace, storeDirectory, "safe.us.csv", firstDate, secondDate, finalDate, 100.5m);

        WriteSeries(workspace, cacheDirectory, "one.us.csv", firstDate, secondDate, finalDate, 100m);
        WriteSeries(workspace, cacheDirectory, "two.us.csv", firstDate, secondDate, finalDate, 102m);
        WriteSeries(workspace, cacheDirectory, "safe.us.csv", firstDate, secondDate, finalDate, 100.5m);
    }

    private static void WriteSeries(
        TemporaryWorkspace workspace,
        string directory,
        string fileName,
        DateOnly firstDate,
        DateOnly secondDate,
        DateOnly finalDate,
        decimal finalClose)
    {
        workspace.WriteCsv(
            $$"""
            Date,Close
            {{firstDate:yyyy-MM-dd}},100
            {{secondDate:yyyy-MM-dd}},101
            {{finalDate:yyyy-MM-dd}},{{finalClose}}
            """,
            Path.GetRelativePath(workspace.Root, Path.Combine(directory, fileName)).Split(Path.DirectorySeparatorChar));
    }

    private static string FreshSeriesCsv()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        DateOnly recent = today.AddDays(-1);

        return
            $$"""
            Date,Close
            {{recent:yyyy-MM-dd}},100
            {{today:yyyy-MM-dd}},101
            """;
    }

    private static void WriteAttempt(string cacheDirectory, string symbol, DateTimeOffset? timestamp = null)
    {
        string path = Path.Combine(cacheDirectory, $"{symbol}.last_attempt");
        string content = (timestamp ?? DateTimeOffset.UtcNow).ToString("O");
        Utf8TestEncoding.WriteAllTextUtf8NoBom(path, content);
    }
}

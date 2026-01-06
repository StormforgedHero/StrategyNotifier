using Gem.Cli.Configuration;
using Gem.Cli.Execution;
using Gem.Cli.Pricing;
using Gem.Cli.Tests.TestSupport;
using Gem.Domain.Model;

namespace Gem.Cli.Tests;

public sealed class MissingDataRiskOffOrSkipTests
{
    [Fact]
    public void Run_SkipsMonthWithoutCommonAsOfDate()
    {
        using var workspace = new TemporaryWorkspace();
        string dataDirectory = workspace.GetPath("data");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        workspace.WriteCsv(
            """
            Date,Close
            2024-01-31,100
            2024-02-29,101
            """,
            "data", "one.us.csv");

        workspace.WriteCsv(
            """
            Date,Close
            2024-01-30,100
            2024-03-29,105
            """,
            "data", "two.us.csv");

        workspace.WriteCsv(
            """
            Date,Close
            2024-01-31,1
            2024-02-29,1.01
            """,
            "data", "bnd.us.csv");

        GemCliConfiguration configuration = BuildConfiguration(dataDirectory, outputPath, windowMonths: 1);
        var runner = new GemRunner(configuration, new NoOpPriceDataUpdater(), new LocalCsvPriceDataProvider());

        IReadOnlyList<Signal> signals = runner.Run(skipUpdate: true, forceUpdate: false);

        Assert.Empty(signals);
        Assert.Equal("[]", workspace.ReadAllText("dist", "gem", "signals.json").Trim());
    }

    [Fact]
    public void Run_SkipsMissingHistoryMonthButProducesLaterSignals()
    {
        using var workspace = new TemporaryWorkspace();
        string dataDirectory = workspace.GetPath("data");
        string outputPath = workspace.GetPath("dist", "gem", "signals.json");

        workspace.WriteCsv(
            """
            Date,Close
            2024-01-31,100
            2024-03-31,120
            2024-04-30,130
            """,
            "data", "one.us.csv");

        workspace.WriteCsv(
            """
            Date,Close
            2024-01-31,100
            2024-03-31,115
            2024-04-30,118
            """,
            "data", "two.us.csv");

        workspace.WriteCsv(
            """
            Date,Close
            2024-01-31,1
            2024-03-31,1
            2024-04-30,1
            """,
            "data", "bnd.us.csv");

        GemCliConfiguration configuration = BuildConfiguration(dataDirectory, outputPath, windowMonths: 1);
        var runner = new GemRunner(configuration, new NoOpPriceDataUpdater(), new LocalCsvPriceDataProvider());

        IReadOnlyList<Signal> signals = runner.Run(skipUpdate: true, forceUpdate: false);

        Assert.Equal(2, signals.Count);
        Assert.Equal(new DateOnly(2024, 3, 31), signals[0].Date);
        Assert.Equal(new DateOnly(2024, 4, 30), signals[1].Date);
        Assert.All(signals, signal => Assert.True(signal.IsRiskOn));
        Assert.All(signals, signal => Assert.Equal("ONE.US", signal.Allocations[0].Instrument.Ticker));
    }

    private static GemCliConfiguration BuildConfiguration(string dataDirectory, string outputPath, int windowMonths)
    {
        var riskOne = new Instrument("ONE.US", "One", "one.us");
        var riskTwo = new Instrument("TWO.US", "Two", "two.us");
        var safe = new Instrument("BND.US", "Safe", "bnd.us");

        var momentum = new MomentumParameters(windowMonths, RankingMode.Top1, useAbsoluteMomentum: true, absoluteThreshold: 0m);
        var portfolio = new PortfolioConfiguration(new[] { riskOne, riskTwo }, safe, momentum);
        var update = new UpdateSettings { Enabled = false, FreshnessDays = 2, MinDelaySeconds = 0 };

        return new GemCliConfiguration(portfolio, update, dataDirectory, outputPath);
    }
}

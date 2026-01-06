using Gem.Domain.Engine;
using Gem.Domain.Model;
using Gem.Domain.Tests.TestSupport;

namespace Gem.Domain.Tests.Engine;

public sealed class AbsoluteMomentumThresholdTests
{
    private static readonly Instrument Winner = new("WIN", "Winner");
    private static readonly Instrument RunnerUp = new("ALT", "Alternative");
    private static readonly Instrument Safe = new("SAFE", "Safe");

    [Fact]
    public void LeaderAboveThreshold_StaysRiskOn()
    {
        Signal signal = GenerateSignal(
            threshold: 0.02m,
            winnerEnd: 105m,
            runnerUpEnd: 102m);

        Assert.True(signal.IsRiskOn);
        Assert.Equal(0.05m, signal.AbsoluteReturn);
        Assert.Equal(Winner.Ticker, signal.Allocations.Single().Instrument.Ticker);
    }

    [Fact]
    public void LeaderEqualToThreshold_FallsBackToRiskOff()
    {
        Signal signal = GenerateSignal(
            threshold: 0.05m,
            winnerEnd: 105m,
            runnerUpEnd: 104m);

        Allocation allocation = signal.Allocations.Single();
        Assert.False(signal.IsRiskOn);
        Assert.Equal(Safe.Ticker, allocation.Instrument.Ticker);
        Assert.Equal(0.05m, signal.AbsoluteReturn);
    }

    [Fact]
    public void LeaderBelowThreshold_GoesRiskOff()
    {
        Signal signal = GenerateSignal(
            threshold: 0.05m,
            winnerEnd: 104m,
            runnerUpEnd: 103m);

        Assert.False(signal.IsRiskOn);
        Assert.Equal(Safe.Ticker, signal.Allocations.Single().Instrument.Ticker);
        Assert.Equal(0.04m, signal.AbsoluteReturn);
    }

    [Fact]
    public void ZeroThreshold_AllowsAnyPositiveReturn()
    {
        Signal signal = GenerateSignal(
            threshold: 0m,
            winnerEnd: 101m,
            runnerUpEnd: 99m);

        Assert.True(signal.IsRiskOn);
        Assert.Equal(0.01m, signal.AbsoluteReturn);
    }

    private static Signal GenerateSignal(decimal threshold, decimal winnerEnd, decimal runnerUpEnd)
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [Winner.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, winnerEnd)),
            [RunnerUp.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, runnerUpEnd)),
            [Safe.Ticker] = Series((2024, 2, 29, 1m))
        });

        var momentum = new MomentumParameters(
            windowMonths: 1,
            rankingMode: RankingMode.Top1,
            useAbsoluteMomentum: true,
            absoluteThreshold: threshold);

        var portfolio = new PortfolioConfiguration(new[] { Winner, RunnerUp }, Safe, momentum);
        var engine = new GemEngine(repository);
        return engine.GenerateSignal(new DateOnly(2024, 2, 29), portfolio);
    }

    private static IReadOnlyList<PricePoint> Series(params (int Year, int Month, int Day, decimal Close)[] points)
    {
        return points
            .Select(p => new PricePoint(new DateOnly(p.Year, p.Month, p.Day), p.Close))
            .ToList();
    }
}

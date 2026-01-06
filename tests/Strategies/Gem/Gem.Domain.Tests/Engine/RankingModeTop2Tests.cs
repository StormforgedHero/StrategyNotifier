using Gem.Domain.Engine;
using Gem.Domain.Model;
using Gem.Domain.Tests.TestSupport;

namespace Gem.Domain.Tests.Engine;

public sealed class RankingModeTop2Tests
{
    private static readonly Instrument RiskOne = new("R1", "Risk One");
    private static readonly Instrument RiskTwo = new("R2", "Risk Two");
    private static readonly Instrument RiskThree = new("R3", "Risk Three");
    private static readonly Instrument Safe = new("SAFE", "Safe");

    [Fact]
    public void PicksBestTwoOutOfThree()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [RiskOne.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 120m)),
            [RiskTwo.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 115m)),
            [RiskThree.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 110m)),
            [Safe.Ticker] = Series((2024, 2, 29, 1m))
        });

        Signal signal = CreateEngine(repository).GenerateSignal(
            new DateOnly(2024, 2, 29),
            CreatePortfolio());

        Assert.True(signal.IsRiskOn);
        Assert.Equal(2, signal.Allocations.Count);
        Assert.Equal(RiskOne.Ticker, signal.Allocations[0].Instrument.Ticker);
        Assert.Equal(RiskTwo.Ticker, signal.Allocations[1].Instrument.Ticker);
        Assert.All(signal.Allocations, a => Assert.Equal(0.5m, a.Weight));
    }

    [Fact]
    public void TiesPreserveRiskOnOrder()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [RiskOne.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 120m)),
            [RiskTwo.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 120m)),
            [RiskThree.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 110m)),
            [Safe.Ticker] = Series((2024, 2, 29, 1m))
        });

        Signal signal = CreateEngine(repository).GenerateSignal(
            new DateOnly(2024, 2, 29),
            CreatePortfolio());

        Assert.Equal(new[] { RiskOne.Ticker, RiskTwo.Ticker }, signal.Allocations.Select(a => a.Instrument.Ticker));
    }

    [Fact]
    public void SingleCandidateFallsBackToTop1()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [RiskOne.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 120m)),
            [Safe.Ticker] = Series((2024, 2, 29, 1m))
        });

        var portfolio = new PortfolioConfiguration(
            new[] { RiskOne },
            Safe,
            new MomentumParameters(windowMonths: 1, rankingMode: RankingMode.Top2, useAbsoluteMomentum: true, absoluteThreshold: 0m));

        Signal signal = CreateEngine(repository).GenerateSignal(new DateOnly(2024, 2, 29), portfolio);

        Allocation allocation = signal.Allocations.Single();
        Assert.Equal(RiskOne.Ticker, allocation.Instrument.Ticker);
        Assert.Equal(1m, allocation.Weight);
    }

    private static GemEngine CreateEngine(DictionaryPriceSeriesRepository repository)
    {
        return new GemEngine(repository);
    }

    private static PortfolioConfiguration CreatePortfolio()
    {
        var momentum = new MomentumParameters(windowMonths: 1, rankingMode: RankingMode.Top2, useAbsoluteMomentum: true, absoluteThreshold: 0m);
        return new PortfolioConfiguration(new[] { RiskOne, RiskTwo, RiskThree }, Safe, momentum);
    }

    private static IReadOnlyList<PricePoint> Series(params (int Year, int Month, int Day, decimal Close)[] points)
    {
        return points
            .Select(p => new PricePoint(new DateOnly(p.Year, p.Month, p.Day), p.Close))
            .ToList();
    }
}

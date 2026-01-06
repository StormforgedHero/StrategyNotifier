using Gem.Domain.Engine;
using Gem.Domain.Exceptions;
using Gem.Domain.Model;
using Gem.Domain.Tests.TestSupport;

namespace Gem.Domain.Tests.Engine;

public sealed class MissingDataRiskOffOrSkipTests
{
    private static readonly Instrument Risk = new("RISK", "Risk Asset");
    private static readonly Instrument AltRisk = new("ALT", "Alt Risk");
    private static readonly Instrument Safe = new("SAFE", "Safe Asset");

    [Fact]
    public void MissingSafeSeries_StillAllowsRiskOffAllocation()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [Risk.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 95m)),
            [AltRisk.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 96m))
        });

        var momentum = new MomentumParameters(windowMonths: 1, RankingMode.Top1, useAbsoluteMomentum: true, absoluteThreshold: 0m);
        var portfolio = new PortfolioConfiguration(new[] { Risk, AltRisk }, Safe, momentum);
        Signal signal = new GemEngine(repository).GenerateSignal(new DateOnly(2024, 2, 29), portfolio);

        Assert.False(signal.IsRiskOn);
        Assert.Equal(Safe.Ticker, signal.Allocations.Single().Instrument.Ticker);
    }

    [Fact]
    public void EmptyRiskSeries_TriggersValidationFailure()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [Risk.Ticker] = Array.Empty<PricePoint>(),
            [AltRisk.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 103m)),
            [Safe.Ticker] = Series((2024, 2, 29, 1m))
        });

        Assert.Throws<DomainValidationException>(() => new GemEngine(repository).GenerateSignal(
            new DateOnly(2024, 2, 29),
            CreatePortfolio()));
    }

    [Fact]
    public void MissingDataBeforeAsOf_Throws()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [Risk.Ticker] = Series((2024, 3, 1, 100m)),
            [AltRisk.Ticker] = Series((2024, 3, 1, 101m)),
            [Safe.Ticker] = Series((2024, 3, 1, 1m))
        });

        var ex = Assert.Throws<DomainValidationException>(() => new GemEngine(repository).GenerateSignal(
            new DateOnly(2024, 2, 15),
            CreatePortfolio()));

        Assert.Contains("No price data on or before", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PortfolioConfiguration CreatePortfolio()
    {
        var momentum = new MomentumParameters(windowMonths: 1, RankingMode.Top1, useAbsoluteMomentum: true, absoluteThreshold: 0m);
        return new PortfolioConfiguration(new[] { Risk, AltRisk }, Safe, momentum);
    }

    private static IReadOnlyList<PricePoint> Series(params (int Year, int Month, int Day, decimal Close)[] points)
    {
        return points
            .Select(p => new PricePoint(new DateOnly(p.Year, p.Month, p.Day), p.Close))
            .ToList();
    }
}

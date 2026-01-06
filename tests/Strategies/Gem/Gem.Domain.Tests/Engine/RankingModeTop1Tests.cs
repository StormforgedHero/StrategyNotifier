using Gem.Domain.Engine;
using Gem.Domain.Exceptions;
using Gem.Domain.Model;
using Gem.Domain.Tests.TestSupport;

namespace Gem.Domain.Tests.Engine;

public sealed class RankingModeTop1Tests
{
    private static readonly Instrument Alpha = new("ALPHA", "Alpha");
    private static readonly Instrument Bravo = new("BRAVO", "Bravo");
    private static readonly Instrument Safe = new("SAFE", "Safe");

    [Fact]
    public void SelectsClearWinner()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [Alpha.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 120m)),
            [Bravo.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 110m)),
            [Safe.Ticker] = Series((2024, 2, 29, 1m))
        });

        Signal signal = CreateEngine(repository).GenerateSignal(
            new DateOnly(2024, 2, 29),
            CreatePortfolio());

        Allocation allocation = signal.Allocations.Single();
        Assert.True(signal.IsRiskOn);
        Assert.Equal(Alpha.Ticker, allocation.Instrument.Ticker);
        Assert.Equal(1m, allocation.Weight);
        Assert.Equal(1, signal.RelativeRank);
    }

    [Fact]
    public void TiesResolveByConfigOrder()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [Alpha.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 110m)),
            [Bravo.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 110m)),
            [Safe.Ticker] = Series((2024, 2, 29, 1m))
        });

        Signal signal = CreateEngine(repository).GenerateSignal(
            new DateOnly(2024, 2, 29),
            CreatePortfolio());

        Allocation allocation = signal.Allocations.Single();
        Assert.True(signal.IsRiskOn);
        Assert.Equal(Alpha.Ticker, allocation.Instrument.Ticker);
        Assert.Equal(0.1m, signal.AbsoluteReturn);
    }

    [Fact]
    public void EmptySeries_ThrowsValidation()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [Alpha.Ticker] = Series((2024, 1, 15, 100m), (2024, 1, 31, 100m), (2024, 2, 29, 110m)),
            [Bravo.Ticker] = Array.Empty<PricePoint>(),
            [Safe.Ticker] = Series((2024, 2, 29, 1m))
        });

        var ex = Assert.Throws<DomainValidationException>(() => CreateEngine(repository).GenerateSignal(
            new DateOnly(2024, 2, 29),
            CreatePortfolio()));

        Assert.Contains("Price series is empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GemEngine CreateEngine(DictionaryPriceSeriesRepository repository)
    {
        return new GemEngine(repository);
    }

    private static PortfolioConfiguration CreatePortfolio()
    {
        var momentum = new MomentumParameters(1, RankingMode.Top1, useAbsoluteMomentum: true, absoluteThreshold: 0m);
        return new PortfolioConfiguration(new[] { Alpha, Bravo }, Safe, momentum);
    }

    private static IReadOnlyList<PricePoint> Series(params (int Year, int Month, int Day, decimal Close)[] points)
    {
        return points
            .Select(p => new PricePoint(new DateOnly(p.Year, p.Month, p.Day), p.Close))
            .ToList();
    }
}

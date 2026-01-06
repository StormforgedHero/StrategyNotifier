using Gem.Domain.Engine;
using Gem.Domain.Exceptions;
using Gem.Domain.Model;
using Gem.Domain.Tests.TestSupport;

namespace Gem.Domain.Tests.Engine;

public sealed class CalculateReturnTests
{
    private static readonly Instrument RiskA = new("RISK-A", "Risk A");
    private static readonly Instrument RiskB = new("RISK-B", "Risk B");
    private static readonly Instrument Safe = new("SAFE", "Safe Asset");

    [Fact]
    public void RisingPrices_YieldPositiveReturn()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [RiskA.Ticker] = Series((2024, 1, 31, 100m), (2024, 3, 29, 125m)),
            [RiskB.Ticker] = Series((2024, 1, 31, 100m), (2024, 3, 29, 110m)),
            [Safe.Ticker] = Series((2024, 3, 29, 1m))
        });

        Signal signal = CreateEngine(repository).GenerateSignal(
            new DateOnly(2024, 3, 31),
            CreatePortfolio(windowMonths: 2));

        Assert.True(signal.IsRiskOn);
        Assert.Equal(RiskA.Ticker, signal.Allocations.Single().Instrument.Ticker);
        Assert.Equal(0.25m, signal.AbsoluteReturn);
    }

    [Fact]
    public void FallingPrices_TriggerRiskOff()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [RiskA.Ticker] = Series((2024, 1, 31, 100m), (2024, 3, 29, 90m)),
            [RiskB.Ticker] = Series((2024, 1, 31, 100m), (2024, 3, 29, 95m)),
            [Safe.Ticker] = Series((2024, 3, 29, 1m))
        });

        Signal signal = CreateEngine(repository).GenerateSignal(
            new DateOnly(2024, 3, 31),
            CreatePortfolio(windowMonths: 2));

        Allocation allocation = signal.Allocations.Single();
        Assert.False(signal.IsRiskOn);
        Assert.Equal(Safe.Ticker, allocation.Instrument.Ticker);
        Assert.Equal(1m, allocation.Weight);
        Assert.Equal(-0.05m, signal.AbsoluteReturn);
    }

    [Fact]
    public void FlatPrices_YieldZeroReturnAndRiskOff()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [RiskA.Ticker] = Series((2024, 1, 31, 100m), (2024, 3, 29, 100m)),
            [RiskB.Ticker] = Series((2024, 1, 31, 100m), (2024, 3, 29, 99m)),
            [Safe.Ticker] = Series((2024, 3, 29, 1m))
        });

        Signal signal = CreateEngine(repository).GenerateSignal(
            new DateOnly(2024, 3, 31),
            CreatePortfolio(windowMonths: 2));

        Allocation allocation = signal.Allocations.Single();
        Assert.False(signal.IsRiskOn);
        Assert.Equal(Safe.Ticker, allocation.Instrument.Ticker);
        Assert.Equal(0m, signal.AbsoluteReturn);
    }

    [Fact]
    public void UsesLastAvailablePricesBeforeWindowAndAsOfDate()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [RiskA.Ticker] = Series(
                (2024, 1, 15, 90m),
                (2024, 2, 15, 110m),
                (2024, 3, 30, 132m)),
            [RiskB.Ticker] = Series((2024, 1, 31, 100m), (2024, 3, 29, 120m)),
            [Safe.Ticker] = Series((2024, 3, 29, 1m))
        });

        Signal signal = CreateEngine(repository).GenerateSignal(
            new DateOnly(2024, 3, 31),
            CreatePortfolio(windowMonths: 1));

        // Start price falls back to the last available quote before the window start (2024-02-15).
        Assert.True(signal.IsRiskOn);
        Assert.Equal(0.2m, signal.AbsoluteReturn);
    }

    [Fact]
    public void InsufficientHistory_ThrowsDomainValidation()
    {
        var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            [RiskA.Ticker] = Series((2024, 2, 1, 100m), (2024, 3, 1, 101m)),
            [RiskB.Ticker] = Series((2024, 2, 1, 100m), (2024, 3, 1, 101m)),
            [Safe.Ticker] = Series((2024, 3, 1, 1m))
        });

        var ex = Assert.Throws<DomainValidationException>(() => CreateEngine(repository).GenerateSignal(
            new DateOnly(2024, 3, 31),
            CreatePortfolio(windowMonths: 6)));

        Assert.Contains("Insufficient history", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GemEngine CreateEngine(DictionaryPriceSeriesRepository repository)
    {
        return new GemEngine(repository);
    }

    private static PortfolioConfiguration CreatePortfolio(int windowMonths, IEnumerable<Instrument>? riskOn = null)
    {
        IReadOnlyList<Instrument> riskInstruments = (riskOn ?? new[] { RiskA, RiskB }).ToList();
        var momentum = new MomentumParameters(windowMonths, RankingMode.Top1, useAbsoluteMomentum: true, absoluteThreshold: 0m);
        return new PortfolioConfiguration(riskInstruments, Safe, momentum);
    }

    private static IReadOnlyList<PricePoint> Series(params (int Year, int Month, int Day, decimal Close)[] points)
    {
        return points
            .Select(p => new PricePoint(new DateOnly(p.Year, p.Month, p.Day), p.Close))
            .ToList();
    }
}

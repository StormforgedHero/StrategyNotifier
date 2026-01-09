using Gem.Domain.Engine;
using Gem.Domain.Exceptions;
using Gem.Domain.Model;
using Gem.Domain.Tests.TestSupport;

namespace Gem.Domain.Tests;

public sealed class GemEngineTests
{
    private static readonly Instrument UsEquity = new("US", "US Equity");
    private static readonly Instrument ExUsEquity = new("XUS", "Ex-US Equity");
    private static readonly Instrument SafeAsset = new("BND", "Safe Asset");

    [Fact]
    public void CalculateReturn_UsesLastAvailablePricesBeforeDates()
    {
        var repository = new InMemoryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            ["US"] = new[]
            {
                new PricePoint(new DateOnly(2024, 1, 5), 90m),
                new PricePoint(new DateOnly(2024, 1, 25), 100m),
                new PricePoint(new DateOnly(2024, 2, 20), 140m)
            },
            ["XUS"] = new[]
            {
                new PricePoint(new DateOnly(2024, 1, 4), 90m),
                new PricePoint(new DateOnly(2024, 2, 10), 100m),
                new PricePoint(new DateOnly(2024, 2, 22), 105m)
            },
            ["BND"] = new[]
            {
                new PricePoint(new DateOnly(2024, 2, 20), 1m)
            }
        });

        var engine = new GemEngine(repository);
        var portfolio = new PortfolioConfiguration(
            new[] { UsEquity, ExUsEquity },
            SafeAsset,
            new MomentumParameters(windowMonths: 1));

        Signal signal = engine.GenerateSignal(new DateOnly(2024, 2, 28), portfolio);

        Assert.True(signal.IsRiskOn);
        Assert.Equal(UsEquity.Ticker, signal.Allocations.Single().Instrument.Ticker);
        Assert.Equal(0.4m, signal.AbsoluteReturn);
    }

    [Fact]
    public void AbsoluteMomentum_FallsBackToRiskOffWhenThresholdNotMet()
    {
        var repository = new InMemoryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            ["US"] = new[]
            {
                new PricePoint(new DateOnly(2024, 1, 15), 100m),
                new PricePoint(new DateOnly(2024, 1, 31), 100m),
                new PricePoint(new DateOnly(2024, 2, 29), 102m)
            },
            ["XUS"] = new[]
            {
                new PricePoint(new DateOnly(2024, 1, 15), 100m),
                new PricePoint(new DateOnly(2024, 1, 31), 100m),
                new PricePoint(new DateOnly(2024, 2, 29), 101m)
            },
            ["BND"] = new[]
            {
                new PricePoint(new DateOnly(2024, 2, 29), 1m)
            }
        });

        var engine = new GemEngine(repository);
        var portfolio = new PortfolioConfiguration(
            new[] { UsEquity, ExUsEquity },
            SafeAsset,
            new MomentumParameters(windowMonths: 1, useAbsoluteMomentum: true, absoluteThreshold: 0.05m));

        Signal signal = engine.GenerateSignal(new DateOnly(2024, 2, 29), portfolio);

        Allocation allocation = signal.Allocations.Single();
        Assert.False(signal.IsRiskOn);
        Assert.Equal(SafeAsset.Ticker, allocation.Instrument.Ticker);
        Assert.Equal(1m, allocation.Weight);
        Assert.Equal(0.02m, signal.AbsoluteReturn);
    }

    [Fact]
    public void Top1_AllocatesFullyToLeader()
    {
        var repository = new InMemoryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            ["US"] = new[]
            {
                new PricePoint(new DateOnly(2024, 1, 31), 100m),
                new PricePoint(new DateOnly(2024, 3, 29), 130m)
            },
            ["XUS"] = new[]
            {
                new PricePoint(new DateOnly(2024, 1, 31), 100m),
                new PricePoint(new DateOnly(2024, 3, 29), 110m)
            },
            ["BND"] = new[]
            {
                new PricePoint(new DateOnly(2024, 3, 29), 1m)
            }
        });

        var engine = new GemEngine(repository);
        var portfolio = new PortfolioConfiguration(
            new[] { UsEquity, ExUsEquity },
            SafeAsset,
            new MomentumParameters(windowMonths: 2, rankingMode: RankingMode.Top1));

        Signal signal = engine.GenerateSignal(new DateOnly(2024, 3, 31), portfolio);

        Allocation allocation = signal.Allocations.Single();
        Assert.True(signal.IsRiskOn);
        Assert.Equal(UsEquity.Ticker, allocation.Instrument.Ticker);
        Assert.Equal(1m, allocation.Weight);
    }

    [Fact]
    public void Top2_SplitsEvenlyAcrossBestTwo()
    {
        var repository = new InMemoryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            ["US"] = new[]
            {
                new PricePoint(new DateOnly(2024, 1, 15), 100m),
                new PricePoint(new DateOnly(2024, 1, 31), 100m),
                new PricePoint(new DateOnly(2024, 4, 30), 140m)
            },
            ["XUS"] = new[]
            {
                new PricePoint(new DateOnly(2024, 1, 15), 100m),
                new PricePoint(new DateOnly(2024, 1, 31), 100m),
                new PricePoint(new DateOnly(2024, 4, 30), 130m)
            },
            ["BND"] = new[]
            {
                new PricePoint(new DateOnly(2024, 4, 30), 1m)
            }
        });

        var engine = new GemEngine(repository);
        var portfolio = new PortfolioConfiguration(
            new[] { UsEquity, ExUsEquity },
            SafeAsset,
            new MomentumParameters(windowMonths: 3, rankingMode: RankingMode.Top2));

        Signal signal = engine.GenerateSignal(new DateOnly(2024, 4, 30), portfolio);

        Assert.True(signal.IsRiskOn);
        Assert.Equal(2, signal.Allocations.Count);
        Assert.All(signal.Allocations, allocation => Assert.Equal(0.5m, allocation.Weight));
    }

    [Fact]
    public void GenerateSignal_ThrowsWhenHistoryMissing()
    {
        var repository = new InMemoryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
        {
            ["US"] = new[]
            {
                new PricePoint(new DateOnly(2024, 2, 1), 100m)
            },
            ["XUS"] = new[]
            {
                new PricePoint(new DateOnly(2024, 2, 1), 100m)
            },
            ["BND"] = new[]
            {
                new PricePoint(new DateOnly(2024, 2, 1), 1m)
            }
        });

        var engine = new GemEngine(repository);
        var portfolio = new PortfolioConfiguration(
            new[] { UsEquity, ExUsEquity },
            SafeAsset,
            new MomentumParameters(windowMonths: 12));

        Assert.Throws<DomainValidationException>(
            () => engine.GenerateSignal(new DateOnly(2024, 2, 28), portfolio));
    }

}

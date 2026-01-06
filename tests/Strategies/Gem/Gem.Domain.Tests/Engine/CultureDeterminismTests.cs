using System.Globalization;
using Gem.Domain.Engine;
using Gem.Domain.Model;
using Gem.Domain.Tests.TestSupport;

namespace Gem.Domain.Tests.Engine;

public sealed class CultureDeterminismTests
{
    [Fact]
    public void BuildComment_UsesInvariantCultureUnderPolishLocale()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var polish = new CultureInfo("pl-PL");
            CultureInfo.CurrentCulture = polish;
            CultureInfo.CurrentUICulture = polish;

            var repository = new DictionaryPriceSeriesRepository(new Dictionary<string, IReadOnlyList<PricePoint>>
            {
                ["WIN"] = new[]
                {
                    new PricePoint(new DateOnly(2024, 1, 15), 100m),
                    new PricePoint(new DateOnly(2024, 1, 31), 100m),
                    new PricePoint(new DateOnly(2024, 2, 29), 150m)
                },
                ["SAFE"] = new[] { new PricePoint(new DateOnly(2024, 2, 29), 1m) }
            });

            var momentum = new MomentumParameters(windowMonths: 1, RankingMode.Top1, useAbsoluteMomentum: true, absoluteThreshold: 0m);
            var portfolio = new PortfolioConfiguration(new[] { new Instrument("WIN", "Winner") }, new Instrument("SAFE", "Safe"), momentum);
            Signal signal = new GemEngine(repository).GenerateSignal(new DateOnly(2024, 2, 29), portfolio);

            Assert.Contains("50.00 %", signal.Comment, StringComparison.Ordinal);
            Assert.DoesNotContain("50,00", signal.Comment, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}

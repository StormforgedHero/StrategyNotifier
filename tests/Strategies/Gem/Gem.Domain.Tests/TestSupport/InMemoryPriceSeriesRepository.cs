using Gem.Domain.Model;
using Gem.Domain.Pricing;

namespace Gem.Domain.Tests.TestSupport;

internal sealed class InMemoryPriceSeriesRepository : IPriceSeriesRepository
{
    private readonly Dictionary<string, IReadOnlyList<PricePoint>> _data;

    public InMemoryPriceSeriesRepository(Dictionary<string, IReadOnlyList<PricePoint>> data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public IReadOnlyList<PricePoint> GetSeries(Instrument instrument)
    {
        if (!_data.TryGetValue(instrument.Ticker, out IReadOnlyList<PricePoint>? series))
        {
            throw new InvalidOperationException($"Missing price series for {instrument.Ticker}.");
        }

        return series;
    }
}

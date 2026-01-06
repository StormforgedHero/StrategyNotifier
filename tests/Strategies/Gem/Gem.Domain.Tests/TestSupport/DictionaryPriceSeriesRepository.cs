using System.Collections.Generic;
using Gem.Domain.Model;
using Gem.Domain.Pricing;

namespace Gem.Domain.Tests.TestSupport;

internal sealed class DictionaryPriceSeriesRepository : IPriceSeriesRepository
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<PricePoint>> _data;

    public DictionaryPriceSeriesRepository(IReadOnlyDictionary<string, IReadOnlyList<PricePoint>> data)
    {
        _data = data;
    }

    public IReadOnlyList<PricePoint> GetSeries(Instrument instrument)
    {
        if (instrument is null)
        {
            throw new ArgumentNullException(nameof(instrument));
        }

        if (!_data.TryGetValue(instrument.Ticker, out IReadOnlyList<PricePoint>? series))
        {
            throw new InvalidOperationException($"Missing price series for {instrument.Ticker}.");
        }

        return series;
    }
}

using Gem.Domain.Model;
using Gem.Domain.Pricing;

namespace Gem.Cli.Pricing
{
    public sealed class FilePriceSeriesRepository : IPriceSeriesRepository
    {
        private readonly string _storeDirectory;
        private readonly IPriceDataProvider _provider;
        private readonly Dictionary<string, IReadOnlyList<PricePoint>> _seriesCache =
            new(StringComparer.OrdinalIgnoreCase);

        public FilePriceSeriesRepository(
            string storeDirectory,
            IPriceDataProvider provider)
        {
            if (string.IsNullOrWhiteSpace(storeDirectory))
            {
                throw new ArgumentException("Store directory must not be empty.", nameof(storeDirectory));
            }

            _storeDirectory = storeDirectory;
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public IReadOnlyList<PricePoint> GetSeries(Instrument instrument)
        {
            if (instrument is null)
            {
                throw new ArgumentNullException(nameof(instrument));
            }

            string symbolKey = instrument.GetNormalizedSymbol();

            if (_seriesCache.TryGetValue(symbolKey, out IReadOnlyList<PricePoint>? cached))
            {
                return cached;
            }

            string path = Path.Combine(_storeDirectory, instrument.GetCacheFileName());

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Price data file not found for instrument '{instrument.Ticker}' in store directory.",
                    path);
            }

            IReadOnlyList<PricePoint> series = _provider.LoadSeries(path);
            _seriesCache[symbolKey] = series;
            return series;
        }
    }
}

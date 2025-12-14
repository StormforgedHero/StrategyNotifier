using Gem.Domain.Model;
using Gem.Domain.Pricing;

namespace Gem.Cli.Pricing
{
    public sealed class FilePriceSeriesRepository : IPriceSeriesRepository
    {
        private readonly string _baseDirectory;
        private readonly IPriceDataProvider _provider;
        private readonly Dictionary<string, IReadOnlyList<PricePoint>> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        public FilePriceSeriesRepository(string baseDirectory, IPriceDataProvider provider)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new ArgumentException("Cache directory must not be empty.", nameof(baseDirectory));
            }

            _baseDirectory = baseDirectory;
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public IReadOnlyList<PricePoint> GetSeries(Instrument instrument)
        {
            if (instrument is null)
            {
                throw new ArgumentNullException(nameof(instrument));
            }

            string path = Path.Combine(_baseDirectory, instrument.GetCacheFileName());

            if (_cache.TryGetValue(path, out IReadOnlyList<PricePoint>? cached))
            {
                return cached;
            }

            IReadOnlyList<PricePoint> series = _provider.LoadSeries(path);
            _cache[path] = series;
            return series;
        }
    }
}

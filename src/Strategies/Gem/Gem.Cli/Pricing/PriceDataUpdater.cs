using System.Globalization;
using System.Text;
using Gem.Cli.Utilities;
using Gem.Domain.Model;
using Gem.Domain.Pricing;

namespace Gem.Cli.Pricing
{
    public sealed class PriceDataUpdater : IPriceDataUpdater, IDisposable
    {
        private readonly StooqCsvHttpDataProvider _provider;
        private readonly string _cacheDirectory;
        private readonly TimeSpan _freshnessWindow;
        private readonly TimeSpan _minDelay;
        private readonly HashSet<string> _requestedSymbols = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public PriceDataUpdater(
            StooqCsvHttpDataProvider provider,
            string cacheDirectory,
            TimeSpan freshnessWindow,
            TimeSpan minDelay)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

            if (string.IsNullOrWhiteSpace(cacheDirectory))
            {
                throw new ArgumentException("Cache directory must not be empty.", nameof(cacheDirectory));
            }

            _cacheDirectory = cacheDirectory;
            _freshnessWindow = freshnessWindow <= TimeSpan.Zero ? TimeSpan.FromDays(2) : freshnessWindow;
            _minDelay = minDelay < TimeSpan.Zero ? TimeSpan.Zero : minDelay;
        }

        public async Task UpdateAsync(
            IEnumerable<Instrument> instruments,
            bool forceUpdate,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PriceDataUpdater));
            }

            ArgumentNullException.ThrowIfNull(instruments);

            Directory.CreateDirectory(_cacheDirectory);

            bool firstRequest = true;

            foreach (Instrument instrument in instruments)
            {
                string symbol = GetSymbol(instrument);

                if (!_requestedSymbols.Add(symbol))
                {
                    continue;
                }

                string path = Path.Combine(_cacheDirectory, $"{symbol}.csv");

                if (!forceUpdate && !IsStale(path))
                {
                    continue;
                }

                if (!firstRequest && _minDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_minDelay, cancellationToken);
                }

                IReadOnlyList<PricePoint> series = await _provider.LoadSeriesAsync(symbol, cancellationToken);

                WriteSeries(path, series);
                firstRequest = false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _provider.Dispose();
        }

        private bool IsStale(string path)
        {
            if (!File.Exists(path))
            {
                return true;
            }

            try
            {
                using var stream = File.OpenRead(path);
                IReadOnlyList<PricePoint> series = PriceCsvParser.ParseFromFile(stream);
                DateOnly lastDate = series.Last().Date;
                DateOnly threshold = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-_freshnessWindow.TotalDays));
                return lastDate < threshold;
            }
            catch
            {
                return true;
            }
        }

        private static void WriteSeries(string path, IReadOnlyList<PricePoint> series)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Date,Close");

            foreach (PricePoint point in series.OrderBy(p => p.Date))
            {
                builder.Append(point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.AppendLine(point.Close.ToString(CultureInfo.InvariantCulture));
            }

            File.WriteAllText(path, builder.ToString(), FileEncodings.Utf8NoBom);
        }

        private static string GetSymbol(Instrument instrument)
        {
            if (instrument is null)
            {
                throw new ArgumentNullException(nameof(instrument));
            }

            return instrument.GetNormalizedSymbol();
        }
    }
}

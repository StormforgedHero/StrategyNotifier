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
        private readonly string _storeDirectory;
        private readonly bool _saveUpdatedDataToStore;
        private readonly TimeSpan _maxAgeWindow;
        private readonly TimeSpan _requestDelay;
        private readonly TimeSpan _attemptCooldown;
        private readonly string _attemptExtension = ".last_attempt";
        private readonly HashSet<string> _requestedSymbols = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public PriceDataUpdater(
            StooqCsvHttpDataProvider provider,
            string cacheDirectory,
            string storeDirectory,
            bool saveUpdatedDataToStore,
            TimeSpan maxAgeWindow,
            TimeSpan requestDelay,
            TimeSpan attemptCooldown)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

            if (string.IsNullOrWhiteSpace(cacheDirectory))
            {
                throw new ArgumentException("Cache directory must not be empty.", nameof(cacheDirectory));
            }

            if (string.IsNullOrWhiteSpace(storeDirectory))
            {
                throw new ArgumentException("Store directory must not be empty.", nameof(storeDirectory));
            }

            _cacheDirectory = cacheDirectory;
            _storeDirectory = storeDirectory;
            _saveUpdatedDataToStore = saveUpdatedDataToStore;
            _maxAgeWindow = maxAgeWindow < TimeSpan.Zero ? TimeSpan.Zero : maxAgeWindow;
            _requestDelay = requestDelay < TimeSpan.Zero ? TimeSpan.Zero : requestDelay;
            _attemptCooldown = attemptCooldown < TimeSpan.Zero ? TimeSpan.Zero : attemptCooldown;
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

                string cachePath = Path.Combine(_cacheDirectory, $"{symbol}.csv");
                string storePath = Path.Combine(_storeDirectory, $"{symbol}.csv");

                string attemptPath = Path.Combine(_cacheDirectory, $"{symbol}{_attemptExtension}");

                if (!forceUpdate && !IsStale(cachePath, storePath))
                {
                    continue;
                }

                if (!forceUpdate && ShouldSkipForCooldown(attemptPath))
                {
                    continue;
                }

                if (!firstRequest && _requestDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_requestDelay, cancellationToken);
                }

                WriteLastAttempt(attemptPath);

                try
                {
                    IReadOnlyList<PricePoint> series = await _provider.LoadSeriesAsync(symbol, cancellationToken);

                    string content = BuildSeriesContent(series);

                    WriteAtomically(cachePath, content);

                    if (_saveUpdatedDataToStore)
                    {
                        WriteAtomically(storePath, content);
                    }
                }
                finally
                {
                    firstRequest = false;
                }
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

        private bool IsStale(string cachePath, string storePath)
        {
            DateOnly? latest = _saveUpdatedDataToStore
                ? TryReadLastDate(storePath)
                : TryReadLastDate(cachePath);

            if (!latest.HasValue)
            {
                return true;
            }

            DateOnly threshold = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-_maxAgeWindow.TotalDays));
            return latest.Value < threshold;
        }

        private bool ShouldSkipForCooldown(string attemptPath)
        {
            if (_attemptCooldown <= TimeSpan.Zero)
            {
                return false;
            }

            DateTimeOffset? lastAttempt = ReadLastAttempt(attemptPath);

            if (lastAttempt is null)
            {
                return false;
            }

            TimeSpan elapsed = DateTimeOffset.UtcNow - lastAttempt.Value;
            return elapsed < _attemptCooldown;
        }

        private static DateTimeOffset? ReadLastAttempt(string attemptPath)
        {
            try
            {
                if (!File.Exists(attemptPath))
                {
                    return null;
                }

                string content = File.ReadAllText(attemptPath, Encoding.UTF8).Trim();
                return DateTimeOffset.TryParseExact(content, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static void WriteLastAttempt(string attemptPath)
        {
            string? directory = Path.GetDirectoryName(attemptPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(attemptPath, DateTimeOffset.UtcNow.ToString("O"), FileEncodings.Utf8NoBom);
        }

        private static void WriteAtomically(string path, string content)
        {
            string? directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = Path.Combine(directory ?? ".", Path.GetRandomFileName());
            File.WriteAllText(tempPath, content, FileEncodings.Utf8NoBom);
            File.Move(tempPath, path, overwrite: true);
        }

        private static string BuildSeriesContent(IReadOnlyList<PricePoint> series)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Date,Close");

            foreach (PricePoint point in series.OrderBy(p => p.Date))
            {
                builder.Append(point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.AppendLine(point.Close.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string GetSymbol(Instrument instrument)
        {
            if (instrument is null)
            {
                throw new ArgumentNullException(nameof(instrument));
            }

            return instrument.GetNormalizedSymbol();
        }

        private static DateOnly? TryReadLastDate(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using var stream = File.OpenRead(path);
                IReadOnlyList<PricePoint> series = PriceCsvParser.ParseFromFile(stream);
                return series.Count == 0 ? null : series[^1].Date;
            }
            catch
            {
                return null;
            }
        }
    }
}

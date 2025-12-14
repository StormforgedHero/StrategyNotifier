using System.Net.Http;
using Gem.Domain.Model;
using Gem.Domain.Pricing;

namespace Gem.Cli.Pricing
{
    public sealed class StooqCsvHttpDataProvider : IPriceDataProvider, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly bool _ownsClient;

        public StooqCsvHttpDataProvider(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            _ownsClient = httpClient is null;
        }

        public IReadOnlyList<PricePoint> LoadSeries(string path)
        {
            return LoadSeriesAsync(path).GetAwaiter().GetResult();
        }

        public async Task<IReadOnlyList<PricePoint>> LoadSeriesAsync(
            string symbol,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("Symbol must not be empty.", nameof(symbol));
            }

            string normalized = symbol.Trim().ToLowerInvariant();
            string url = $"https://stooq.pl/q/d/l/?s={normalized}&i=d";

            string csv = await _httpClient.GetStringAsync(url, cancellationToken);

            string trimmed = csv.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new FormatException($"Stooq returned empty content for symbol '{normalized}'.");
            }

            if (trimmed.StartsWith("<", StringComparison.Ordinal))
            {
                throw new FormatException($"Download did not return CSV content for symbol '{normalized}'.");
            }

            if (trimmed.Contains("brak danych", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("no data", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException($"Stooq returned no data for symbol '{normalized}' (Brak danych). Check symbol mapping or endpoint.");
            }

            return PriceCsvParser.ParseFromText(csv);
        }

        public void Dispose()
        {
            if (_ownsClient)
            {
                _httpClient.Dispose();
            }
        }
    }
}

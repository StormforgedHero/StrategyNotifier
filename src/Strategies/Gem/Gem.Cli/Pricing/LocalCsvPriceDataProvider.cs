using Gem.Domain.Pricing;
using Gem.Domain.Model;

namespace Gem.Cli.Pricing
{
    public sealed class LocalCsvPriceDataProvider : IPriceDataProvider
    {
        public IReadOnlyList<PricePoint> LoadSeries(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must not be empty.", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Price data file not found at '{path}'.", path);
            }

            using var stream = File.OpenRead(path);
            return PriceCsvParser.ParseFromFile(stream);
        }
    }
}

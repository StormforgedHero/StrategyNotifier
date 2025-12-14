using Gem.Domain.Exceptions;

namespace Gem.Domain.Model
{
    public sealed class Instrument
    {
        public Instrument(string ticker, string name, string? sourceSymbol = null)
        {
            if (string.IsNullOrWhiteSpace(ticker))
            {
                throw new DomainValidationException("Instrument ticker must be provided.");
            }

            Ticker = ticker.Trim();
            Name = string.IsNullOrWhiteSpace(name) ? Ticker : name.Trim();
            SourceSymbol = string.IsNullOrWhiteSpace(sourceSymbol) ? null : sourceSymbol.Trim();
        }

        public string Ticker { get; }

        public string Name { get; }

        public string? SourceSymbol { get; }

        public string GetCacheFileName()
        {
            return $"{GetNormalizedSymbol()}.csv";
        }

        public string GetNormalizedSymbol()
        {
            string symbol = SourceSymbol ?? Ticker;
            return symbol.Trim().ToLowerInvariant();
        }
    }
}

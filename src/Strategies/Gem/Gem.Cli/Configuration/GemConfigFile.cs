namespace Gem.Cli.Configuration
{
    internal sealed class GemConfigFile
    {
        public int? WindowMonths { get; set; }

        public string? RankingMode { get; set; }

        public InstrumentsConfig? Instruments { get; set; }

        public UpdateConfig? Update { get; set; }

        public string? StoreDirectory { get; set; }

        public string? CacheDirectory { get; set; }

        public string? OutputPath { get; set; }
    }
}

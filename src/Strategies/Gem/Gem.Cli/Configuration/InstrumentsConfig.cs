namespace Gem.Cli.Configuration
{
    internal sealed class InstrumentsConfig
    {
        public InstrumentConfig? UsEquity { get; set; }

        public InstrumentConfig? ExUsEquity { get; set; }

        public InstrumentConfig? SafeAsset { get; set; }
    }
}

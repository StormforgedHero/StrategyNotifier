using System.Collections.Generic;

namespace Gem.Cli.Configuration
{
    internal sealed class InstrumentsConfig
    {
        public List<InstrumentConfig>? RiskOn { get; set; }

        public InstrumentConfig? UsEquity { get; set; }

        public InstrumentConfig? ExUsEquity { get; set; }

        public InstrumentConfig? SafeAsset { get; set; }
    }
}

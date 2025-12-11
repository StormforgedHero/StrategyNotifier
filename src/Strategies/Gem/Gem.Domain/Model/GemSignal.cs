using Gem.Domain.Core;

namespace Gem.Domain.Model
{
    public sealed class GemSignal
    {
        public YearMonth Period { get; }

        public AssetKind Position { get; }

        public GemSignal(YearMonth period, AssetKind position)
        {
            Period = period;
            Position = position;
        }
    }
}

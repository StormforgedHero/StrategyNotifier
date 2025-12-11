using Gem.Domain.Core;
using Gem.Domain.Exceptions;

namespace Gem.Domain.Model
{
    public sealed class GemInputData
    {
        public AssetReturnSeries UsEquity { get; }

        public AssetReturnSeries ExUsEquity { get; }

        public AssetReturnSeries SafeAsset { get; }

        public GemInputData(
            AssetReturnSeries usEquity,
            AssetReturnSeries exUsEquity,
            AssetReturnSeries safeAsset)
        {
            UsEquity = usEquity ?? throw new ArgumentNullException(nameof(usEquity));
            ExUsEquity = exUsEquity ?? throw new ArgumentNullException(nameof(exUsEquity));
            SafeAsset = safeAsset ?? throw new ArgumentNullException(nameof(safeAsset));

            if (UsEquity.AssetKind != AssetKind.UsEquity)
            {
                throw new DomainValidationException(
                    "US equity series must have AssetKind UsEquity.");
            }

            if (ExUsEquity.AssetKind != AssetKind.ExUsEquity)
            {
                throw new DomainValidationException(
                    "Ex-US equity series must have AssetKind ExUsEquity.");
            }

            if (SafeAsset.AssetKind != AssetKind.SafeAsset)
            {
                throw new DomainValidationException(
                    "Safe asset series must have AssetKind SafeAsset.");
            }
        }
    }
}

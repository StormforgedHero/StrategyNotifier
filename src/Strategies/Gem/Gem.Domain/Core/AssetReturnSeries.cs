using Gem.Domain.Exceptions;

namespace Gem.Domain.Core
{
    public sealed class AssetReturnSeries
    {
        private readonly List<MonthlyReturn> _returns;

        public AssetKind AssetKind { get; }

        public IReadOnlyList<MonthlyReturn> Returns => _returns;

        public AssetReturnSeries(AssetKind assetKind, IEnumerable<MonthlyReturn> returns)
        {
            ArgumentNullException.ThrowIfNull(returns);

            AssetKind = assetKind;

            var items = returns.ToList();

            if (items.Count == 0)
            {
                _returns = new List<MonthlyReturn>();
                return;
            }

            items.Sort((left, right) => left.Period.CompareTo(right.Period));

            for (var index = 1; index < items.Count; index++)
            {
                if (items[index].Period == items[index - 1].Period)
                {
                    throw new DomainValidationException(
                        $"Duplicate period {items[index].Period.Year}-{items[index].Period.Month:00} in return series.");
                }
            }

            _returns = items;
        }

        public IReadOnlyList<MonthlyReturn> GetLookbackWindow(YearMonth endPeriod, int lookbackMonths)
        {
            if (lookbackMonths <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lookbackMonths),
                    lookbackMonths,
                    "Lookback months must be greater than zero.");
            }

            if (_returns.Count == 0)
            {
                throw new DomainValidationException("Return series is empty.");
            }

            var eligibleCount = 0;

            for (var index = 0; index < _returns.Count; index++)
            {
                if (_returns[index].Period.CompareTo(endPeriod) <= 0)
                {
                    eligibleCount++;
                }
                else
                {
                    break;
                }
            }

            if (eligibleCount < lookbackMonths)
            {
                throw new DomainValidationException(
                    "Not enough data points to build the requested lookback window.");
            }

            var startIndex = eligibleCount - lookbackMonths;
            var result = new MonthlyReturn[lookbackMonths];

            for (var i = 0; i < lookbackMonths; i++)
            {
                result[i] = _returns[startIndex + i];
            }

            return result;
        }
    }
}
